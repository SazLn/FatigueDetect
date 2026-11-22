using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed class GestorAlertasProgresivas
    {
        private readonly SesionConduccion sesion;
        private readonly Dictionary<NivelAlertaProgresiva, DateTimeOffset> ultimoSonido = new();
        private NivelAlertaProgresiva nivel;
        private DateTimeOffset? inicioRecuperacion;
        private DateTimeOffset? inicioEvento;

        public GestorAlertasProgresivas(SesionConduccion sesion)
        {
            this.sesion = sesion;
        }

        public EstadoAlertaProgresiva Observar(DateTimeOffset instante, EyeState estadoOcular,
            FatigaEnVivo lectura, MuestraAtencion? atencion, CalidadSesion calidad)
        {
            NivelAlertaProgresiva solicitado = EvaluarRiesgo(estadoOcular, lectura, atencion);
            bool recuperacionPendiente = false;
            long? restante = null;
            string explicacion;

            if (solicitado > nivel)
            {
                CambiarNivel(solicitado, instante);
                inicioRecuperacion = null;
                explicacion = Explicar(solicitado, estadoOcular, lectura, atencion);
            }
            else if (solicitado < nivel)
            {
                recuperacionPendiente = true;
                long necesario = TiempoRecuperacion(nivel);
                if (RecuperacionValida(lectura, atencion, calidad))
                {
                    inicioRecuperacion ??= instante;
                    long estable = Math.Max(0, (long)(instante - inicioRecuperacion.Value).TotalMilliseconds);
                    restante = Math.Max(0, necesario - estable);
                    if (estable >= necesario)
                    {
                        NivelAlertaProgresiva siguiente = (NivelAlertaProgresiva)Math.Max((int)solicitado,
                            (int)nivel - 1);
                        CambiarNivel(siguiente, instante);
                        inicioRecuperacion = siguiente > solicitado ? instante : null;
                        recuperacionPendiente = siguiente != NivelAlertaProgresiva.Normal;
                        restante = recuperacionPendiente ? TiempoRecuperacion(siguiente) : null;
                    }
                }
                else
                {
                    inicioRecuperacion = null;
                    restante = necesario;
                }
                explicacion = recuperacionPendiente
                    ? "Mantén los ojos abiertos, la cabeza frontal y una medición estable para reducir la alerta."
                    : "Recuperación confirmada.";
            }
            else
            {
                inicioRecuperacion = null;
                explicacion = Explicar(nivel, estadoOcular, lectura, atencion);
            }

            if (nivel != NivelAlertaProgresiva.Normal && inicioEvento is null)
                inicioEvento = instante;

            bool emitir = DebeEmitirSonido(nivel, instante);
            return new(nivel, emitir, recuperacionPendiente, restante, explicacion);
        }

        public void Interrumpir(DateTimeOffset instante)
        {
            CerrarEvento(instante);
            inicioRecuperacion = null;
        }

        public void Finalizar(DateTimeOffset instante)
        {
            CerrarEvento(instante);
            nivel = NivelAlertaProgresiva.Normal;
            inicioRecuperacion = null;
        }

        public static NivelAlertaProgresiva EvaluarRiesgo(EyeState estadoOcular,
            FatigaEnVivo lectura, MuestraAtencion? atencion)
        {
            double? puntaje = atencion?.Puntaje;
            if (estadoOcular == EyeState.ALARMA_ACTIVADA || puntaje is <= 25)
                return NivelAlertaProgresiva.Critica;
            if (estadoOcular == EyeState.SOMNOLIENTO || puntaje is <= 50 ||
                lectura.ProporcionDistraccion is >= .30)
                return NivelAlertaProgresiva.Alta;
            if (puntaje is <= 75 || lectura.Perclos is >= .15 ||
                lectura.ProporcionDistraccion is >= .15)
                return NivelAlertaProgresiva.Preventiva;
            return NivelAlertaProgresiva.Normal;
        }

        private void CambiarNivel(NivelAlertaProgresiva nuevo, DateTimeOffset instante)
        {
            if (nuevo == nivel)
                return;
            CerrarEvento(instante);
            nivel = nuevo;
            if (nivel != NivelAlertaProgresiva.Normal)
                inicioEvento = instante;
        }

        private void CerrarEvento(DateTimeOffset instante)
        {
            if (inicioEvento is not DateTimeOffset inicio || nivel == NivelAlertaProgresiva.Normal)
            {
                inicioEvento = null;
                return;
            }
            sesion.RegistrarEvento(new EventoSesion(TipoEvento(nivel), inicio,
                instante < inicio ? inicio : instante));
            inicioEvento = null;
        }

        private bool DebeEmitirSonido(NivelAlertaProgresiva actual, DateTimeOffset instante)
        {
            if (actual == NivelAlertaProgresiva.Normal)
                return false;
            TimeSpan espera = actual switch
            {
                NivelAlertaProgresiva.Preventiva => TimeSpan.FromSeconds(30),
                NivelAlertaProgresiva.Alta => TimeSpan.FromSeconds(15),
                _ => TimeSpan.FromSeconds(5)
            };
            if (ultimoSonido.TryGetValue(actual, out DateTimeOffset anterior) && instante - anterior < espera)
                return false;
            ultimoSonido[actual] = instante;
            return true;
        }

        private static bool RecuperacionValida(FatigaEnVivo lectura, MuestraAtencion? atencion,
            CalidadSesion calidad)
        {
            bool ojos = lectura.CierreMs == 0 && lectura.AperturaIzquierda is >= .55
                && lectura.AperturaDerecha is >= .55;
            bool frente = lectura.Angulos is { } angulos && Math.Abs(angulos.Horizontal) <= 20
                && Math.Abs(angulos.Vertical) <= 15 && Math.Abs(angulos.Lateral) <= 15;
            return ojos && frente && calidad.Puntaje is >= 50 && atencion?.Puntaje is >= 70;
        }

        private static long TiempoRecuperacion(NivelAlertaProgresiva actual) => actual switch
        {
            NivelAlertaProgresiva.Critica => 3_000,
            NivelAlertaProgresiva.Alta => 5_000,
            NivelAlertaProgresiva.Preventiva => 8_000,
            _ => 0
        };

        private static TipoEventoSesion TipoEvento(NivelAlertaProgresiva actual) => actual switch
        {
            NivelAlertaProgresiva.Preventiva => TipoEventoSesion.AlertaPreventiva,
            NivelAlertaProgresiva.Alta => TipoEventoSesion.AlertaAlta,
            _ => TipoEventoSesion.AlertaCritica
        };

        private static string Explicar(NivelAlertaProgresiva actual, EyeState estado,
            FatigaEnVivo lectura, MuestraAtencion? atencion)
        {
            if (actual == NivelAlertaProgresiva.Normal)
                return "Sin señales suficientes para elevar la alerta.";
            var causas = new List<string>();
            if (estado == EyeState.ALARMA_ACTIVADA) causas.Add("cierre ocular crítico");
            else if (estado == EyeState.SOMNOLIENTO) causas.Add("cierre ocular prolongado");
            if (atencion?.Puntaje is double puntaje) causas.Add($"atención {puntaje:0}/100");
            if (lectura.Perclos is double perclos) causas.Add($"PERCLOS {perclos:P0}");
            if (lectura.ProporcionDistraccion is double distraccion) causas.Add($"distracción {distraccion:P0}");
            return causas.Count == 0 ? "Riesgo observado." : string.Join(" · ", causas);
        }
    }
}
