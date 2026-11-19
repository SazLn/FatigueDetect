using FatigueDetect.Entidades;

namespace FatigueDetect.Presentacion
{
    internal static class AvisosIluminacion
    {
        public static string Mensaje(TipoEventoSesion problema) => problema switch
        {
            TipoEventoSesion.CamaraTapada => "Posible cámara tapada. Revisa el lente.",
            TipoEventoSesion.CamaraDesenfocada => "Imagen desenfocada. Revisa el enfoque y limpia el lente.",
            TipoEventoSesion.CamaraCongelada => "La imagen está congelada. Revisa la conexión.",
            _ => ""
        };

        public static string Mensaje(EstadoIluminacion estado) => estado switch
        {
            EstadoIluminacion.Insuficiente => "Imagen oscura. Añade luz frontal.",
            EstadoIluminacion.Excesiva => "Imagen sobreexpuesta. Reduce la luz directa.",
            _ => "Iluminación adecuada."
        };
    }
}
