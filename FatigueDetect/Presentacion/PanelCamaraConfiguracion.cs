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
        private readonly Button actualizar = Tema.Boton("Actualizar lista", Tema.TarjetaClara, Tema.Texto);
        private readonly Button previsualizar = Tema.Boton("Ver vista previa", Tema.Azul, Color.White);
        private readonly PictureBox vista = new PictureBox();
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
            Margin = new Padding(8, 0, 0, 0);
            BackColor = Tema.Fondo;

            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 36, 34, 0, 44, 49);
            tabla.Controls.Add(Tema.Etiqueta("Cámara", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            selector.DropDownStyle = ComboBoxStyle.DropDownList;
            selector.FlatStyle = FlatStyle.Flat;
            selector.BackColor = Tema.TarjetaClara;
            selector.ForeColor = Tema.Texto;
            selector.Font = Tema.Fuente(10);
            selector.Dock = DockStyle.Fill;
            selector.Margin = new Padding(0);
            selector.AccessibleName = "Seleccionar cámara";
            selector.SelectionChangeCommitted += GuardarSeleccion;
            tabla.Controls.Add(selector, 0, 1);

            vista.Dock = DockStyle.Fill;
            vista.SizeMode = PictureBoxSizeMode.Zoom;
            vista.BackColor = Tema.Fondo;
            vista.Margin = new Padding(0, 2, 0, 2);
            tabla.Controls.Add(vista, 0, 2);
            tabla.Controls.Add(estado, 0, 3);

            var botones = new TableLayoutPanel
            {
                ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = new Padding(0)
            };
            botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actualizar.Dock = DockStyle.Fill;
            actualizar.Click += (_, _) => Recargar();
            previsualizar.Dock = DockStyle.Fill;
            previsualizar.Margin = new Padding(0);
            previsualizar.Click += VistaPrevia_Click;
            botones.Controls.Add(actualizar, 0, 0);
            botones.Controls.Add(previsualizar, 1, 0);
            tabla.Controls.Add(botones, 0, 4);

            tarjeta.Controls.Add(tabla);
            Controls.Add(tarjeta);
            reloj.Tick += (_, _) => MostrarCuadro();
            CargarOpciones(Array.Empty<DispositivoCamara>());
            MostrarEstado("La selección se guarda en este equipo.");
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
                preferencia.Guardar(opcion.Id);
                config.CamaraId = opcion.Id;
                MostrarEstado("Cámara guardada.");
            }
            catch (Exception ex)
            {
                selector.SelectedItem = selector.Items.Cast<Opcion>().First(c => MismaCamara(c.Id));
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
            reloj.Start();

            try
            {
                await HiloCapturaCamara.EjecutarAsync(() => Capturar(opcion.Id, cancelacion.Token));
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
                if (!IsDisposed && disponible)
                    MostrarEstado(ex.Message, aviso: true);
            }
            finally
            {
                captura = null;
                cancelacion.Dispose();
                if (!IsDisposed)
                {
                    reloj.Stop();
                    LimpiarVista();
                    ActualizarBotones();
                }
            }
        }

        private void Capturar(string? id, CancellationToken token)
        {
            try
            {
                using var camara = new CameraService(id, token);
                if (!camara.IsOpened())
                    throw new InvalidOperationException("No se pudo abrir la cámara. Comprueba los permisos de Windows y cierra otras aplicaciones que la usen.");

                var desdeUltimaImagen = Stopwatch.StartNew();
                while (!token.IsCancellationRequested)
                {
                    using Mat frame = camara.GetFrame();
                    if (frame.Empty())
                    {
                        if (desdeUltimaImagen.Elapsed >= TimeSpan.FromSeconds(2))
                            throw new InvalidOperationException("La cámara dejó de enviar imagen. Comprueba la conexión.");
                    }
                    else
                    {
                        desdeUltimaImagen.Restart();
                        Cv2.Flip(frame, frame, FlipMode.Y);
                        EstadoIluminacion iluminacion = MedidorIluminacion.Evaluar(frame);
                        Bitmap? imagen = ImageUtils.MatToBitmap(frame);
                        if (imagen != null)
                            Interlocked.Exchange(ref pendiente, new Cuadro(imagen, iluminacion))?.Imagen.Dispose();
                    }
                    token.WaitHandle.WaitOne(60);
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
            anterior?.Dispose();
            MostrarEstado(AvisosIluminacion.Mensaje(cuadro.Iluminacion),
                aviso: cuadro.Iluminacion != EstadoIluminacion.Adecuada);
        }

        private void LimpiarVista()
        {
            Interlocked.Exchange(ref pendiente, null)?.Imagen.Dispose();
            Image? imagen = vista.Image;
            vista.Image = null;
            imagen?.Dispose();
        }

        private void ActualizarBotones()
        {
            selector.Enabled = actualizar.Enabled = disponible && captura == null;
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

        private sealed record Cuadro(Bitmap Imagen, EstadoIluminacion Iluminacion);
    }
}
