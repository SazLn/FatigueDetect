using FatigueDetect.Entidades;

namespace FatigueDetect.Presentacion
{
    internal static class AvisosIluminacion
    {
        public static string Mensaje(EstadoIluminacion estado) => estado switch
        {
            EstadoIluminacion.Insuficiente => "Imagen oscura. Añade luz frontal.",
            EstadoIluminacion.Excesiva => "Imagen sobreexpuesta. Reduce la luz directa.",
            _ => "Iluminación adecuada."
        };
    }
}
