using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public static class AnalisisAtencion
    {
        // Fórmula experimental v1. Los factores disponibles se normalizan a 100 puntos.
        // La calidad limita la disponibilidad, no se resta como conducta del conductor.
        public static (double? Puntaje, IReadOnlyList<FactorAtencion> Factores) Calcular(
            double? perclos, int cierres, int cabeceos, bool cabezaDisponible,
            double? distraccion, double? calidad, double cobertura)
        {
            if (perclos is not double ocular || !double.IsFinite(ocular)
                || calidad is not >= 50 || cobertura < 0.5)
                return (null, Array.Empty<FactorAtencion>());
            var factores = new List<(string Nombre, double Peso, double Riesgo)>
            {
                ("PERCLOS", 40, Math.Clamp(ocular / 0.4, 0, 1)),
                ("Cierres prolongados recientes", 25, Math.Clamp(cierres / 3.0, 0, 1))
            };
            if (cabezaDisponible)
                factores.Add(("Cabeceos recientes", 20, Math.Clamp(cabeceos / 3.0, 0, 1)));
            if (distraccion is double d && double.IsFinite(d))
                factores.Add(("Distracción reciente", 15, Math.Clamp(d / 0.3, 0, 1)));
            double total = factores.Sum(f => f.Peso);
            var descuentos = factores.Select(f => new FactorAtencion(f.Nombre, 100 * f.Peso * f.Riesgo / total)).ToArray();
            return (Math.Clamp(100 - descuentos.Sum(f => f.Descuento), 0, 100), descuentos);
        }

        public static double? Promedio(IEnumerable<MuestraAtencion> muestras)
        {
            var validas = muestras.Where(m => m.Puntaje.HasValue && m.PesoMs > 0).ToArray();
            double peso = validas.Sum(m => (double)m.PesoMs);
            return peso > 0 ? validas.Sum(m => m.Puntaje!.Value * m.PesoMs) / peso : null;
        }

        public static (int Minuto, double Puntaje)? PeorMinuto(InformeAtencion informe)
        {
            var minutos = informe.Muestras.Where(m => m.Puntaje.HasValue && m.PesoMs > 0)
                .GroupBy(m => Math.Max(0, m.ActivoMs - 1) / 60_000)
                .Where(g => g.Sum(m => m.PesoMs) >= 2000)
                .Select(g => (Minuto: (int)g.Key + 1, Puntaje: Promedio(g)!.Value))
                .OrderBy(g => g.Puntaje).ThenBy(g => g.Minuto).ToArray();
            return minutos.Length > 0 ? minutos[0] : null;
        }

        public static string Tendencia(InformeAtencion informe)
        {
            var validas = informe.Muestras.Where(m => m.Puntaje.HasValue && m.PesoMs > 0).ToArray();
            if (validas.Length < 6 || validas.Sum(m => m.PesoMs) < 30_000)
                return "Sin datos suficientes (requiere 30 s válidos)";
            int tercio = validas.Length / 3;
            double cambio = Promedio(validas.TakeLast(tercio))!.Value - Promedio(validas.Take(tercio))!.Value;
            return $"{(cambio > 5 ? "Mejora" : cambio < -5 ? "Empeora" : "Estable")} ({cambio:+0.0;-0.0;0.0} puntos entre tercios inicial y final)";
        }

        public static string Resumir(InformeAtencion? informe)
        {
            if (informe is null)
                return "Atención: sin medición en esta sesión anterior.";
            double? promedio = Promedio(informe.Muestras);
            var peor = PeorMinuto(informe);
            var factores = informe.Muestras.Where(m => m.Puntaje.HasValue && m.PesoMs > 0)
                .SelectMany(m => m.Factores.Select(f => (f.Nombre, Valor: f.Descuento * m.PesoMs, m.PesoMs)))
                .GroupBy(f => f.Nombre).Select(g => new { Nombre = g.Key, Valor = g.Sum(f => f.Valor) / g.Sum(f => (double)f.PesoMs) })
                .Where(f => f.Valor > 0.05).OrderByDescending(f => f.Valor).ToArray();
            return $"Atención experimental: {promedio?.ToString("0.0") ?? "sin medición"}/100.\n" +
                $"Primer síntoma desde el inicio activo: {(informe.PrimerSintomaActivoMs is long ms ? $"{ms / 1000.0:0.0} s" : "no observado")}.\n" +
                (peor is { } p ? $"Minuto activo de mayor fatiga observada: {p.Minuto} ({p.Puntaje:0.0}/100).\n" : "Minuto de mayor fatiga: sin datos suficientes.\n") +
                $"Tendencia: {Tendencia(informe)}.\n" +
                (factores.Length == 0 ? "Sin descuentos observados o sin medición suficiente.\n" : "Descuentos medios: " + string.Join("; ", factores.Select(f => $"{f.Nombre}: −{f.Valor:0.0} puntos")) + ".\n") +
                $"Tiempo con puntaje válido: {informe.Muestras.Where(m => m.Puntaje.HasValue).Sum(m => m.PesoMs) / 1000.0:0.0} s. " +
                (informe.Muestras.Any(m => m.DistraccionDisponible) ? "Incluye muestras con distracción medida.\n" : "Distracción: pendiente de detector.\n") +
                $"Alarmas con respuesta ocular: {informe.Respuestas.Count(r => r.RespuestaMs.HasValue)}/{informe.Respuestas.Count}. " +
                $"Recuperaciones confirmadas: {informe.Respuestas.Count(r => r.RecuperacionMs.HasValue)}. " +
                "La respuesta observada no demuestra que la alarma la haya causado.";
        }
    }
}
