using System.Drawing;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public interface IDetectorView
    {
        // Método que toda vista debe implementar para recibir
        // la imagen, el estado y el tiempo desde el controlador
        void UpdateImage(Bitmap? image, EyeState estado, long tiempoMs);

        // Método que toda vista debe implementar para indicar
        // si está pausada o no
        bool IsPaused();
    }
}
