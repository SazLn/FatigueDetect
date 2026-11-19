using System;

namespace FatigueDetect.Entidades
{
    public sealed record CalidadSesion(long Fotogramas, long Analizables, long TiempoActivoMs,
        long TiempoRostroValidoMs, long FallosCamara)
    {
        public double? Fps => TiempoActivoMs > 0 ? Fotogramas * 1000.0 / TiempoActivoMs : null;
        public double? ProporcionAnalizable => Fotogramas > 0 ? (double)Analizables / Fotogramas : null;
        public TimeSpan TiempoRostroValido => TimeSpan.FromMilliseconds(TiempoRostroValidoMs);
    }
}
