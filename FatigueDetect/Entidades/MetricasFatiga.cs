namespace FatigueDetect.Entidades
{
    public sealed record MetricasFatiga(int Parpadeos, int CierresProlongados, int Cabeceos,
        long TiempoOcularValidoMs, long TiempoOcularCerradoMs)
    {
        public double? Perclos => TiempoOcularValidoMs >= 2000
            ? (double)TiempoOcularCerradoMs / TiempoOcularValidoMs : null;
    }

    public readonly record struct AngulosCabeza(double Vertical, double Horizontal, double Lateral);

    public readonly record struct MedicionFacial(double EarIzquierdo, double EarDerecho, AngulosCabeza? Angulos);

    public sealed record FatigaEnVivo(double? AperturaIzquierda, double? AperturaDerecha,
        double? Perclos, long CierreMs, AngulosCabeza? Angulos, MetricasFatiga Acumulado);
}
