using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public class CameraService : IDisposable
    {
        private static readonly SemaphoreSlim acceso = new(1, 1);
        private VideoCapture? camera;

        public CameraService(string? dispositivoId, CancellationToken cancelacion = default)
        {
            if (!acceso.Wait(TimeSpan.FromSeconds(2), cancelacion))
                throw new InvalidOperationException("La aplicación está usando la cámara en otra pantalla.");

            try
            {
                camera = Abrir(dispositivoId, cancelacion);
            }
            finally
            {
                if (camera == null)
                    acceso.Release();
            }
        }

        private static VideoCapture? Abrir(string? dispositivoId, CancellationToken cancelacion)
        {
            cancelacion.ThrowIfCancellationRequested();
            if (dispositivoId != null)
            {
                DispositivoCamara? dispositivo = CatalogoCamaras.Listar().FirstOrDefault(c =>
                    string.Equals(c.Id, dispositivoId, StringComparison.OrdinalIgnoreCase));
                if (dispositivo == null)
                    throw new InvalidOperationException("La cámara seleccionada no está conectada. Vuelve a conectarla o cambia la selección en Configuración.");

                return IntentarAbrir(dispositivo.Indice, VideoCaptureAPIs.DSHOW, cancelacion);
            }

            // Mantiene los intentos de respaldo para cámaras sin una entrada DirectShow.
            int[] indicesDirectShow;
            try
            {
                indicesDirectShow = CatalogoCamaras.Listar().Select(c => c.Indice).ToArray();
            }
            catch (COMException)
            {
                indicesDirectShow = new[] { 0, 1, 2 };
            }

            foreach (int indice in indicesDirectShow)
            {
                VideoCapture? intento = IntentarAbrir(indice, VideoCaptureAPIs.DSHOW, cancelacion);
                if (intento != null)
                    return intento;
            }

            for (int indice = 0; indice <= 2; indice++)
            {
                foreach (VideoCaptureAPIs backend in new[] { VideoCaptureAPIs.MSMF, VideoCaptureAPIs.ANY })
                {
                    VideoCapture? intento = IntentarAbrir(indice, backend, cancelacion);
                    if (intento != null)
                        return intento;
                }
            }
            return null;
        }

        private static VideoCapture? IntentarAbrir(int indice, VideoCaptureAPIs backend, CancellationToken cancelacion)
        {
            cancelacion.ThrowIfCancellationRequested();
            var intento = new VideoCapture();
            bool abierto = false;
            try
            {
                abierto = intento.Open(indice, backend) && EntregaImagen(intento, cancelacion);
                return abierto ? intento : null;
            }
            finally
            {
                if (!abierto)
                    intento.Dispose();
            }
        }

        // Algunas cámaras tardan un momento en entregar el primer frame.
        private static bool EntregaImagen(VideoCapture candidata, CancellationToken cancelacion)
        {
            using var prueba = new Mat();

            for (int i = 0; i < 10; i++)
            {
                cancelacion.ThrowIfCancellationRequested();
                bool entrega = candidata.Read(prueba) && !prueba.Empty();
                cancelacion.ThrowIfCancellationRequested();
                if (entrega)
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
            try
            {
                if (camera?.Read(frame) == true)
                    return frame;

                if (frame.Empty())
                    return frame;

                frame.Dispose();
                return new Mat();
            }
            catch (OpenCVException)
            {
                // Una desconexión del dispositivo puede provocar un error de lectura.
                frame.Dispose();
                return new Mat();
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }

        /// <summary>Libera la cámara (se apaga la luz) para poder abrirla en otra sesión.</summary>
        public void Dispose()
        {
            VideoCapture? captura = Interlocked.Exchange(ref camera, null);
            if (captura == null)
                return;

            try
            {
                captura.Dispose();
            }
            finally
            {
                acceso.Release();
            }
        }
    }
}
