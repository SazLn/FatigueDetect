namespace FatigueDetect.Entidades
{
    public sealed record FactorAtencion(string Nombre, double Descuento);

    public sealed record MuestraAtencion(DateTimeOffset Instante, long ActivoMs, long PesoMs,
        double? Puntaje, double? Calidad, bool DistraccionDisponible, IReadOnlyList<FactorAtencion> Factores);

    public sealed record RespuestaAlarma(DateTimeOffset Inicio, long? RespuestaMs,
        long? RecuperacionMs, string Estado);

    public sealed record InformeAtencion(int Version, long? PrimerSintomaActivoMs,
        IReadOnlyList<MuestraAtencion> Muestras, IReadOnlyList<RespuestaAlarma> Respuestas);
}
