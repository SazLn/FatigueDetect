using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed record FilaComparacion(string Indicador, double? Actual, double? Anterior, string Formato)
    {
        public double? Diferencia => Actual - Anterior;
    }

    public sealed class ComparacionHistorial
    {
        public DateTime Desde { get; }
        public DateTime Hasta { get; }
        public DateTime AnteriorDesde { get; }
        public DateTime AnteriorHasta { get; }
        public IReadOnlyList<FilaComparacion> Filas { get; }

        public ComparacionHistorial(IReadOnlyList<SesionConduccion> sesiones, FiltroHistorial filtro,
            DateTime hoy, int dias = 30, bool usarRango = true)
        {
            Hasta = filtro.Hasta?.Date ?? hoy.Date;
            DateTime inicio = Hasta.AddDays(1 - dias);
            if (usarRango && filtro.Desde is DateTime desde && desde.Date <= Hasta)
                inicio = desde.Date;
            Desde = inicio;
            AnteriorHasta = Desde.AddDays(-1);
            AnteriorDesde = AnteriorHasta.AddDays(-(Hasta - Desde).Days);

            var actual = (filtro with { Desde = Desde, Hasta = Hasta }).Aplicar(sesiones);
            var anterior = (filtro with { Desde = AnteriorDesde, Hasta = AnteriorHasta }).Aplicar(sesiones);
            Filas =
            [
                new("Sesiones", actual.Count, anterior.Count, "0"),
                new("Alarmas", actual.Sum(s => s.CantidadAlarmas), anterior.Sum(s => s.CantidadAlarmas), "0"),
                new("Alarmas por hora de sesión finalizada", TasaAlarmas(actual), TasaAlarmas(anterior), "0.0"),
                new("FPS medios por sesión", Promedio(actual, s => s.Calidad?.Fps),
                    Promedio(anterior, s => s.Calidad?.Fps), "0.0"),
                new("Calidad media (0–100)", Promedio(actual, s => s.Calidad?.Puntaje),
                    Promedio(anterior, s => s.Calidad?.Puntaje), "0.0"),
                new("Sesiones con puntaje de calidad", actual.Count(s => s.Calidad?.Puntaje is not null),
                    anterior.Count(s => s.Calidad?.Puntaje is not null), "0"),
                new("Ojos abiertos observados (%)", AperturaPonderada(actual), AperturaPonderada(anterior), "0.0"),
                new("Cobertura ocular media (%)", Promedio(actual, s => s.CoberturaOcular * 100),
                    Promedio(anterior, s => s.CoberturaOcular * 100), "0.0"),
                new("Sesiones con indicador ocular", actual.Count(s => s.OjosAbiertosObservados is not null),
                    anterior.Count(s => s.OjosAbiertosObservados is not null), "0")
            ];
        }

        public static string Rango(DateTime desde, DateTime hasta) => $"{desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}";

        private static double? Promedio(IEnumerable<SesionConduccion> sesiones,
            Func<SesionConduccion, double?> valor) => sesiones.Select(valor).Average();

        private static double? TasaAlarmas(IEnumerable<SesionConduccion> sesiones)
        {
            var validas = sesiones.Where(s => s.Fin.HasValue && s.Duracion > TimeSpan.Zero).ToArray();
            double horas = validas.Sum(s => s.Duracion.TotalHours);
            return horas > 0 ? validas.Sum(s => s.CantidadAlarmas) / horas : null;
        }

        private static double? AperturaPonderada(IEnumerable<SesionConduccion> sesiones)
        {
            var validas = sesiones.Select(s => s.MetricasFatiga).OfType<MetricasFatiga>()
                .Where(f => f.Perclos.HasValue).ToArray();
            double total = validas.Sum(f => (double)f.TiempoOcularValidoMs);
            return total > 0 ? 100 * Math.Clamp(1 - validas.Sum(f => (double)f.TiempoOcularCerradoMs) / total, 0, 1) : null;
        }
    }
}
