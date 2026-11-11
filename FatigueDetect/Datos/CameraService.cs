using System;
using System.Threading;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public class CameraService : IDisposable
    {
        private VideoCapture? camera;

        public CameraService()
        {
            // DSHOW suele funcionar mejor que el backend por defecto en Windows.
            VideoCaptureAPIs[] backends =
            {
                VideoCaptureAPIs.DSHOW,
                VideoCaptureAPIs.ANY,
                VideoCaptureAPIs.MSMF
            };

            for (int indice = 0; indice <= 2 && camera == null; indice++)
            {
                foreach (VideoCaptureAPIs backend in backends)
                {
                    var intento = new VideoCapture(indice, backend);

                    if (intento.IsOpened() && EntregaImagen(intento))
                    {
                        camera = intento;
                        break;
                    }

                    intento.Dispose();
                }
            }
        }

        // Algunas cámaras tardan un momento en entregar el primer frame.
        private static bool EntregaImagen(VideoCapture candidata)
        {
            using var prueba = new Mat();

            for (int i = 0; i < 10; i++)
            {
                if (candidata.Read(prueba) && !prueba.Empty())
                {
                    return true;
                }
                Thread.Sleep(100);
            }

            return false;
        }

        public bool IsOpened()
        {
            return camera != null && camera.IsOpened();
        }

        public Mat GetFrame()
        {
            var frame = new Mat();
            camera?.Read(frame);
            return frame;
        }

        /// <summary>Libera la cámara (se apaga la luz) para poder abrirla en otra sesión.</summary>
        public void Dispose()
        {
            if (camera != null)
            {
                camera.Release();
                camera.Dispose();
                camera = null;
            }
        }
    }
}
