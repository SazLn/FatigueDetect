namespace FatigueDetect.Entidades
{
    /// <summary>Ajustes de la detección (se comparten entre la lógica y la pantalla de configuración).</summary>
    public class Configuracion
    {
        public double SegundosSomnoliento { get; set; } = 1.5;
        public double SegundosAlarma { get; set; } = 3.0;
        public bool SonidoActivado { get; set; } = true;
    }
}
