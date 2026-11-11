using System;
using System.Linq;
using System.Threading;
using OpenCvSharp;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    /// <summary>
    /// Bucle de detección. Se crea uno por sesión: Iniciar() arranca el hilo y
    /// Detener() lo para y espera a que termine.
    /// </summary>
    public class DetectionController
    {
        private readonly CameraService cameraService;
        private readonly IDetectorView view;
        private readonly FaceDetector faceDetector;
        private readonly EyeDetector eyeDetector;
        private readonly DrowsinessLogic drowsinessLogic;
        private readonly AlarmService alarmService;
        private readonly SesionConduccion sesion;
        private EyeState estadoAnterior = EyeState.DESPIERTO;

        private Thread? hilo;
        private volatile bool running = false;
        private volatile bool paused = false;

        public DetectionController(CameraService cameraService, IDetectorView view,
            FaceDetector faceDetector, EyeDetector eyeDetector,
            DrowsinessLogic drowsinessLogic, AlarmService alarmService,
            SesionConduccion sesion)
        {
            this.cameraService = cameraService;
            this.view = view;
            this.faceDetector = faceDetector;
            this.eyeDetector = eyeDetector;
            this.drowsinessLogic = drowsinessLogic;
            this.alarmService = alarmService;
            this.sesion = sesion;
        }

        public void Iniciar()
        {
            if (running) return;

            running = true;
            hilo = new Thread(Bucle) { IsBackground = true };
            hilo.Start();
        }

        private void Bucle()
        {
            if (!cameraService.IsOpened())
            {
                System.Console.WriteLine("Error con la cámara");
                return;
            }

            while (running)
            {
                if (paused)
                {
                    Thread.Sleep(100);
                    continue;
                }

                using Mat frame = cameraService.GetFrame();

                if (frame.Empty())
                {
                    Thread.Sleep(10);
                    continue;
                }

                Cv2.Flip(frame, frame, FlipMode.Y);

                bool ojosDetectados = ProcesarFrame(frame);

                EyeState estado = drowsinessLogic.Evaluate(ojosDetectados);

                // Cuenta una alarma cada vez que se ENTRA al estado de alarma
                // (no una por cada frame mientras suena).
                if (estado == EyeState.ALARMA_ACTIVADA && estadoAnterior != EyeState.ALARMA_ACTIVADA)
                {
                    sesion.RegistrarAlarma();
                }
                estadoAnterior = estado;

                if (drowsinessLogic.ShouldActivateAlarm())
                {
                    alarmService.Activar();
                }
                else
                {
                    alarmService.Detener();
                }

                var image = ImageUtils.MatToBitmap(frame);
                try
                {
                    view.UpdateImage(image, estado, drowsinessLogic.GetClosedDurationMs());
                }
                catch (Exception) when (!running)
                {
                    // La pantalla ya se cerró mientras se procesaba el último frame.
                }
            }
        }

        private bool ProcesarFrame(Mat frame)
        {
            Rect[] rostros = faceDetector.Detect(frame);

            // Sin rostro → resetear todo inmediatamente
            if (rostros.Length == 0)
            {
                drowsinessLogic.EvaluateSinRostro();
                return false;
            }

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
            running = false;
            alarmService.Detener();

            if (hilo != null && hilo.IsAlive)
            {
                hilo.Join(3000);
            }

            alarmService.Detener();
        }

        public void SetPaused(bool paused)
        {
            this.paused = paused;
        }
    }
}
