using System;
using System.Diagnostics;
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
        private readonly string rutaModeloFacial;
        private readonly DrowsinessLogic drowsinessLogic;
        private readonly AlarmService alarmService;
        private readonly RegistroEventosSesion registro;
        private readonly MedicionCalidadSesion calidad = new();
        private readonly RastreadorFatiga fatiga;
        private readonly RastreadorAtencion atencion = new();
        private readonly GestorAlertasProgresivas alertas;
        private readonly RastreadorAccesorios accesorios;
        private readonly SesionConduccion sesion;
        private readonly object candadoEstado = new object();
        private readonly DateTimeOffset origenUtc = DateTimeOffset.UtcNow;
        private readonly Stopwatch reloj = Stopwatch.StartNew();
        private DateTimeOffset Ahora => origenUtc + reloj.Elapsed;
        private DateTimeOffset ultimaInterrupcion = DateTimeOffset.MinValue;

        private Thread? hilo;
        private volatile bool running = false;
        private volatile bool paused = false;

        public DetectionController(IDetectorView view,
            FaceDetector faceDetector, string rutaModeloFacial,
            DrowsinessLogic drowsinessLogic, AlarmService alarmService,
            SesionConduccion sesion, CalibracionGuiada? calibracion = null)
        {
            this.view = view;
            this.faceDetector = faceDetector;
            this.rutaModeloFacial = rutaModeloFacial;
            this.drowsinessLogic = drowsinessLogic;
            this.alarmService = alarmService;
            this.sesion = sesion;
            registro = new RegistroEventosSesion(sesion);
            fatiga = new RastreadorFatiga(sesion, calibracion);
            alertas = new GestorAlertasProgresivas(sesion);
            accesorios = new RastreadorAccesorios(sesion);
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
                using var detectorFacial = new DetectorPuntosFaciales(rutaModeloFacial);
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
                ProcesarCaptura(camara, detectorFacial, cancelacion);
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
                    InterrumpirObservacion(Ahora);
                }
            }
        }

        private void ProcesarCaptura(CameraService camara, DetectorPuntosFaciales detectorFacial,
            CancellationToken cancelacion)
        {
            using var detectorCalidad = new DetectorCalidadImagen();
            var detectorAccesorios = new DetectorAccesorios();
            DateTimeOffset ultimaContinuidad = DateTimeOffset.MinValue;
            while (running && !cancelacion.IsCancellationRequested)
            {
                if (paused)
                {
                    Thread.Sleep(100);
                    continue;
                }

                DateTimeOffset inicioLectura = Ahora;
                using Mat frame = LeerFotograma(camara, inicioLectura);

                if (frame.Empty())
                {
                    lock (candadoEstado)
                    {
                        if (running && !paused && inicioLectura > ultimaInterrupcion)
                        {
                            calidad.RegistrarFallo();
                            InterrumpirObservacion(Ahora);
                        }
                    }
                    view.UpdateImage(null, EyeState.DESPIERTO, 0, null, null);
                    Thread.Sleep(10);
                    continue;
                }

                DateTimeOffset instante = Ahora;
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
                Rect[] rostros = indicio.HasValue ? Array.Empty<Rect>() : faceDetector.Detect(frame);
                EstadoIluminacion iluminacion = MedidorIluminacion.Evaluar(frame);
                MedicionFacial? medicion = rostros.Length == 1 && iluminacion == EstadoIluminacion.Adecuada
                    ? detectorFacial.Medir(frame, rostros[0]) : null;
                EvaluacionAccesorios? evaluacionAccesorios = rostros.Length == 1 &&
                    iluminacion == EstadoIluminacion.Adecuada
                    ? detectorAccesorios.Evaluar(frame, rostros[0], medicion) : null;
                bool rostroParcial = rostros.Length == 1 &&
                    (rostros[0].X <= frame.Width * 0.025 || rostros[0].Y <= frame.Height * 0.025 ||
                     rostros[0].Right >= frame.Width * 0.975 || rostros[0].Bottom >= frame.Height * 0.975);
                foreach (Rect rostro in rostros)
                    Cv2.Rectangle(frame, rostro, new Scalar(255, 0, 0), 2);
                Cv2.Flip(frame, frame, FlipMode.Y);

                EyeState estado;
                FatigaEnVivo lectura;
                double? fpsRecientes;
                MuestraAtencion? muestraAtencion;
                EstadoAlertaProgresiva? alerta;
                EvaluacionAccesorios? accesoriosEnVivo;
                EstadoIluminacion? avisoIluminacion;
                TipoEventoSesion? avisoRostro;
                lock (candadoEstado)
                {
                    if (!running || paused || inicioLectura <= ultimaInterrupcion)
                        continue;

                    registro.ObservarCamara(instante, indicio, avisoCalidad);
                    bool analisisDisponible = !indicio.HasValue;
                    bool rostroValido = analisisDisponible && rostros.Length == 1;
                    if (evaluacionAccesorios is null)
                    {
                        accesorios.Interrumpir(instante);
                        accesoriosEnVivo = null;
                    }
                    else accesoriosEnVivo = accesorios.Observar(instante, evaluacionAccesorios);
                    bool medicionConfiable = medicion.HasValue && accesoriosEnVivo?.Confiable == true;
                    calidad.RegistrarFotograma(rostroValido, medicionConfiable);
                    fpsRecientes = calidad.FpsRecientes;

                    lectura = fatiga.Observar(instante, medicionConfiable ? medicion : null,
                        drowsinessLogic.UmbralCierreProlongado, rostroParcial);
                    estado = drowsinessLogic.Evaluar(lectura.CierreMs);
                    atencion.Observar(instante, lectura, estado, calidad.Resumen(), sesion,
                        drowsinessLogic.UmbralCierreProlongado);
                    muestraAtencion = atencion.Actual;
                    alerta = alertas.Observar(instante, estado, lectura, muestraAtencion, calidad.Resumen());
                    (avisoIluminacion, avisoRostro) = registro.Observar(
                        instante, rostros.Length, estado, iluminacion, analisisDisponible);

                    alarmService.Actualizar(alerta.Nivel, alerta.EmitirSonido);
                }

                var image = ImageUtils.MatToBitmap(frame);
                try
                {
                    view.UpdateImage(image, estado, lectura.CierreMs, avisoIluminacion, avisoRostro,
                        avisoCalidad, lectura, fpsRecientes, muestraAtencion, alerta, accesoriosEnVivo);
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

        /// <summary>Para el hilo, espera a que termine y apaga la alarma.</summary>
        public void Detener()
        {
            lock (candadoEstado)
            {
                running = false;
                calidad.Interrumpir();
                sesion.Calidad = calidad.Resumen();
                InterrumpirObservacion(Ahora);
                sesion.MetricasFatiga = fatiga.Resumen;
                sesion.Atencion = atencion.Resumen();
                sesion.Postura = fatiga.ResumenPostura;
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
                    InterrumpirObservacion(Ahora);
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
            fatiga.Interrumpir(instante, drowsinessLogic.UmbralCierreProlongado);
            atencion.Interrumpir(instante);
            alertas.Interrumpir(instante);
            accesorios.Interrumpir(instante);
            alarmService.Detener();
        }
    }
}
