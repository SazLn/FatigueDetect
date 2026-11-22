namespace FatigueDetect.Entidades
{
    public enum DireccionCabeza { Frente, Izquierda, Derecha, Arriba, Abajo }

    public sealed record MetricasPostura(long TiempoPoseMs, long TiempoFueraFrenteMs,
        long DistraccionMaximaMs, long TiempoBocaMs, int Bostezos, double? DistanciaMediaCm)
    {
        public double? ProporcionDistraida(long? tiempoActivoMs) =>
            TiempoPoseMs > 0 && tiempoActivoMs is > 0 ? (double)TiempoFueraFrenteMs / tiempoActivoMs.Value : null;

        public double? BostezosPorHora => TiempoBocaMs > 0 ? Bostezos * 3_600_000.0 / TiempoBocaMs : null;
    }

    public readonly record struct PosturaEnVivo(DireccionCabeza? Orientacion, bool Encorvada,
        bool FueraEncuadre, double? DistanciaCm, int Bostezos);
}
