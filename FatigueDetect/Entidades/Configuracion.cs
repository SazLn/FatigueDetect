namespace FatigueDetect.Entidades
{
    /// <summary>Ajustes de la detección (se comparten entre la lógica y la pantalla de configuración).</summary>
    public class Configuracion
    {
        private volatile AjustesDeteccion deteccion = AjustesDeteccion.Predeterminados;

        public AjustesDeteccion Deteccion
        {
            get => deteccion;
            set => deteccion = value;
        }

        public string? CamaraId { get; set; }
    }
}
