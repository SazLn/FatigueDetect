using System;

namespace FatigueDetect.Entidades
{
    public sealed record CalidadSesion(long Fotogramas, long Analizables, long TiempoActivoMs,
        long TiempoRostroValidoMs, long FallosCamara, long? CaidasFps = null,
        long? TiempoFpsBajoMs = null, long? TiempoEvaluadoFpsMs = null,
        double? BrilloPromedio = null, double? ContrastePromedio = null,
        double? NitidezPromedio = null, double? PosicionFacialPromedio = null,
        long MuestrasImagen = 0, long MuestrasPosicion = 0)
    {
        public double? Fps => TiempoActivoMs > 0 ? Fotogramas * 1000.0 / TiempoActivoMs : null;
        public double? ProporcionAnalizable => Fotogramas > 0 ? (double)Analizables / Fotogramas : null;
        public const int UmbralFps = 12;
        public const int UmbralCalidadBaja = 70;

        public double? CoberturaRostro => TiempoActivoMs > 0
            ? Math.Clamp((double)TiempoRostroValidoMs / TiempoActivoMs, 0, 1) : null;

        public double? EstabilidadFps => TiempoEvaluadoFpsMs is > 0
            ? 1 - Math.Clamp((double)TiempoFpsBajoMs.GetValueOrDefault() / TiempoEvaluadoFpsMs.Value, 0, 1)
            : null;

        public double? PuntajeCamaraEntorno => BrilloPromedio is double brillo
            && ContrastePromedio is double contraste && NitidezPromedio is double nitidez
            ? Math.Clamp(brillo * 0.35 + contraste * 0.25 + nitidez * 0.40, 0, 100) : null;

        public double? PuntajeColocacion => PosicionFacialPromedio is double posicion
            ? Math.Clamp(posicion, 0, 100) : null;

        public double? Puntaje
        {
            get
            {
                if (ProporcionAnalizable is not double analizables || CoberturaRostro is not double rostro)
                    return null;

                bool medicionAmpliada = BrilloPromedio.HasValue || ContrastePromedio.HasValue
                    || NitidezPromedio.HasValue || PosicionFacialPromedio.HasValue;
                if (!medicionAmpliada)
                    return EstabilidadFps is double estabilidad
                        ? 100 * (0.45 * analizables + 0.35 * rostro + 0.20 * estabilidad)
                        : null;

                double suma = analizables * 35 + rostro * 25;
                double peso = 60;
                Agregar(EstabilidadFps is double fps ? fps : null, 15, ref suma, ref peso);
                Agregar(BrilloPromedio / 100, 10, ref suma, ref peso);
                Agregar(ContrastePromedio / 100, 5, ref suma, ref peso);
                Agregar(NitidezPromedio / 100, 5, ref suma, ref peso);
                Agregar(PosicionFacialPromedio / 100, 5, ref suma, ref peso);
                return peso > 0 ? Math.Clamp(suma / peso * 100, 0, 100) : null;
            }
        }

        public string ExplicarReducciones()
        {
            var factores = new System.Collections.Generic.List<string>();
            if (BrilloPromedio is < 70) factores.Add("brillo");
            if (ContrastePromedio is < 70) factores.Add("contraste");
            if (NitidezPromedio is < 70) factores.Add("nitidez");
            if (PosicionFacialPromedio is < 70) factores.Add("posición facial");
            if (EstabilidadFps is < 0.8) factores.Add("estabilidad de FPS");
            if (CoberturaRostro is < 0.7) factores.Add("cobertura facial");
            if (ProporcionAnalizable is < 0.7) factores.Add("fotogramas analizables");
            return factores.Count == 0 ? "Sin factores de calidad destacados."
                : "Factores que redujeron la calidad: " + string.Join(", ", factores) + ".";
        }

        private static void Agregar(double? valor, double pesoFactor, ref double suma, ref double peso)
        {
            if (valor is not double disponible)
                return;
            suma += Math.Clamp(disponible, 0, 1) * pesoFactor;
            peso += pesoFactor;
        }

        public TimeSpan TiempoRostroValido => TimeSpan.FromMilliseconds(TiempoRostroValidoMs);
    }
}
