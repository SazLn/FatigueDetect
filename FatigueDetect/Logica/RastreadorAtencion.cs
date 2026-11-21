using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    internal sealed class RastreadorAtencion
    {
        private readonly List<MuestraAtencion> muestras = new();
        private readonly List<RespuestaAlarma> respuestas = new();
        private readonly Queue<DateTimeOffset> cierres = new(), cabeceos = new();
        private DateTimeOffset? anterior, ultimoMuestreo, inicioAbierto;
        private long? primerSintoma, inicioAlarmaActivo;
        private long activoAnterior, pesoPendiente;
        private int cabeceosAnteriores, alarmaPendiente = -1;
        private bool ojoValidoAnterior, alarmaAnterior, cierreContado;
        public MuestraAtencion? Actual { get; private set; }

        public void Observar(DateTimeOffset instante, FatigaEnVivo lectura, EyeState estado,
            CalidadSesion calidad, SesionConduccion sesion, double umbralCierre)
        {
            long activo = calidad.TiempoActivoMs;
            long intervalo = anterior.HasValue ? (long)(instante - anterior.Value).TotalMilliseconds : 0;
            bool ojoValido = lectura.AperturaIzquierda.HasValue && lectura.AperturaDerecha.HasValue;
            if (!ojoValido || intervalo > 250)
                CerrarRespuesta("Medición ocular interrumpida; recuperación no confirmada");
            if (intervalo is > 0 and <= 250 && ojoValido && ojoValidoAnterior)
                pesoPendiente += intervalo;
            else
                inicioAbierto = null;

            if (lectura.CierreMs >= umbralCierre * 1000 && !cierreContado)
            {
                cierres.Enqueue(instante);
                primerSintoma = Minimo(primerSintoma, Math.Max(0, activo - lectura.CierreMs));
                cierreContado = true;
            }
            if (lectura.CierreMs == 0)
                cierreContado = false;
            if (lectura.Acumulado.Cabeceos > cabeceosAnteriores)
            {
                cabeceos.Enqueue(instante);
                EventoSesion? evento = sesion.Eventos.LastOrDefault(e => e.Tipo == TipoEventoSesion.Cabeceo);
                primerSintoma = Minimo(primerSintoma, Math.Max(0, activo - (long)(instante - (evento?.Inicio ?? instante)).TotalMilliseconds));
            }
            cabeceosAnteriores = lectura.Acumulado.Cabeceos;
            while (cierres.TryPeek(out var cierre) && instante - cierre > TimeSpan.FromSeconds(60)) cierres.Dequeue();
            while (cabeceos.TryPeek(out var cabeza) && instante - cabeza > TimeSpan.FromSeconds(60)) cabeceos.Dequeue();

            bool alarma = estado == EyeState.ALARMA_ACTIVADA;
            if (alarma && !alarmaAnterior)
            {
                CerrarRespuesta("Nueva alarma sin recuperación confirmada");
                respuestas.Add(new RespuestaAlarma(instante, null, null, "Pendiente"));
                alarmaPendiente = respuestas.Count - 1;
                inicioAlarmaActivo = activo;
            }
            if (alarmaPendiente >= 0)
            {
                RespuestaAlarma respuesta = respuestas[alarmaPendiente];
                bool abiertos = lectura.AperturaIzquierda >= 0.55 && lectura.AperturaDerecha >= 0.55;
                if (abiertos && !alarma && respuesta.RespuestaMs is null)
                    respuestas[alarmaPendiente] = respuesta = respuesta with { RespuestaMs = activo - inicioAlarmaActivo };
                bool frontal = lectura.Angulos is { } a && Math.Abs(a.Horizontal) <= 20
                    && Math.Abs(a.Vertical) <= 15 && Math.Abs(a.Lateral) <= 15;
                bool recuperado = abiertos && !alarma && frontal && calidad.Puntaje >= 50
                    && (lectura.ProporcionDistraccion is null or <= 0);
                if (recuperado)
                {
                    inicioAbierto ??= instante;
                    if ((instante - inicioAbierto.Value).TotalMilliseconds >= 2000)
                    {
                        respuestas[alarmaPendiente] = respuesta with
                        { RecuperacionMs = activo - inicioAlarmaActivo, Estado = "Ojos abiertos y cabeza frontal durante 2 s" };
                        alarmaPendiente = -1;
                    }
                }
                else inicioAbierto = null;
            }

            double cobertura = activo > 0 ? (double)lectura.Acumulado.TiempoOcularValidoMs / activo : 0;
            if (ultimoMuestreo is null || (instante - ultimoMuestreo.Value).TotalMilliseconds >= 1000)
            {
                var calculo = AnalisisAtencion.Calcular(ojoValido ? lectura.Perclos : null,
                    cierres.Count, cabeceos.Count, lectura.Angulos.HasValue,
                    lectura.ProporcionDistraccion, calidad.Puntaje, cobertura);
                Actual = new MuestraAtencion(instante, activo, pesoPendiente, calculo.Puntaje,
                    calidad.Puntaje, lectura.ProporcionDistraccion.HasValue, calculo.Factores);
                muestras.Add(Actual);
                pesoPendiente = 0;
                ultimoMuestreo = instante;
            }
            anterior = instante;
            activoAnterior = activo;
            ojoValidoAnterior = ojoValido;
            alarmaAnterior = alarma;
        }

        public void Interrumpir(DateTimeOffset instante)
        {
            if (pesoPendiente > 0 && Actual is { } actual && anterior is { } ultimo)
                muestras.Add(actual with { Instante = ultimo, ActivoMs = activoAnterior, PesoMs = pesoPendiente });
            if (anterior.HasValue)
                muestras.Add(new MuestraAtencion(instante, activoAnterior, 0, null, null, false, Array.Empty<FactorAtencion>()));
            CerrarRespuesta("Observación interrumpida; recuperación no confirmada");
            anterior = ultimoMuestreo = inicioAbierto = null;
            ojoValidoAnterior = alarmaAnterior = cierreContado = false;
            pesoPendiente = 0;
            Actual = null;
        }

        public InformeAtencion Resumen() => new(1, primerSintoma, muestras.ToArray(), respuestas.ToArray());

        private void CerrarRespuesta(string estado)
        {
            if (alarmaPendiente >= 0)
                respuestas[alarmaPendiente] = respuestas[alarmaPendiente] with { Estado = estado };
            alarmaPendiente = -1;
            inicioAlarmaActivo = null;
            inicioAbierto = null;
        }

        private static long Minimo(long? anterior, long actual) => anterior.HasValue ? Math.Min(anterior.Value, actual) : actual;
    }
}
