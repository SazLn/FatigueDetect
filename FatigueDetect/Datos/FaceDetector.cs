using System;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public class FaceDetector
    {
        private readonly CascadeClassifier faceClassifier;

        public FaceDetector(string cascadePath)
        {
            faceClassifier = new CascadeClassifier(cascadePath);
        }

        public Rect[] Detect(Mat frame)
        {
            if (frame.Empty() || frame.Rows <= 0 || frame.Cols <= 0)
            {
                return Array.Empty<Rect>();
            }

            using var gris = new Mat();
            try
            {
                Cv2.CvtColor(frame, gris, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gris, gris);

                Rect[] rostros = faceClassifier.DetectMultiScale(
                    gris,
                    scaleFactor: 1.1,
                    minNeighbors: 5,
                    flags: (HaarDetectionTypes)0,
                    minSize: new Size(100, 100),
                    maxSize: new Size());

                return rostros;
            }
            catch
            {
                return Array.Empty<Rect>();
            }
        }
    }
}
