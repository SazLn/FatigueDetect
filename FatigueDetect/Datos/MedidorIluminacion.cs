using FatigueDetect.Entidades;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public static class MedidorIluminacion
    {
        public static EstadoIluminacion Evaluar(Mat imagen)
        {
            if (imagen.Channels() == 1)
                return Clasificar(Cv2.Mean(imagen).Val0);

            using var gris = new Mat();
            Cv2.CvtColor(imagen, gris, ColorConversionCodes.BGR2GRAY);
            return Clasificar(Cv2.Mean(gris).Val0);
        }

        public static EstadoIluminacion Clasificar(double brillo) => brillo switch
        {
            < 38 => EstadoIluminacion.Insuficiente,
            > 220 => EstadoIluminacion.Excesiva,
            _ => EstadoIluminacion.Adecuada
        };
    }
}
