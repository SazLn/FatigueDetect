using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;
using OpenCvSharp;

namespace FatigueDetect.Presentacion
{
    public sealed class VentanaCalibracionGuiada : Form
    {
        private readonly Configuracion configuracion;
        private readonly string vehiculo;
        private readonly CalibracionGuiada? guardada;
        private readonly PictureBox vista = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black };
        private readonly Label instruccion;
        private readonly Label detalle;
        private readonly ProgressBar progreso = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
        private readonly Button calibrar;
        private readonly Button usarGuardada;
        private readonly Button continuar;
        private CancellationTokenSource? cancelacion;
        private Thread? hilo;

        public CalibracionGuiada? Resultado { get; private set; }

        public VentanaCalibracionGuiada(Configuracion configuracion, string vehiculo,
            PerfilPersonalConductor? perfil)
        {
            this.configuracion = configuracion;
            this.vehiculo = vehiculo;
            guardada = perfil?.Calibracion;
            Text = "Calibración guiada antes de conducir";
            Size = new System.Drawing.Size(820, 650);
            MinimumSize = new System.Drawing.Size(700, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Tema.Fondo;
            ForeColor = Tema.Texto;
            Font = Tema.Fuente(10);

            var raiz = Tema.TablaFilas(Tema.Fondo, 62, 0, 58, 28, 55);
            raiz.Padding = new Padding(18);
            raiz.Controls.Add(Tema.Etiqueta("Calibración personal de ojos y postura", 17, Tema.Texto,
                Tema.Fondo, FontStyle.Bold), 0, 0);
            raiz.Controls.Add(vista, 0, 1);
            instruccion = Tema.Etiqueta("Elige una calibración guardada o realiza una nueva.", 11,
                Tema.Texto, Tema.Fondo, FontStyle.Bold, ContentAlignment.MiddleCenter);
            raiz.Controls.Add(instruccion, 0, 2);
            detalle = Tema.Etiqueta(Contexto(perfil), 9, Tema.TextoSuave, Tema.Fondo,
                alineacion: ContentAlignment.MiddleCenter);
            raiz.Controls.Add(detalle, 0, 3);

            var pie = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, BackColor = Tema.Fondo };
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            usarGuardada = Tema.Boton("Usar perfil guardado", Tema.AzulOscuro, Tema.Texto);
            usarGuardada.Dock = DockStyle.Fill;
            usarGuardada.Enabled = guardada is not null;
            calibrar = Tema.Boton("Calibrar ahora", Tema.Azul, Color.White);
            calibrar.Dock = DockStyle.Fill;
            continuar = Tema.Boton("Continuar", Tema.Verde, Color.White);
            continuar.Dock = DockStyle.Fill;
            continuar.Enabled = false;
            var cancelar = Tema.Boton("Cancelar", Tema.TarjetaClara, Tema.Texto);
            cancelar.Dock = DockStyle.Fill;
            pie.Controls.Add(usarGuardada, 0, 0);
            pie.Controls.Add(calibrar, 1, 0);
            pie.Controls.Add(continuar, 2, 0);
            pie.Controls.Add(cancelar, 3, 0);
            raiz.Controls.Add(pie, 0, 4);
            Controls.Add(raiz);
            progreso.Parent = vista;
            progreso.Dock = DockStyle.Bottom;
            progreso.Height = 10;

            usarGuardada.Click += (_, _) => { Resultado = guardada; DialogResult = DialogResult.OK; Close(); };
            calibrar.Click += (_, _) => IniciarCalibracion();
            continuar.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            FormClosing += (_, _) => DetenerCaptura();
        }

        private string Contexto(PerfilPersonalConductor? perfil) =>
            $"Perfil: {(perfil?.Calibrado == true ? "calibrado" : "en aprendizaje")} · " +
            $"Cámara: {configuracion.CamaraId ?? "Automática"} · Vehículo: {vehiculo}";

        private void IniciarCalibracion()
        {
            DetenerCaptura();
            calibrar.Enabled = usarGuardada.Enabled = continuar.Enabled = false;
            instruccion.Text = "Abriendo la cámara…";
            detalle.Text = "No conduzcas durante esta comprobación.";
            progreso.Value = 0;
            cancelacion = new CancellationTokenSource();
            CancellationToken token = cancelacion.Token;
            hilo = HiloCapturaCamara.Iniciar(() => Capturar(token));
        }

        private void Capturar(CancellationToken token)
        {
            try
            {
                string recursos = Path.Combine(AppContext.BaseDirectory, "Resources");
                var rostros = new FaceDetector(Path.Combine(recursos, "cascades", "haarcascade_frontalface_default.xml"));
                using var puntos = new DetectorPuntosFaciales(Path.Combine(recursos, "modelos", "face-landmark.onnx"));
                using var camara = new CameraService(configuracion.CamaraId, token, configuracion.FormatoCamara);
                if (!camara.IsOpened())
                    throw new InvalidOperationException("No se pudo abrir la cámara seleccionada.");
                var guia = new CalibradorGuiado(configuracion.CamaraId, vehiculo);
                while (!token.IsCancellationRequested && guia.Resultado is null)
                {
                    using Mat frame = camara.GetFrame();
                    if (frame.Empty())
                        continue;
                    Rect[] caras = rostros.Detect(frame);
                    MedicionFacial? medicion = caras.Length == 1 &&
                        MedidorIluminacion.Evaluar(frame) == EstadoIluminacion.Adecuada
                        ? puntos.Medir(frame, caras[0]) : null;
                    EstadoCalibracion estado = guia.Observar(DateTimeOffset.UtcNow, medicion);
                    foreach (Rect cara in caras)
                        Cv2.Rectangle(frame, cara, new Scalar(255, 0, 0), 2);
                    Cv2.Flip(frame, frame, FlipMode.Y);
                    Bitmap? imagen = ImageUtils.MatToBitmap(frame);
                    Publicar(imagen, estado);
                }
                if (guia.Resultado is { } resultado)
                {
                    Resultado = resultado;
                    Publicar(null, new EstadoCalibracion("Calibración completada.", 1,
                        resultado.MuestrasValidas, resultado.ParpadeosConfirmados, true));
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                PublicarError(ex.Message);
            }
        }

        private void Publicar(Bitmap? imagen, EstadoCalibracion estado)
        {
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (imagen is not null)
                    {
                        Image? anterior = vista.Image;
                        vista.Image = imagen;
                        anterior?.Dispose();
                    }
                    instruccion.Text = estado.Instruccion;
                    detalle.Text = $"Muestras válidas: {estado.MuestrasValidas} · Parpadeos: {estado.ParpadeosConfirmados}/2";
                    progreso.Value = Math.Clamp((int)Math.Round(estado.Progreso * 100), 0, 100);
                    if (estado.Completa)
                    {
                        continuar.Enabled = true;
                        calibrar.Enabled = true;
                    }
                }));
            }
            catch
            {
                imagen?.Dispose();
            }
        }

        private void PublicarError(string mensaje)
        {
            try
            {
                BeginInvoke(new Action(() =>
                {
                    instruccion.Text = "No se pudo completar la calibración.";
                    detalle.Text = mensaje;
                    calibrar.Enabled = true;
                    usarGuardada.Enabled = guardada is not null;
                }));
            }
            catch { }
        }

        private void DetenerCaptura()
        {
            cancelacion?.Cancel();
            if (hilo is { IsAlive: true } && hilo != Thread.CurrentThread)
                hilo.Join(1500);
            cancelacion?.Dispose();
            cancelacion = null;
            hilo = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetenerCaptura();
                vista.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
