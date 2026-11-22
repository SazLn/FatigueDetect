namespace FatigueDetect.Entidades
{
    /// <summary>
    /// Línea base experimental de un conductor. Se guarda separada del historial y solo se
    /// actualiza con sesiones de referencia suficientemente largas y confiables.
    /// </summary>
    public sealed record PerfilPersonalConductor(int Version, string Cedula, DateTimeOffset Actualizado,
        int SesionesValidas, long TiempoOcularValidoMs, long TiempoOcularCerradoMs, long Parpadeos,
        double MediaParpadeosSesion, double M2ParpadeosSesion,
        double MediaPerclosSesion, double M2PerclosSesion,
        int SesionesAtencion, double MediaAtencion, double M2Atencion,
        string CamaraId = "", string VehiculoClave = "", CalibracionGuiada? Calibracion = null)
    {
        public bool Calibrado => SesionesValidas >= 3 && TiempoOcularValidoMs >= 180_000;

        public double? ParpadeosPorMinuto => TiempoOcularValidoMs > 0
            ? Parpadeos * 60_000.0 / TiempoOcularValidoMs : null;

        public double? Perclos => TiempoOcularValidoMs > 0
            ? (double)TiempoOcularCerradoMs / TiempoOcularValidoMs : null;

        public double DesviacionParpadeos => SesionesValidas > 1
            ? Math.Sqrt(M2ParpadeosSesion / (SesionesValidas - 1)) : 0;

        public double DesviacionPerclos => SesionesValidas > 1
            ? Math.Sqrt(M2PerclosSesion / (SesionesValidas - 1)) : 0;

        public double DesviacionAtencion => SesionesAtencion > 1
            ? Math.Sqrt(M2Atencion / (SesionesAtencion - 1)) : 0;
    }

    /// <summary>Resultado guardado con la sesión para explicar su comparación personal.</summary>
    public sealed record ComparacionPersonal(int Version, int SesionesBaseAntes, int SesionesBaseDespues,
        bool PerfilCalibradoAntes, bool PerfilCalibradoDespues,
        double? ParpadeosActual, double? ParpadeosBase, double? DiferenciaParpadeos,
        double? PerclosActual, double? PerclosBase, double? DiferenciaPerclos,
        double? AtencionActual, double? AtencionBase, double? DiferenciaAtencion,
        double UmbralParpadeos, double UmbralPerclos, double UmbralAtencion,
        bool CambioParpadeosAnormal, bool PerclosAnormal, bool AtencionAnormal,
        bool IncorporadaAlPerfil, string Estado,
        string CamaraId = "", string VehiculoClave = "",
        double? UmbralSomnoliento = null, double? UmbralAlarma = null)
    {
        public bool CambioAnormal => CambioParpadeosAnormal || PerclosAnormal || AtencionAnormal;
    }
}
