using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed record IndicadorHistorico(int Sesiones, double HorasActivas, int Alarmas,
        double? AlarmasPorHora, double? Atencion, double? Perclos, double? ParpadeosPorMinuto,
        double? Calidad, double? Distraccion);

    public sealed record FilaHistorica(string Nombre, IndicadorHistorico Indicadores);

    public static class AnalisisHistoricoAvanzado
    {
        public static IndicadorHistorico Calcular(IEnumerable<SesionConduccion> origen)
        {
            SesionConduccion[] sesiones = origen.ToArray();
            double horas = sesiones.Sum(s => Math.Max(0, s.Calidad?.TiempoActivoMs ??
                (s.Fin.HasValue ? s.Duracion.TotalMilliseconds : 0))) / 3_600_000.0;
            int alarmas = sesiones.Sum(s => Math.Max(s.CantidadAlarmas,
                s.Eventos.Count(e => e.Tipo == TipoEventoSesion.Alarma)));
            return new(sesiones.Length, horas, alarmas, horas > 0 ? alarmas / horas : null,
                PromedioPonderado(sesiones, s => s.Atencion is { } a
                    ? AnalisisAtencion.Promedio(a.Muestras) : null,
                    s => s.Atencion?.Muestras.Where(m => m.Puntaje.HasValue).Sum(m => m.PesoMs) ?? 0),
                PromedioPonderado(sesiones, s => s.MetricasFatiga?.Perclos,
                    s => s.MetricasFatiga?.TiempoOcularValidoMs ?? 0),
                PromedioPonderado(sesiones, s => s.MetricasFatiga?.ParpadeosPorMinuto,
                    s => s.MetricasFatiga?.TiempoOcularValidoMs ?? 0),
                PromedioPonderado(sesiones, s => s.Calidad?.Puntaje,
                    s => s.Calidad?.TiempoActivoMs ?? 0),
                PromedioPonderado(sesiones, s => s.Postura?.ProporcionDistraida(s.Calidad?.TiempoActivoMs),
                    s => s.Calidad?.TiempoActivoMs ?? 0));
        }

        public static double? Riesgo(SesionConduccion sesion)
        {
            // Un índice orientativo: solo se ordenan sesiones con atención y calidad válidas.
            double? atencion = sesion.Atencion is { } a ? AnalisisAtencion.Promedio(a.Muestras) : null;
            if (atencion is not double valor || sesion.Calidad?.Puntaje is not >= 50)
                return null;
            return Math.Clamp(100 - valor, 0, 100);
        }

        public static IReadOnlyList<FilaHistorica> PorVehiculo(IEnumerable<SesionConduccion> sesiones) =>
            Agrupar(sesiones, s => string.IsNullOrWhiteSpace(s.Vehiculo.Placa)
                ? s.Vehiculo.TipoCarro : s.Vehiculo.Placa.Trim().ToUpperInvariant());

        public static IReadOnlyList<FilaHistorica> PorHora(IEnumerable<SesionConduccion> sesiones) =>
            Agrupar(sesiones, s => $"{s.Inicio.Hour:00}:00–{s.Inicio.Hour:00}:59");

        public static IReadOnlyList<FilaHistorica> PorDiaSemana(IEnumerable<SesionConduccion> sesiones) =>
            Agrupar(sesiones, s => $"{((int)s.Inicio.DayOfWeek + 6) % 7 + 1} · {s.Inicio:dddd}");

        public static IReadOnlyList<FilaHistorica> TendenciaPersonal(IEnumerable<SesionConduccion> sesiones,
            string cedula) => sesiones.Where(s => string.Equals(s.Conductor.Cedula.Trim(), cedula.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Inicio).ThenBy(s => s.Id)
            .Select(s => new FilaHistorica($"{s.Inicio:dd/MM/yyyy HH:mm} · {s.Id.ToString()[..8]}", Calcular([s])))
            .ToArray();

        public static int[,] MapaCalor(IEnumerable<SesionConduccion> sesiones)
        {
            var mapa = new int[7, 24];
            foreach (SesionConduccion s in sesiones)
                mapa[((int)s.Inicio.DayOfWeek + 6) % 7, s.Inicio.Hour]++;
            return mapa;
        }

        private static IReadOnlyList<FilaHistorica> Agrupar(IEnumerable<SesionConduccion> sesiones,
            Func<SesionConduccion, string> clave) => sesiones.GroupBy(clave)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new FilaHistorica(g.Key, Calcular(g))).ToArray();

        private static double? PromedioPonderado(IEnumerable<SesionConduccion> sesiones,
            Func<SesionConduccion, double?> valor, Func<SesionConduccion, long> peso)
        {
            var validas = sesiones.Select(s => (Valor: valor(s), Peso: Math.Max(0, peso(s))))
                .Where(x => x.Valor is double v && double.IsFinite(v) && x.Peso > 0).ToArray();
            double total = validas.Sum(x => (double)x.Peso);
            return total > 0 ? validas.Sum(x => x.Valor!.Value * x.Peso) / total : null;
        }
    }
}
