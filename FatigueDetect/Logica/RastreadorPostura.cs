using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    internal sealed class RastreadorPostura(SesionConduccion sesion)
    {
        private readonly Episodio giro = new();
        private readonly Episodio distraccion = new();
        private readonly Episodio postura = new();
        private readonly Episodio encuadre = new();
        private readonly Episodio bostezo = new();
        private readonly Queue<(DateTimeOffset Fin, long Duracion, bool Fuera)> recientes = new();
        private long observadoRecienteMs, fueraRecienteMs;
        private DateTimeOffset? anterior;

        public double? DistraccionReciente => poseAnterior && observadoRecienteMs >= 750
            ? (double)fueraRecienteMs / observadoRecienteMs : null;
        private double? alturaNeutra, distanciaAnterior;
        private bool poseAnterior, bocaAnterior;
        private long poseMs, bocaMs, fueraMs, maximoMs, distanciaMs;
        private double sumaDistancia;
        private int bostezos;

        public MetricasPostura Resumen => new(poseMs, fueraMs, maximoMs, bocaMs, bostezos,
            distanciaMs > 0 ? sumaDistancia / distanciaMs : null);

        public PosturaEnVivo Observar(DateTimeOffset instante, MedicionFacial? cara,
            AngulosCabeza? angulos, bool rostroParcial)
        {
            if (anterior is { } ultima)
            {
                long lapso = (long)(instante - ultima).TotalMilliseconds;
                if (lapso is < 0 or > 250)
                    Interrumpir();
                else
                {
                    if (poseAnterior && angulos.HasValue && lapso > 0)
                    {
                        poseMs += lapso;
                        bool fuera = distraccion.Tipo.HasValue && distraccion.Confirmado;
                        recientes.Enqueue((instante, lapso, fuera));
                        observadoRecienteMs += lapso;
                        if (fuera) fueraRecienteMs += lapso;
                        if (distraccion.Tipo.HasValue && !distraccion.Confirmado
                            && (instante - distraccion.Inicio).TotalMilliseconds >= 750)
                            ConfirmarDistraccionReciente();
                    }
                    if (bocaAnterior && cara?.AperturaBoca is not null) bocaMs += lapso;
                    if (distanciaAnterior is double distancia && cara?.DistanciaCm is not null)
                    {
                        sumaDistancia += distancia * lapso;
                        distanciaMs += lapso;
                    }
                }
            }

            while (recientes.TryPeek(out var muestra) && (instante - muestra.Fin).TotalSeconds >= 60)
            {
                recientes.Dequeue();
                observadoRecienteMs -= muestra.Duracion;
                if (muestra.Fuera) fueraRecienteMs -= muestra.Duracion;
            }

            DireccionCabeza? direccion = angulos is { } pose ? Orientacion(pose) : null;
            if (direccion == DireccionCabeza.Frente)
                alturaNeutra ??= cara?.AlturaRostro;
            bool encorvada = angulos is { Vertical: >= 14 } && alturaNeutra is double baseAltura
                && cara?.AlturaRostro - baseAltura >= 0.13;

            TipoEventoSesion? tipoGiro = direccion switch
            {
                DireccionCabeza.Izquierda => TipoEventoSesion.GiroIzquierda,
                DireccionCabeza.Derecha => TipoEventoSesion.GiroDerecha,
                DireccionCabeza.Arriba => TipoEventoSesion.CabezaArriba,
                DireccionCabeza.Abajo => TipoEventoSesion.CabezaAbajo,
                _ => null
            };
            Registrar(giro, tipoGiro, instante, 400, direccion.HasValue);
            Registrar(distraccion, tipoGiro.HasValue ? TipoEventoSesion.FueraZonaFrontal : null,
                instante, 750, direccion.HasValue);
            Registrar(postura, encorvada ? TipoEventoSesion.PosiblePosturaEncorvada : null, instante, 2000,
                direccion.HasValue && cara?.AlturaRostro is not null);
            Registrar(encuadre, rostroParcial ? TipoEventoSesion.RostroParcial : null, instante, 500, true);

            double? abertura = cara?.AperturaBoca;
            bool bocaAbierta = abertura >= (bostezo.Tipo.HasValue ? 0.32 : 0.48);
            Registrar(bostezo, bocaAbierta ? TipoEventoSesion.PosibleBostezo : null, instante, 800,
                abertura.HasValue, completar: abertura.HasValue);

            anterior = instante;
            poseAnterior = angulos.HasValue;
            bocaAnterior = abertura.HasValue;
            distanciaAnterior = cara?.DistanciaCm;
            return new(direccion, encorvada, rostroParcial, distanciaAnterior, bostezos);
        }

        public void Interrumpir()
        {
            if (anterior is not { } ultimo)
                return;

            Registrar(giro, null, ultimo, 400, false);
            Registrar(distraccion, null, ultimo, 750, false);
            Registrar(postura, null, ultimo, 2000, false);
            Registrar(encuadre, null, ultimo, 500, false);
            Registrar(bostezo, null, ultimo, 800, false, completar: false);
            anterior = null;
            poseAnterior = bocaAnterior = false;
            distanciaAnterior = null;
            recientes.Clear();
            observadoRecienteMs = fueraRecienteMs = 0;
        }

        private DireccionCabeza Orientacion(AngulosCabeza pose)
        {
            double horizontal = pose.Horizontal / (giro.Tipo is TipoEventoSesion.GiroIzquierda or TipoEventoSesion.GiroDerecha ? 16 : 22);
            double vertical = pose.Vertical / (giro.Tipo is TipoEventoSesion.CabezaArriba or TipoEventoSesion.CabezaAbajo ? 13 : 18);
            // La nariz apunta a -Z. Un giro positivo se ve a la izquierda en la imagen sin espejo.
            if (Math.Abs(horizontal) >= 1 && Math.Abs(horizontal) >= Math.Abs(vertical))
                return horizontal > 0 ? DireccionCabeza.Derecha : DireccionCabeza.Izquierda;
            if (Math.Abs(vertical) >= 1)
                return vertical > 0 ? DireccionCabeza.Abajo : DireccionCabeza.Arriba;
            return DireccionCabeza.Frente;
        }

        private void ConfirmarDistraccionReciente()
        {
            distraccion.Confirmado = true;
            var actualizadas = recientes.Select(muestra =>
                (muestra.Fin, muestra.Duracion,
                    Fuera: muestra.Fuera || muestra.Fin > distraccion.Inicio)).ToArray();
            recientes.Clear();
            fueraRecienteMs = 0;
            foreach (var muestra in actualizadas)
            {
                recientes.Enqueue(muestra);
                if (muestra.Fuera)
                    fueraRecienteMs += muestra.Duracion;
            }
        }

        private void Registrar(Episodio episodio, TipoEventoSesion? tipo, DateTimeOffset instante,
            int minimoMs, bool medible, bool completar = true)
        {
            if (episodio.Tipo != tipo && episodio.Tipo is { } anteriorTipo)
            {
                DateTimeOffset fin = medible ? instante : anterior ?? instante;
                long duracion = (long)(fin - episodio.Inicio).TotalMilliseconds;
                if (completar && duracion >= minimoMs &&
                    (anteriorTipo != TipoEventoSesion.PosibleBostezo || duracion <= 8000))
                {
                    sesion.RegistrarEvento(new(anteriorTipo, episodio.Inicio, fin));
                    if (anteriorTipo == TipoEventoSesion.FueraZonaFrontal)
                    {
                        fueraMs += duracion;
                        maximoMs = Math.Max(maximoMs, duracion);
                    }
                    if (anteriorTipo == TipoEventoSesion.PosibleBostezo)
                        bostezos++;
                }
            }

            if (episodio.Tipo != tipo)
            {
                episodio.Tipo = tipo;
                episodio.Inicio = instante;
                episodio.Confirmado = false;
            }
        }

        private sealed class Episodio
        {
            public TipoEventoSesion? Tipo { get; set; }
            public DateTimeOffset Inicio { get; set; }
            public bool Confirmado { get; set; }
        }
    }
}
