using System;
using System.Linq;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public class EyeDetector
    {
        private readonly CascadeClassifier eyeClassifier;

        public EyeDetector(string cascadePath)
        {
            eyeClassifier = new CascadeClassifier(cascadePath);
        }

        public Rect[] Detect(Mat faceRegion)
        {
            if (faceRegion.Empty() || faceRegion.Rows <= 0 || faceRegion.Cols <= 0)
            {
                return Array.Empty<Rect>();
            }

            int inicio = (int)(faceRegion.Rows * 0.25);
            int fin = (int)(faceRegion.Rows * 0.55);
            int altura = fin - inicio;

            if (altura <= 0)
            {
                return Array.Empty<Rect>();
            }

            var zonaOjos = new Rect(0, inicio, faceRegion.Cols, altura);

            if (zonaOjos.X + zonaOjos.Width > faceRegion.Cols ||
                zonaOjos.Y + zonaOjos.Height > faceRegion.Rows)
            {
                return Array.Empty<Rect>();
            }

            using var zonaBusqueda = new Mat(faceRegion, zonaOjos);
            using var gris = new Mat();

            try
            {
                Cv2.CvtColor(zonaBusqueda, gris, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gris, gris);
                Cv2.GaussianBlur(gris, gris, new Size(3, 3), 0);

                Rect[] ojosArray = eyeClassifier.DetectMultiScale(
                    gris,
                    scaleFactor: 1.1,
                    minNeighbors: 8,
                    flags: (HaarDetectionTypes)0,
                    minSize: new Size(30, 30),
                    maxSize: new Size());

                if (ojosArray.Length > 2)
                {
                    return ojosArray
                        .OrderByDescending(r => r.Width * r.Height)
                        .Take(2)
                        .ToArray();
                }

                return ojosArray;
            }
            catch
            {
                return Array.Empty<Rect>();
            }
        }
    }
}
