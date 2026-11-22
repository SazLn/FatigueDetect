using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;
using OpenCvSharp;

namespace FatigueDetect.Presentacion
{
    internal sealed class PanelCamaraConfiguracion : UserControl
    {
        private readonly Configuracion config;
        private readonly PreferenciaCamara preferencia;
        private readonly ComboBox selector = new ComboBox();
        private readonly ComboBox resolucion = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox fps = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Button actualizar = Tema.Boton("Actualizar lista", Tema.TarjetaClara, Tema.Texto);
        private readonly Button previsualizar = Tema.Boton("Ver vista previa", Tema.Azul, Color.White);
        private readonly PictureBox vista = new PictureBox();
        private readonly Label marcadorVista = new Label();
        private readonly Label estado = Tema.Etiqueta("", 10, Tema.TextoSuave, Tema.Tarjeta);
        private readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer { Interval = 80 };
        private CancellationTokenSource? captura;
        private Cuadro? pendiente;
        private bool disponible = true;

        public PanelCamaraConfiguracion(Configuracion config, PreferenciaCamara preferencia)
        {
            this.config = config;
            this.preferencia = preferencia;
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = Tema.Fondo;
            AutoScaleMode = AutoScaleMode.Dpi;

            var tarjeta = new PanelRedondeado
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(LogicalToDeviceUnits(22)),
                Radio = LogicalToDeviceUnits(16)
            };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 30, 28, 22, 42, 66, 0, 60, 54);
            tabla.Controls.Add(Tema.Etiqueta("Cámara", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta("Elige el dispositivo y la calidad de captura.", 9,
                Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            tabla.Controls.Add(Tema.Etiqueta("Dispositivo", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 2);

            selector.DropDownStyle = ComboBoxStyle.DropDownList;
            selector.FlatStyle = FlatStyle.Flat;
            selector.BackColor = Tema.TarjetaClara;
            selector.ForeColor = Tema.Texto;
            selector.Font = Tema.Fuente(10);
            selector.Dock = DockStyle.Fill;
            selector.Margin = new Padding(0);
            selector.AccessibleName = "Seleccionar cámara";
            selector.SelectionChangeCommitted += GuardarSeleccion;
            tabla.Controls.Add(selector, 0, 3);

            foreach (ComboBox control in new[] { resolucion, fps })
            {
                control.FlatStyle = FlatStyle.Flat;
                control.Font = Tema.Fuente(10);
                control.BackColor = Tema.TarjetaClara;
                control.ForeColor = Tema.Texto;
                control.Margin = Padding.Empty;
            }

            tabla.Controls.Add(ConstruirFormato(), 0, 4);
            vista.Dock = DockStyle.Fill;
            vista.SizeMode = PictureBoxSizeMode.Zoom;
            vista.BackColor = Tema.Lateral;
            vista.BorderStyle = BorderStyle.FixedSingle;
            vista.Margin = new Padding(0, 4, 0, 12);
            marcadorVista.Dock = DockStyle.Fill;
            marcadorVista.Text = "▣\r\n\r\nLa vista previa aparecerá aquí.";
            marcadorVista.TextAlign = ContentAlignment.MiddleCenter;
            marcadorVista.Font = Tema.Fuente(10);
            marcadorVista.ForeColor = Tema.TextoSuave;
            marcadorVista.BackColor = Tema.Lateral;
            marcadorVista.AccessibleName = "Estado de la vista previa de cámara";
            vista.Controls.Add(marcadorVista);
            tabla.Controls.Add(vista, 0, 5);
            tabla.Controls.Add(estado, 0, 6);

            var botones = new TableLayoutPanel
            {
                ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = new Padding(0)
            };
            botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actualizar.Dock = DockStyle.Fill;
            actualizar.Margin = new Padding(0, 0, 6, 0);
            actualizar.Click += (_, _) => Recargar();
            previsualizar.Dock = DockStyle.Fill;
            previsualizar.Margin = new Padding(6, 0, 0, 0);
            previsualizar.Click += VistaPrevia_Click;
            botones.Controls.Add(actualizar, 0, 0);
            botones.Controls.Add(previsualizar, 1, 0);
            tabla.Controls.Add(botones, 0, 7);

            tarjeta.Controls.Add(tabla);
            Controls.Add(tarjeta);
            reloj.Tick += (_, _) => MostrarCuadro();
            CargarOpciones(Array.Empty<DispositivoCamara>());
            MostrarEstado("La selección se guarda en este equipo.");
        }

        private Control ConstruirFormato()
        {
            var formato = config.FormatoCamara;
            var resoluciones = new List<Resolucion>
            {
                new(0, 0), new(640, 480), new(1280, 720), new(1920, 1080), new(3840, 2160)
            };
            var seleccion = new Resolucion(formato.Ancho, formato.Alto);
            if (!resoluciones.Contains(seleccion))
                resoluciones.Add(seleccion);

            var velocidades = new List<Velocidad> { new(0), new(15), new(24), new(30), new(60) };
            var velocidad = new Velocidad(formato.Fps);
            if (!velocidades.Contains(velocidad))
                velocidades.Add(velocidad);

            resolucion.DataSource = resoluciones;
            resolucion.SelectedItem = seleccion;
            fps.DataSource = velocidades;
            fps.SelectedItem = velocidad;
            resolucion.AccessibleName = "Resolución de cámara";
            fps.AccessibleName = "FPS de cámara";
            resolucion.SelectionChangeCommitted += GuardarSeleccion;
            fps.SelectionChangeCommitted += GuardarSeleccion;

            var fila = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty,
                BackColor = Tema.Tarjeta
            };
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            Control campoResolucion = Campo("Resolución", resolucion);
            campoResolucion.Margin = new Padding(0, 0, 6, 0);
            Control campoFps = Campo("FPS solicitados", fps);
            campoFps.Margin = new Padding(6, 0, 0, 0);
            fila.Controls.Add(campoResolucion, 0, 0);
            fila.Controls.Add(campoFps, 1, 0);
            return fila;
        }

        private static Control Campo(string titulo, ComboBox control)
        {
            var campo = Tema.TablaFilas(Tema.Tarjeta, 20, 0);
            campo.Controls.Add(Tema.Etiqueta(titulo, 9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            campo.Controls.Add(control, 0, 1);
            return campo;
        }

        public void Recargar()
        {
            if (!disponible || captura != null)
                return;

            try
            {
                var dispositivos = CatalogoCamaras.Listar();
                CargarOpciones(dispositivos);
                if (dispositivos.Count == 0)
                    MostrarEstado("Windows no muestra cámaras para elegir. Puedes probar Automática.", aviso: true);
                else if (selector.SelectedItem is Opcion { Conectada: false })
                    MostrarEstado("La cámara guardada no está conectada. Elige otra o vuelve a conectarla.", aviso: true);
                else
                    MostrarEstado("La selección se guarda en este equipo.");
            }
            catch (Exception ex)
            {
                CargarOpciones(Array.Empty<DispositivoCamara>());
                MostrarEstado("No se pudo listar las cámaras. " + ex.Message, aviso: true);
            }
            ActualizarBotones();
        }

        private void CargarOpciones(IReadOnlyList<DispositivoCamara> dispositivos)
        {
            var opciones = new List<Opcion> { new(null, "Automática") };
            foreach (DispositivoCamara dispositivo in dispositivos)
            {
                string nombre = dispositivos.Count(c => c.Nombre == dispositivo.Nombre) > 1
                    ? $"{dispositivo.Nombre} ({dispositivo.Indice + 1})"
                    : dispositivo.Nombre;
                opciones.Add(new Opcion(dispositivo.Id, nombre));
            }

            if (config.CamaraId != null && !opciones.Any(c => MismaCamara(c.Id)))
                opciones.Add(new Opcion(config.CamaraId, "Cámara guardada (no disponible)", Conectada: false));

            selector.DataSource = opciones;
            selector.SelectedItem = opciones.First(c => MismaCamara(c.Id));
        }

        private bool MismaCamara(string? id) => string.Equals(id, config.CamaraId, StringComparison.OrdinalIgnoreCase);

        private void GuardarSeleccion(object? sender, EventArgs e)
        {
            if (selector.SelectedItem is not Opcion { Conectada: true } opcion)
            {
                selector.SelectedItem = selector.Items.Cast<Opcion>().First(c => MismaCamara(c.Id));
                return;
            }

            try
            {
                var tamaño = (Resolucion)resolucion.SelectedItem!;
                var velocidad = (Velocidad)fps.SelectedItem!;
                var formato = new FormatoCamara(tamaño.Ancho, tamaño.Alto, velocidad.Fps);
                preferencia.Guardar(opcion.Id, formato);
                config.CamaraId = opcion.Id;
                config.FormatoCamara = formato;
                MostrarEstado("Ajustes de cámara guardados. Abre la vista previa para comprobar el formato.");
            }
            catch (Exception ex)
            {
                selector.SelectedItem = selector.Items.Cast<Opcion>().First(c => MismaCamara(c.Id));
                resolucion.SelectedItem = new Resolucion(config.FormatoCamara.Ancho, config.FormatoCamara.Alto);
                fps.SelectedItem = new Velocidad(config.FormatoCamara.Fps);
                MostrarEstado("No se pudo guardar la cámara. " + ex.Message, aviso: true);
            }
            ActualizarBotones();
        }

        public void Habilitar(bool valor)
        {
            disponible = valor;
            if (!valor)
            {
                DetenerVistaPrevia();
                MostrarEstado("Detén el monitoreo para cambiar o probar la cámara.");
            }
            else if (captura == null)
            {
                MostrarEstado("La selección se guarda en este equipo.");
            }
            ActualizarBotones();
        }

        public void DetenerVistaPrevia()
        {
            captura?.Cancel();
            reloj.Stop();
            LimpiarVista();
            marcadorVista.Text = "▣\r\n\r\nLa vista previa aparecerá aquí.";
            ActualizarBotones();
        }

        private async void VistaPrevia_Click(object? sender, EventArgs e)
        {
            if (captura != null)
            {
                DetenerVistaPrevia();
                MostrarEstado("Cerrando la cámara…");
                return;
            }

            if (!disponible || selector.SelectedItem is not Opcion { Conectada: true } opcion)
                return;

            var cancelacion = new CancellationTokenSource();
            captura = cancelacion;
            ActualizarBotones();
            MostrarEstado("Abriendo la cámara…");
            marcadorVista.Text = "Abriendo la cámara…";
            marcadorVista.Visible = true;
            reloj.Start();
            bool fallo = false;

            try
            {
                var formato = config.FormatoCamara;
                await HiloCapturaCamara.EjecutarAsync(() => Capturar(opcion.Id, formato, cancelacion.Token));
                if (!IsDisposed && disponible)
                    MostrarEstado("Vista previa detenida.");
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && disponible)
                    MostrarEstado("Vista previa detenida.");
            }
            catch (Exception ex)
            {
                fallo = true;
                if (!IsDisposed && disponible)
                {
                    MostrarEstado(ex.Message, aviso: true);
                    marcadorVista.Text = "No se pudo acceder a la cámara.\r\nRevisa la conexión y los permisos de Windows.";
                }
            }
            finally
            {
                captura = null;
                cancelacion.Dispose();
                if (!IsDisposed)
                {
                    reloj.Stop();
                    LimpiarVista();
                    if (!fallo)
                        marcadorVista.Text = "▣\r\n\r\nLa vista previa aparecerá aquí.";
                    ActualizarBotones();
                }
            }
        }

        private void Capturar(string? id, FormatoCamara formato, CancellationToken token)
        {
            try
            {
                using var camara = new CameraService(id, token, formato);
                if (!camara.IsOpened())
                    throw new InvalidOperationException("No se pudo abrir la cámara. Comprueba los permisos de Windows y cierra otras aplicaciones que la usen.");

                using var detector = new DetectorCalidadImagen();
                var desdeUltimaImagen = Stopwatch.StartNew();
                double fpsReportados = camara.FpsReportados;
                var intervaloCuadro = Stopwatch.StartNew();
                while (!token.IsCancellationRequested)
                {
                    using Mat frame = camara.GetFrame();
                    if (frame.Empty())
                    {
                        detector.Reiniciar();
                        if (desdeUltimaImagen.Elapsed >= TimeSpan.FromSeconds(2))
                            throw new InvalidOperationException("La cámara dejó de enviar imagen. Comprueba la conexión.");
                    }
                    else
                    {
                        desdeUltimaImagen.Restart();
                        var problema = detector.Observar(frame, DateTimeOffset.UtcNow, out _);
                        if (intervaloCuadro.ElapsedMilliseconds < 60)
                            continue;

                        intervaloCuadro.Restart();
                        Cv2.Flip(frame, frame, FlipMode.Y);
                        EstadoIluminacion iluminacion = MedidorIluminacion.Evaluar(frame);
                        Bitmap? imagen = ImageUtils.MatToBitmap(frame);
                        if (imagen != null)
                            Interlocked.Exchange(ref pendiente, new Cuadro(imagen, iluminacion, problema, fpsReportados))?.Imagen.Dispose();
                    }
                    if (frame.Empty())
                        token.WaitHandle.WaitOne(20);
                }
            }
            finally
            {
                if (token.IsCancellationRequested)
                    Interlocked.Exchange(ref pendiente, null)?.Imagen.Dispose();
            }
        }

        private void MostrarCuadro()
        {
            Cuadro? cuadro = Interlocked.Exchange(ref pendiente, null);
            if (cuadro == null)
                return;

            Image? anterior = vista.Image;
            vista.Image = cuadro.Imagen;
            marcadorVista.Visible = false;
            anterior?.Dispose();
            string formato = $"{cuadro.Imagen.Width} × {cuadro.Imagen.Height}";
            if (cuadro.FpsReportados > 0 && double.IsFinite(cuadro.FpsReportados))
                formato += $" · {cuadro.FpsReportados:0.#} FPS informados";

            string calidad = cuadro.Problema is TipoEventoSesion problema
                ? AvisosIluminacion.Mensaje(problema)
                : AvisosIluminacion.Mensaje(cuadro.Iluminacion);
            MostrarEstado(formato + Environment.NewLine + calidad,
                aviso: cuadro.Problema.HasValue || cuadro.Iluminacion != EstadoIluminacion.Adecuada);
        }

        private void LimpiarVista()
        {
            Interlocked.Exchange(ref pendiente, null)?.Imagen.Dispose();
            Image? imagen = vista.Image;
            vista.Image = null;
            marcadorVista.Visible = true;
            imagen?.Dispose();
        }

        private void ActualizarBotones()
        {
            selector.Enabled = actualizar.Enabled = resolucion.Enabled = fps.Enabled = disponible && captura == null;
            previsualizar.Text = captura == null ? "Ver vista previa" : "Detener vista previa";
            previsualizar.Enabled = disponible && (captura == null
                ? selector.SelectedItem is Opcion { Conectada: true }
                : !captura.IsCancellationRequested);
        }

        private void MostrarEstado(string mensaje, bool aviso = false)
        {
            estado.Text = mensaje;
            estado.ForeColor = aviso ? Tema.Ambar : Tema.TextoSuave;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                captura?.Cancel();
                reloj.Dispose();
                LimpiarVista();
            }
            base.Dispose(disposing);
        }

        private sealed record Opcion(string? Id, string Nombre, bool Conectada = true)
        {
            public override string ToString() => Nombre;
        }

        private sealed record Resolucion(int Ancho, int Alto)
        {
            public override string ToString() => Ancho == 0 ? "Automática" : $"{Ancho} × {Alto}";
        }

        private sealed record Velocidad(int Fps)
        {
            public override string ToString() => Fps == 0 ? "Automáticos" : Fps.ToString();
        }

        private sealed record Cuadro(Bitmap Imagen, EstadoIluminacion Iluminacion,
            TipoEventoSesion? Problema, double FpsReportados);
    }
}
