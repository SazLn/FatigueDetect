using System.Globalization;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed record ResumenPeriodo(string Periodo, int Sesiones, double? PromedioSegundos);
    public sealed record ResumenEvento(TipoEventoSesion Tipo, int Cantidad,
        long? TotalMs, long? MaximoMs, int SinDuracion);

    public sealed class ResumenHistorial(IReadOnlyList<SesionConduccion> sesiones)
    {
        public int TotalSesiones => sesiones.Count;
        public double? PromedioSegundos => Promedio(sesiones);

        public IEnumerable<ResumenPeriodo> PorDia() =>
            Agrupar(s => s.Inicio.Date, fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        public IEnumerable<ResumenPeriodo> PorSemana() =>
            Agrupar(s => s.Inicio.Date.AddDays(-((int)s.Inicio.DayOfWeek + 6) % 7),
                lunes => lunes.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        public IEnumerable<ResumenPeriodo> PorMes() =>
            Agrupar(s => new DateTime(s.Inicio.Year, s.Inicio.Month, 1),
                fecha => fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture));

        public IEnumerable<ResumenPeriodo> PorHora() => sesiones.GroupBy(s => s.Inicio.Hour)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new ResumenPeriodo($"{g.Key:00}:00–{g.Key:00}:59", g.Count(), Promedio(g)));

        private IEnumerable<ResumenPeriodo> Agrupar(Func<SesionConduccion, DateTime> clave,
            Func<DateTime, string> etiqueta) => sesiones.GroupBy(clave).OrderByDescending(g => g.Key)
            .Select(g => new ResumenPeriodo(etiqueta(g.Key), g.Count(), Promedio(g)));

        private static double? Promedio(IEnumerable<SesionConduccion> grupo) =>
            grupo.Where(s => s.Fin.HasValue).Select(s => (double?)s.Duracion.TotalSeconds).Average();

        public static int AlarmasSinHora(SesionConduccion sesion, IReadOnlyList<EventoSesion> eventos) =>
            Math.Max(0, sesion.CantidadAlarmas - eventos.Count(e => e.Tipo == TipoEventoSesion.Alarma));

        public static IEnumerable<ResumenEvento> PorTipo(SesionConduccion sesion, IReadOnlyList<EventoSesion> eventos)
        {
            int alarmasSinHora = AlarmasSinHora(sesion, eventos);
            var grupos = eventos.ToLookup(e => e.Tipo);
            foreach (TipoEventoSesion tipo in Enum.GetValues<TipoEventoSesion>())
            {
                var grupo = grupos[tipo];
                int cantidad = grupo.Count() + (tipo == TipoEventoSesion.Alarma ? alarmasSinHora : 0);
                if (cantidad == 0)
                    continue;

                long[] duraciones = grupo.Where(e => e.DuracionMilisegundos.HasValue)
                    .Select(e => e.DuracionMilisegundos!.Value).ToArray();
                yield return new ResumenEvento(tipo, cantidad,
                    duraciones.Length > 0 ? duraciones.Sum() : null,
                    duraciones.Length > 0 ? duraciones.Max() : null, cantidad - duraciones.Length);
            }
        }
    }
}
