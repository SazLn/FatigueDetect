using System.Globalization;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public enum FiltroAlarmas { Todas, ConAlarmas, SinAlarmas }
    public enum FiltroCalidad { Todas, Baja, ConMedicion, SinMedicion, ConCaidasFps }

    public sealed record FiltroHistorial(DateTime? Desde, DateTime? Hasta, string Conductor, string Vehiculo,
        FiltroAlarmas Alarmas = FiltroAlarmas.Todas, FiltroCalidad Calidad = FiltroCalidad.Todas)
    {
        public bool Coincide(SesionConduccion sesion) =>
            (!Desde.HasValue || sesion.Inicio.Date >= Desde.Value.Date)
            && (!Hasta.HasValue || sesion.Inicio.Date <= Hasta.Value.Date)
            && Coincide(Conductor, sesion.Conductor.NombreCompleto, sesion.Conductor.Cedula)
            && Coincide(Vehiculo, sesion.Vehiculo.Placa, sesion.Vehiculo.TipoCarro)
            && (Alarmas == FiltroAlarmas.Todas || (sesion.CantidadAlarmas > 0) == (Alarmas == FiltroAlarmas.ConAlarmas))
            && (Calidad switch
            {
                FiltroCalidad.Baja => sesion.Calidad?.Puntaje < CalidadSesion.UmbralCalidadBaja,
                FiltroCalidad.ConMedicion => sesion.Calidad?.Puntaje is not null,
                FiltroCalidad.SinMedicion => sesion.Calidad?.Puntaje is null,
                FiltroCalidad.ConCaidasFps => sesion.Calidad?.CaidasFps > 0,
                _ => true
            });

        public List<SesionConduccion> Aplicar(IEnumerable<SesionConduccion> sesiones) =>
            sesiones.Where(Coincide).ToList();

        private static bool Coincide(string filtro, params string[] valores)
        {
            filtro = filtro.Trim();
            return filtro.Length == 0 || valores.Any(valor => CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(valor, filtro, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
        }
    }
}
