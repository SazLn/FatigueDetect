using System.Globalization;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed record FiltroHistorial(DateTime? Desde, DateTime? Hasta, string Conductor, string Vehiculo)
    {
        public bool Coincide(SesionConduccion sesion) =>
            (!Desde.HasValue || sesion.Inicio.Date >= Desde.Value.Date)
            && (!Hasta.HasValue || sesion.Inicio.Date <= Hasta.Value.Date)
            && Coincide(Conductor, sesion.Conductor.NombreCompleto, sesion.Conductor.Cedula)
            && Coincide(Vehiculo, sesion.Vehiculo.Placa, sesion.Vehiculo.TipoCarro);

        private static bool Coincide(string filtro, params string[] valores)
        {
            filtro = filtro.Trim();
            return filtro.Length == 0 || valores.Any(valor => CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(valor, filtro, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
        }
    }
}
