namespace FatigueDetect.Entidades
{
    public sealed record MetricasFatiga(int Parpadeos, int CierresProlongados, int Cabeceos,
        long TiempoOcularValidoMs, long TiempoOcularCerradoMs,
        int VersionMetricasOculares = 0, int ParpadeosCortos = 0, int ParpadeosProlongados = 0,
        int CierresAnomalos = 0, long DuracionParpadeosTotalMs = 0,
        long? DuracionParpadeoMinMs = null, long? DuracionParpadeoMaxMs = null,
        long CierreMasLargoMs = 0, long MicrosuenoMasLargoMs = 0,
        double? DiferenciaAperturaPromedio = null)
    {
        public IReadOnlyList<MuestraPerclos> EvolucionPerclos { get; init; } = Array.Empty<MuestraPerclos>();

        public double? Perclos => TiempoOcularValidoMs >= 2000
            ? (double)TiempoOcularCerradoMs / TiempoOcularValidoMs : null;

        public int Microsuenos => CierresProlongados;

        public double? ParpadeosPorMinuto => TiempoOcularValidoMs >= 2000
            ? Parpadeos * 60_000.0 / TiempoOcularValidoMs : null;

        public double? DuracionParpadeoPromedioMs => Parpadeos > 0
            ? (double)DuracionParpadeosTotalMs / Parpadeos : null;

        public double? CambioPerclos => EvolucionPerclos.Count >= 2
            ? EvolucionPerclos[^1].Valor - EvolucionPerclos[0].Valor : null;
    }

    public readonly record struct MuestraPerclos(DateTimeOffset Instante, double Valor);

    public readonly record struct AngulosCabeza(double Vertical, double Horizontal, double Lateral);

    public readonly record struct MedicionFacial(double EarIzquierdo, double EarDerecho, AngulosCabeza? Angulos);

    public sealed record FatigaEnVivo(double? AperturaIzquierda, double? AperturaDerecha,
        double? DiferenciaApertura, double? Perclos, long CierreMs,
        AngulosCabeza? Angulos, MetricasFatiga Acumulado)
    {
        // Contrato para el detector de distracción: null = no medido, 0..1 = proporción reciente.
        public double? ProporcionDistraccion { get; init; }
    }
}
