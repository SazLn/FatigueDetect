using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    /// <summary>
    /// Genera un descriptor LBPH local sin servicios externos ni fotografías guardadas.
    /// Está pensado como prototipo local y no reemplaza un sistema biométrico certificado.
    /// </summary>
    public class ExtractorDescriptorFacial
    {
        private const int Lado = 128;
        private const int CeldasPorLado = 8;
        private const int NivelesLbp = 256;

        public bool IntentarExtraer(Mat frame, Rect rostro, out float[] descriptor, out string mensaje)
        {
            descriptor = Array.Empty<float>();
            mensaje = "";

            if (frame.Empty() || rostro.Width < 100 || rostro.Height < 100)
            {
                mensaje = "Acércate un poco a la cámara.";
                return false;
            }

            Rect limite = new Rect(0, 0, frame.Width, frame.Height);
            Rect recorte = rostro.Intersect(limite);
            if (recorte.Width < 100 || recorte.Height < 100)
            {
                mensaje = "Mantén todo el rostro dentro del recuadro.";
                return false;
            }

            using var region = new Mat(frame, recorte);
            using var gris = new Mat();
            using var normalizada = new Mat();
            Cv2.CvtColor(region, gris, ColorConversionCodes.BGR2GRAY);
            Cv2.Resize(gris, normalizada, new Size(Lado, Lado));

            Cv2.MeanStdDev(normalizada, out Scalar media, out Scalar desviacion);
            if (media.Val0 < 38)
            {
                mensaje = "Hay poca luz. Ilumina tu rostro de frente.";
                return false;
            }
            if (media.Val0 > 220)
            {
                mensaje = "La imagen está sobreexpuesta. Reduce la luz directa.";
                return false;
            }
            if (desviacion.Val0 < 18)
            {
                mensaje = "La imagen tiene poco contraste. Ajusta la iluminación.";
                return false;
            }

            using var laplaciano = new Mat();
            Cv2.Laplacian(normalizada, laplaciano, MatType.CV_64F);
            Cv2.MeanStdDev(laplaciano, out _, out Scalar desviacionLaplaciano);
            double varianzaNitidez = desviacionLaplaciano.Val0 * desviacionLaplaciano.Val0;
            if (varianzaNitidez < 35)
            {
                mensaje = "La imagen está borrosa. Mantén la cabeza quieta.";
                return false;
            }

            Cv2.EqualizeHist(normalizada, normalizada);
            descriptor = CalcularLbph(normalizada);
            mensaje = "Captura válida.";
            return true;
        }

        public float[] Promediar(IReadOnlyList<float[]> descriptores)
        {
            if (descriptores.Count == 0)
            {
                return Array.Empty<float>();
            }

            var promedio = new float[descriptores[0].Length];
            foreach (float[] descriptor in descriptores)
            {
                if (descriptor.Length != promedio.Length)
                {
                    throw new InvalidOperationException("Los descriptores faciales no tienen el mismo tamaño.");
                }
                for (int i = 0; i < promedio.Length; i++)
                {
                    promedio[i] += descriptor[i];
                }
            }

            for (int i = 0; i < promedio.Length; i++)
            {
                promedio[i] /= descriptores.Count;
            }
            return promedio;
        }

        /// <summary>Devuelve una coincidencia aproximada entre 0 y 1.</summary>
        public double Comparar(float[] primero, float[] segundo)
        {
            if (primero.Length == 0 || primero.Length != segundo.Length)
            {
                return 0;
            }

            double interseccion = 0;
            for (int i = 0; i < primero.Length; i++)
            {
                interseccion += Math.Min(primero[i], segundo[i]);
            }
            return Math.Clamp(interseccion / (CeldasPorLado * CeldasPorLado), 0, 1);
        }

        private static float[] CalcularLbph(Mat gris)
        {
            int celdas = CeldasPorLado * CeldasPorLado;
            var histograma = new float[celdas * NivelesLbp];
            int anchoCelda = Lado / CeldasPorLado;
            int altoCelda = Lado / CeldasPorLado;

            for (int y = 1; y < Lado - 1; y++)
            {
                for (int x = 1; x < Lado - 1; x++)
                {
                    byte centro = gris.At<byte>(y, x);
                    int codigo = 0;
                    if (gris.At<byte>(y - 1, x - 1) >= centro) codigo |= 1;
                    if (gris.At<byte>(y - 1, x) >= centro) codigo |= 2;
                    if (gris.At<byte>(y - 1, x + 1) >= centro) codigo |= 4;
                    if (gris.At<byte>(y, x + 1) >= centro) codigo |= 8;
                    if (gris.At<byte>(y + 1, x + 1) >= centro) codigo |= 16;
                    if (gris.At<byte>(y + 1, x) >= centro) codigo |= 32;
                    if (gris.At<byte>(y + 1, x - 1) >= centro) codigo |= 64;
                    if (gris.At<byte>(y, x - 1) >= centro) codigo |= 128;

                    int celdaX = Math.Min(x / anchoCelda, CeldasPorLado - 1);
                    int celdaY = Math.Min(y / altoCelda, CeldasPorLado - 1);
                    int celda = celdaY * CeldasPorLado + celdaX;
                    histograma[celda * NivelesLbp + codigo]++;
                }
            }

            for (int celda = 0; celda < celdas; celda++)
            {
                int inicio = celda * NivelesLbp;
                float total = 0;
                for (int nivel = 0; nivel < NivelesLbp; nivel++)
                {
                    total += histograma[inicio + nivel];
                }
                if (total <= 0) continue;
                for (int nivel = 0; nivel < NivelesLbp; nivel++)
                {
                    histograma[inicio + nivel] /= total;
                }
            }

            return histograma;
        }
    }
}
