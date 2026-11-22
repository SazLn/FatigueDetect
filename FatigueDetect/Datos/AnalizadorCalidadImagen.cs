using System;
using FatigueDetect.Entidades;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public sealed class AnalizadorCalidadImagen : IDisposable
    {
        private readonly Mat gris = new();
        private readonly Mat muestra = new();
        private readonly Mat laplaciano = new();

        public EvaluacionCalidadImagen Evaluar(Mat imagen, Rect? rostro)
        {
            if (imagen.Channels() == 1)
                imagen.CopyTo(gris);
            else
                Cv2.CvtColor(imagen, gris, ColorConversionCodes.BGR2GRAY);
            Cv2.Resize(gris, muestra, new Size(320, 180), interpolation: InterpolationFlags.Area);
            Cv2.MeanStdDev(muestra, out Scalar media, out Scalar desviacion);
            Cv2.Laplacian(muestra, laplaciano, MatType.CV_32F);
            Cv2.MeanStdDev(laplaciano, out _, out Scalar bordes);

            double brillo = PuntuarRango(media.Val0, 65, 190, 20, 240);
            double contraste = PuntuarMinimo(desviacion.Val0, 8, 35);
            double nitidez = PuntuarMinimo(bordes.Val0 * bordes.Val0, 8, 80);
            double? posicion = rostro is Rect cara ? PuntuarPosicion(cara, imagen.Width, imagen.Height) : null;
            return new(brillo, contraste, nitidez, posicion);
        }

        private static double PuntuarRango(double valor, double idealMin, double idealMax,
            double limiteMin, double limiteMax)
        {
            if (valor >= idealMin && valor <= idealMax) return 100;
            if (valor < idealMin)
                return 100 * Math.Clamp((valor - limiteMin) / (idealMin - limiteMin), 0, 1);
            return 100 * Math.Clamp((limiteMax - valor) / (limiteMax - idealMax), 0, 1);
        }

        private static double PuntuarMinimo(double valor, double limite, double ideal) =>
            100 * Math.Clamp((valor - limite) / (ideal - limite), 0, 1);

        private static double PuntuarPosicion(Rect cara, int ancho, int alto)
        {
            if (ancho <= 0 || alto <= 0 || cara.Width <= 0 || cara.Height <= 0)
                return 0;
            double centroX = (cara.X + cara.Width / 2.0) / ancho;
            double centroY = (cara.Y + cara.Height / 2.0) / alto;
            double desvio = Math.Sqrt(Math.Pow((centroX - 0.5) / 0.42, 2) + Math.Pow((centroY - 0.5) / 0.42, 2));
            double centro = 100 * Math.Clamp(1 - desvio, 0, 1);
            double proporcion = (double)cara.Width / ancho;
            double escala = proporcion switch
            {
                < 0.12 => 0,
                < 0.22 => (proporcion - 0.12) / 0.10 * 100,
                <= 0.58 => 100,
                < 0.78 => (0.78 - proporcion) / 0.20 * 100,
                _ => 0
            };
            double margen = Math.Min(Math.Min(cara.X, cara.Y), Math.Min(ancho - cara.Right, alto - cara.Bottom));
            double borde = 100 * Math.Clamp(margen / (Math.Min(ancho, alto) * 0.04), 0, 1);
            return Math.Clamp(centro * 0.50 + escala * 0.30 + borde * 0.20, 0, 100);
        }

        public void Dispose()
        {
            gris.Dispose();
            muestra.Dispose();
            laplaciano.Dispose();
        }
    }
}
