namespace FatigueDetect.Entidades
{
    public sealed record CalibracionGuiada(int Version, DateTimeOffset Instante,
        string CamaraId, string VehiculoClave, double EarIzquierdoAbierto,
        double EarDerechoAbierto, AngulosCabeza PosturaFrontal,
        int MuestrasValidas, int ParpadeosConfirmados);

    public sealed record EstadoCalibracion(string Instruccion, double Progreso,
        int MuestrasValidas, int ParpadeosConfirmados, bool Completa);

    public sealed record UmbralesPersonalizados(double SegundosSomnoliento,
        double SegundosAlarma, bool Personalizados, string Explicacion);
}
