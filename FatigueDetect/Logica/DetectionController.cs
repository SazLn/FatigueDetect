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
        private readonly MedicionCalidadSesion calidad = new();
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

        public Task IniciarAsync(string? camaraId, CancellationToken cancelacion, FormatoCamara? formato = null)
        {
            var arranque = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            running = true;
            hilo = HiloCapturaCamara.Iniciar(() => Bucle(camaraId, arranque, cancelacion, formato));
            return arranque.Task;
        }

        private void Bucle(string? camaraId, TaskCompletionSource arranque, CancellationToken cancelacion,
            FormatoCamara? formato)
        {
            try
            {
                using var camara = new CameraService(camaraId, cancelacion, formato);
                if (!camara.IsOpened())
                    throw new InvalidOperationException("No se pudo abrir la cámara. Selecciona o prueba otra en Configuración, " +
                        "cierra las aplicaciones que la usen y comprueba los permisos de cámara de Windows.");

                cancelacion.ThrowIfCancellationRequested();
                sesion.Inicio = DateTime.Now;
                lock (candadoEstado)
                {
                    if (running && !paused)
                        calidad.Reanudar();
                }
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
                    calidad.Interrumpir();
                    InterrumpirObservacion(DateTimeOffset.UtcNow);
                }
            }
        }

        private void ProcesarCaptura(CameraService camara, CancellationToken cancelacion)
        {
            using var detectorCalidad = new DetectorCalidadImagen();
            DateTimeOffset ultimaContinuidad = DateTimeOffset.MinValue;
            while (running && !cancelacion.IsCancellationRequested)
            {
                if (paused)
                {
                    Thread.Sleep(100);
                    continue;
                }

                DateTimeOffset inicioLectura = DateTimeOffset.UtcNow;
                using Mat frame = LeerFotograma(camara, inicioLectura);

                if (frame.Empty())
                {
                    lock (candadoEstado)
                    {
                        if (running && !paused && inicioLectura > ultimaInterrupcion)
                        {
                            calidad.RegistrarFallo();
                            InterrumpirObservacion(DateTimeOffset.UtcNow);
                        }
                    }
                    view.UpdateImage(null, EyeState.DESPIERTO, 0, null, null);
                    Thread.Sleep(10);
                    continue;
                }

                DateTimeOffset instante = DateTimeOffset.UtcNow;
                TipoEventoSesion? avisoCalidad;
                TipoEventoSesion? indicio;
                lock (candadoEstado)
                {
                    if (!running || paused || inicioLectura <= ultimaInterrupcion)
                        continue;
                    if (ultimaContinuidad != ultimaInterrupcion)
                    {
                        detectorCalidad.Reiniciar();
                        ultimaContinuidad = ultimaInterrupcion;
                    }
                    avisoCalidad = detectorCalidad.Observar(frame, instante, out indicio);
                }
                Cv2.Flip(frame, frame, FlipMode.Y);
                Rect[] rostros = indicio.HasValue ? Array.Empty<Rect>() : faceDetector.Detect(frame);
                EstadoIluminacion iluminacion = MedidorIluminacion.Evaluar(frame);
                bool ojosDetectados = rostros.Length > 0 && DibujarRostroYOjos(frame, rostros);

                EyeState estado;
                long cierreMs;
                EstadoIluminacion? avisoIluminacion;
                TipoEventoSesion? avisoRostro;
                lock (candadoEstado)
                {
                    if (!running || paused || inicioLectura <= ultimaInterrupcion)
                        continue;

                    registro.ObservarCamara(instante, indicio, avisoCalidad);
                    bool analisisDisponible = !indicio.HasValue;
                    bool rostroValido = analisisDisponible && rostros.Length == 1;
                    calidad.RegistrarFotograma(rostroValido,
                        rostroValido && iluminacion == EstadoIluminacion.Adecuada);

                    estado = rostros.Length == 0
                        ? drowsinessLogic.EvaluateSinRostro()
                        : drowsinessLogic.Evaluate(ojosDetectados);
                    cierreMs = drowsinessLogic.GetClosedDurationMs();
                    (avisoIluminacion, avisoRostro) = registro.Observar(
                        instante, rostros.Length, ojosDetectados, estado, iluminacion, analisisDisponible);

                    if (estado == EyeState.ALARMA_ACTIVADA)
                        alarmService.Activar();
                    else
                        alarmService.Detener();
                }

                var image = ImageUtils.MatToBitmap(frame);
                try
                {
                    view.UpdateImage(image, estado, cierreMs, avisoIluminacion, avisoRostro, avisoCalidad);
                }
                catch (Exception) when (!running)
                {
                    // La pantalla ya se cerró mientras se procesaba el último frame.
                }
            }
        }

        private Mat LeerFotograma(CameraService camara, DateTimeOffset inicioLectura)
        {
            try
            {
                return camara.GetFrame();
            }
            catch
            {
                lock (candadoEstado)
                {
                    if (running && !paused && inicioLectura > ultimaInterrupcion)
                        calidad.RegistrarFallo();
                }
                throw;
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
                calidad.Interrumpir();
                sesion.Calidad = calidad.Resumen();
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
                {
                    calidad.Interrumpir();
                    InterrumpirObservacion(DateTimeOffset.UtcNow);
                }
                else if (running)
                {
                    calidad.Reanudar();
                }
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
