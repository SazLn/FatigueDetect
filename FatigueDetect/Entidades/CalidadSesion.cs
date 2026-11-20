using System;

namespace FatigueDetect.Entidades
{
    public sealed record CalidadSesion(long Fotogramas, long Analizables, long TiempoActivoMs,
        long TiempoRostroValidoMs, long FallosCamara, long? CaidasFps = null,
        long? TiempoFpsBajoMs = null, long? TiempoEvaluadoFpsMs = null)
    {
        public double? Fps => TiempoActivoMs > 0 ? Fotogramas * 1000.0 / TiempoActivoMs : null;
        public double? ProporcionAnalizable => Fotogramas > 0 ? (double)Analizables / Fotogramas : null;
        public const int UmbralFps = 12;
        public const int UmbralCalidadBaja = 70;

        public double? CoberturaRostro => TiempoActivoMs > 0
            ? Math.Clamp((double)TiempoRostroValidoMs / TiempoActivoMs, 0, 1) : null;

        public double? Puntaje => TiempoEvaluadoFpsMs is > 0 && CoberturaRostro is double rostro
            ? 100 * (0.45 * ProporcionAnalizable.GetValueOrDefault() + 0.35 * rostro
                + 0.20 * (1 - Math.Clamp((double)TiempoFpsBajoMs.GetValueOrDefault() / TiempoEvaluadoFpsMs.Value, 0, 1)))
            : null;

        public TimeSpan TiempoRostroValido => TimeSpan.FromMilliseconds(TiempoRostroValidoMs);
    }
}
