using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    /// <summary>
    /// Bucle de detección. Abre, lee y libera la cámara en el mismo hilo.
    /// </summary>
    public class DetectionController
    {
        private readonly IDetectorView view;
        private readonly FaceDetector faceDetector;
        private readonly EyeDetector eyeDetector;
        private readonly DrowsinessLogic drowsinessLogic;
        private readonly AlarmService alarmService;
        private readonly RegistroEventosSesion registro;
        private readonly SesionConduccion sesion;
        private readonly object candadoEstado = new object();
        private DateTimeOffset ultimaInterrupcion = DateTimeOffset.MinValue;

        private Thread? hilo;
        private volatile bool running = false;
        private volatile bool paused = false;

        public DetectionController(IDetectorView view,
            FaceDetector faceDetector, EyeDetector eyeDetector,
            DrowsinessLogic drowsinessLogic, AlarmService alarmService,
            SesionConduccion sesion)
        {
            this.view = view;
            this.faceDetector = faceDetector;
            this.eyeDetector = eyeDetector;
            this.drowsinessLogic = drowsinessLogic;
            this.alarmService = alarmService;
            this.sesion = sesion;
            registro = new RegistroEventosSesion(sesion);
        }

        public Task IniciarAsync(string? camaraId, CancellationToken cancelacion)
        {
            var arranque = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            running = true;
            hilo = HiloCapturaCamara.Iniciar(() => Bucle(camaraId, arranque, cancelacion));
            return arranque.Task;
        }

        private void Bucle(string? camaraId, TaskCompletionSource arranque, CancellationToken cancelacion)
        {
            try
            {
                using var camara = new CameraService(camaraId, cancelacion);
                if (!camara.IsOpened())
                    throw new InvalidOperationException("No se pudo abrir la cámara. Selecciona o prueba otra en Configuración, " +
                        "cierra las aplicaciones que la usen y comprueba los permisos de cámara de Windows.");

                cancelacion.ThrowIfCancellationRequested();
                sesion.Inicio = DateTime.Now;
                arranque.SetResult();
                ProcesarCaptura(camara, cancelacion);
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                arranque.TrySetCanceled(cancelacion);
            }
            catch (Exception ex)
            {
                if (!arranque.TrySetException(ex) && running)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                    view.UpdateImage(null, EyeState.DESPIERTO, 0, null, null);
                }
            }
            finally
            {
                lock (candadoEstado)
                {
                    running = false;
                    InterrumpirObservacion(DateTimeOffset.UtcNow);
                }
            }
        }

        private void ProcesarCaptura(CameraService camara, CancellationToken cancelacion)
        {
            while (running && !cancelacion.IsCancellationRequested)
            {
                if (paused)
                {
                    Thread.Sleep(100);
                    continue;
                }

                using Mat frame = camara.GetFrame();

                if (frame.Empty())
                {
                    lock (candadoEstado)
                    {
                        if (running && !paused)
                            InterrumpirObservacion(DateTimeOffset.UtcNow);
                    }
                    view.UpdateImage(null, EyeState.DESPIERTO, 0, null, null);
                    Thread.Sleep(10);
                    continue;
                }

                DateTimeOffset instante = DateTimeOffset.UtcNow;
                Cv2.Flip(frame, frame, FlipMode.Y);
                Rect[] rostros = faceDetector.Detect(frame);
                EstadoIluminacion iluminacion = MedidorIluminacion.Evaluar(frame);
                bool ojosDetectados = rostros.Length > 0 && DibujarRostroYOjos(frame, rostros);

                EyeState estado;
                long cierreMs;
                EstadoIluminacion? avisoIluminacion;
                TipoEventoSesion? avisoRostro;
                lock (candadoEstado)
                {
                    if (!running || paused || instante <= ultimaInterrupcion)
                        continue;

                    estado = rostros.Length == 0
                        ? drowsinessLogic.EvaluateSinRostro()
                        : drowsinessLogic.Evaluate(ojosDetectados);
                    cierreMs = drowsinessLogic.GetClosedDurationMs();
                    (avisoIluminacion, avisoRostro) = registro.Observar(
                        instante, rostros.Length, ojosDetectados, estado, iluminacion);

                    if (estado == EyeState.ALARMA_ACTIVADA)
                        alarmService.Activar();
                    else
                        alarmService.Detener();
                }

                var image = ImageUtils.MatToBitmap(frame);
                try
                {
                    view.UpdateImage(image, estado, cierreMs, avisoIluminacion, avisoRostro);
                }
                catch (Exception) when (!running)
                {
                    // La pantalla ya se cerró mientras se procesaba el último frame.
                }
            }
        }

        private bool DibujarRostroYOjos(Mat frame, Rect[] rostros)
        {
            // Solo el rostro más grande (el del conductor)
            Rect rostro = rostros.OrderByDescending(r => r.Width * r.Height).First();
            Cv2.Rectangle(frame, rostro, new Scalar(255, 0, 0), 2);

            using Mat regionRostro = new Mat(frame, rostro);
            Rect[] ojos = eyeDetector.Detect(regionRostro);

            foreach (Rect ojo in ojos)
            {
                var ojoEnFrame = new Rect(
                    rostro.X + ojo.X,
                    rostro.Y + ojo.Y + (int)(rostro.Height * 0.25),
                    ojo.Width,
                    ojo.Height);
                Cv2.Rectangle(frame, ojoEnFrame, new Scalar(0, 255, 0), 2);
            }

            // 2 ojos detectados → despierto; si no → ojos cerrados
            return ojos.Length >= 2;
        }

        /// <summary>Para el hilo, espera a que termine y apaga la alarma.</summary>
        public void Detener()
        {
            lock (candadoEstado)
            {
                running = false;
                InterrumpirObservacion(DateTimeOffset.UtcNow);
            }

            if (hilo != null && hilo.IsAlive)
            {
                hilo.Join(3000);
            }
        }

        public void SetPaused(bool paused)
        {
            lock (candadoEstado)
            {
                this.paused = paused;
                if (paused)
                    InterrumpirObservacion(DateTimeOffset.UtcNow);
            }
        }

        private void InterrumpirObservacion(DateTimeOffset instante)
        {
            ultimaInterrupcion = instante;
            registro.Interrumpir(instante);
            drowsinessLogic.Reset();
            alarmService.Detener();
        }
    }
}
