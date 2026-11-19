using System;

namespace FatigueDetect.Entidades
{
    /// <summary>
    /// Datos del conductor asociados a una plantilla facial. La plantilla contiene
    /// descriptores numéricos; nunca se guardan fotografías de la cámara.
    /// </summary>
    public class PerfilBiometrico
    {
        public string Cedula { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string TipoCarro { get; set; } = "";
        public string Placa { get; set; } = "";
        public float[] DescriptorFacial { get; set; } = Array.Empty<float>();
        public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;

        public string NombreCompleto => $"{Nombre} {Apellidos}".Trim();
    }
}
