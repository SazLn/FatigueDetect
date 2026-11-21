using FatigueDetect.Entidades;

namespace FatigueDetect.Presentacion
{
    // Adaptador sin WinForms para ejecutar el exportador real también en Linux.
    internal static class PaginaHistorial
    {
        internal static string NombreEvento(TipoEventoSesion tipo) => tipo.ToString();
        internal static string NombreEvento(EventoSesion evento) => evento.CodigoTipo ?? NombreEvento(evento.Tipo);
    }
}
