using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    /// <summary>Ventana principal: menú lateral + pantallas. También controla el ciclo de cada sesión.</summary>
    public class MainForm : Form
    {
        private readonly SesionService sesionService;
        private readonly CatalogoTiposCarro catalogo;
        private readonly Configuracion config;

        private readonly Panel contenido = new Panel();
        private readonly BotonNavegacion btnInicio = new BotonNavegacion("Inicio");
        private readonly BotonNavegacion btnRegistro = new BotonNavegacion("Registro / Persona");
        private readonly BotonNavegacion btnHistorial = new BotonNavegacion("Historial de sesiones");
        private readonly BotonNavegacion btnConfig = new BotonNavegacion("Configuración");

        private readonly PaginaInicio paginaInicio;
        private readonly PaginaRegistro paginaRegistro;
        private readonly PaginaHistorial paginaHistorial;
        private readonly PaginaConfiguracion paginaConfig;

        // Sesión en curso (null cuando no se está monitoreando)
        private SesionConduccion? sesionActiva;
        private DetectionController? controlador;
        private CancellationTokenSource? arranqueCamara;

        private FaceDetector? detectorRostros;
        private EyeDetector? detectorOjos;

        public MainForm(SesionService sesionService, CatalogoTiposCarro catalogo, Configuracion config,
            ServicioBiometrico servicioBiometrico, PreferenciaCamara preferenciaCamara,
            ConfiguracionDeteccionRepository repositorioAjustes)
        {
            this.sesionService = sesionService;
            this.catalogo = catalogo;
            this.config = config;

            Text = "Sistema de Detección de Fatiga al Volante";
            Size = new Size(1320, 780);
            MinimumSize = new Size(1150, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Tema.Fondo;
            Font = Tema.Fuente(10);

            paginaInicio = new PaginaInicio(config);
            paginaRegistro = new PaginaRegistro(catalogo, servicioBiometrico);
            paginaHistorial = new PaginaHistorial(sesionService);
            paginaConfig = new PaginaConfiguracion(config, preferenciaCamara, repositorioAjustes);

            contenido.Dock = DockStyle.Fill;
            contenido.BackColor = Tema.Fondo;
            contenido.Controls.Add(paginaInicio);
            contenido.Controls.Add(paginaRegistro);
            contenido.Controls.Add(paginaHistorial);
            contenido.Controls.Add(paginaConfig);

            // Primero el contenido (Fill) y después el menú (Left).
            Controls.Add(contenido);
            Controls.Add(ConstruirMenu());

            btnInicio.Click += (remitente, evento) => Mostrar(paginaInicio, btnInicio);
            btnRegistro.Click += (remitente, evento) => Mostrar(paginaRegistro, btnRegistro);
            btnHistorial.Click += (remitente, evento) =>
            {
                paginaHistorial.Recargar(null);
                Mostrar(paginaHistorial, btnHistorial);
            };
            btnConfig.Click += (remitente, evento) =>
            {
                Mostrar(paginaConfig, btnConfig);
                paginaConfig.ActualizarCamaras();
            };

            paginaRegistro.RegistroConfirmado += IniciarSesion;
            paginaInicio.DetenerSolicitado += () => DetenerSesion(true);
            paginaInicio.PausaSolicitada += pausa => controlador?.SetPaused(pausa);

            FormClosing += (remitente, evento) =>
            {
                arranqueCamara?.Cancel();
                paginaConfig.DetenerVistaPrevia();
                // Cerrar la ventana también termina y guarda la sesión en curso.
                if (sesionActiva != null)
                {
                    DetenerSesion(false);
                }
            };

            Mostrar(paginaRegistro, btnRegistro);
        }

        private Control ConstruirMenu()
        {
            var lateral = new Panel
            {
                Dock = DockStyle.Left,
                Width = 236,
                BackColor = Tema.Lateral,
                Padding = new Padding(16, 18, 16, 14)
            };

            var tabla = Tema.TablaFilas(Tema.Lateral, 96, 214, 0, 64);

            var marca = Tema.TablaFilas(Tema.Lateral, 56, 22);
            marca.Controls.Add(Tema.Etiqueta("Sistema de Detección de Fatiga al Volante", 11, Tema.Texto, Tema.Lateral, FontStyle.Bold), 0, 0);
            marca.Controls.Add(Tema.Etiqueta("Seguridad vial inteligente", 8.5f, Tema.TextoSuave, Tema.Lateral), 0, 1);
            tabla.Controls.Add(marca, 0, 0);

            var menu = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Tema.Lateral,
                Margin = new Padding(0)
            };
            foreach (BotonNavegacion boton in new[] { btnInicio, btnRegistro, btnHistorial, btnConfig })
            {
                boton.Width = 202;
                boton.Margin = new Padding(0, 0, 0, 8);
                menu.Controls.Add(boton);
            }
            tabla.Controls.Add(menu, 0, 1);

            tabla.Controls.Add(Tema.Etiqueta("Conduce seguro.\nLlega seguro.", 9.5f, Tema.TextoSuave, Tema.Lateral,
                FontStyle.Italic), 0, 3);

            lateral.Controls.Add(tabla);
            return lateral;
        }

        private void Mostrar(Control pagina, BotonNavegacion boton)
        {
            if (!ReferenceEquals(pagina, paginaConfig) && paginaConfig.Visible)
                paginaConfig.DetenerVistaPrevia();

            foreach (Control c in contenido.Controls)
            {
                c.Visible = ReferenceEquals(c, pagina);
            }
            pagina.BringToFront();

            btnInicio.Activo = ReferenceEquals(boton, btnInicio);
            btnRegistro.Activo = ReferenceEquals(boton, btnRegistro);
            btnHistorial.Activo = ReferenceEquals(boton, btnHistorial);
            btnConfig.Activo = ReferenceEquals(boton, btnConfig);
        }

        // ------------------------------------------------------------------
        //  Ciclo de una sesión
        // ------------------------------------------------------------------

        private async void IniciarSesion(string cedula, string nombre, string apellidos, string tipo, string placa)
        {
            if (arranqueCamara != null)
                return;

            if (sesionActiva != null)
            {
                MessageBox.Show(this,
                    "Ya hay una sesión en curso. Deténla desde la pestaña Inicio antes de registrar otro conductor.",
                    "Sesión en curso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var cancelacion = new CancellationTokenSource();
            arranqueCamara = cancelacion;
            paginaConfig.HabilitarCamara(false);
            DetectionController? preparando = null;
            try
            {
                string rutaCascadas = Path.Combine(AppContext.BaseDirectory, "Resources", "cascades");
                detectorRostros ??= new FaceDetector(Path.Combine(rutaCascadas, "haarcascade_frontalface_default.xml"));
                detectorOjos ??= new EyeDetector(Path.Combine(rutaCascadas, "haarcascade_eye_tree_eyeglasses.xml"));

                SesionConduccion sesion = sesionService.Iniciar(cedula, nombre, apellidos, tipo, placa);
                preparando = new DetectionController(
                    paginaInicio, detectorRostros, detectorOjos,
                    new DrowsinessLogic(config), new AlarmService(config), sesion);
                await preparando.IniciarAsync(config.CamaraId, cancelacion.Token, config.FormatoCamara);
                cancelacion.Token.ThrowIfCancellationRequested();

                sesion.Vehiculo.TipoCarro = catalogo.Registrar(tipo);
                paginaRegistro.RecargarTipos();
                paginaInicio.IniciarSesion(sesion);
                paginaRegistro.Limpiar();
                Mostrar(paginaInicio, btnInicio);

                controlador = preparando;
                sesionActiva = sesion;
                preparando = null;
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                // Se cerró la ventana mientras se abría la cámara.
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !cancelacion.IsCancellationRequested)
                    MessageBox.Show(this, ex.Message, "No se pudo iniciar el monitoreo",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                preparando?.Detener();
                arranqueCamara = null;
                if (sesionActiva == null && !IsDisposed && !cancelacion.IsCancellationRequested)
                    paginaConfig.HabilitarCamara(true);
            }
        }

        private void DetenerSesion(bool mostrarHistorial)
        {
            if (sesionActiva == null)
            {
                return;
            }

            SesionConduccion sesion = sesionActiva;

            controlador?.Detener();
            controlador = null;
            sesionActiva = null;
            paginaConfig.HabilitarCamara(true);

            paginaInicio.FinalizarSesion();

            string? errorGuardado = null;
            try
            {
                sesionService.Finalizar(sesion);
            }
            catch (Exception ex)
            {
                errorGuardado = "No se pudo guardar la sesión en el historial.\n" + ex.Message;
            }

            if (mostrarHistorial)
            {
                MostrarResumenFinSesion(sesion, errorGuardado);
                if (errorGuardado is null)
                {
                    paginaHistorial.Recargar(sesion);
                    Mostrar(paginaHistorial, btnHistorial);
                }
            }
            else if (errorGuardado is not null)
            {
                MessageBox.Show(this, errorGuardado, "Error al guardar",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MostrarResumenFinSesion(SesionConduccion sesion, string? errorGuardado)
        {
            var eventos = sesion.Eventos;
            int ausencias = eventos.Count(evento => evento.Tipo == TipoEventoSesion.RostroNoDetectado);
            var destacados = eventos
                .Where(evento => evento.Tipo is not (TipoEventoSesion.Alarma or TipoEventoSesion.RostroNoDetectado))
                .GroupBy(evento => evento.Tipo)
                .OrderBy(grupo => grupo.Key)
                .Select(grupo => $"• {PaginaHistorial.NombreEvento(grupo.Key)}: {grupo.Count()}")
                .ToArray();

            string mensaje = $"Duración: {Tema.FormatoDuracion(sesion.Duracion)}\n" +
                $"Alarmas: {sesion.CantidadAlarmas}\n" +
                $"Episodios sin rostro detectado: {ausencias}\n\n" +
                (destacados.Length == 0 ? "Sin otros eventos destacados."
                    : "Otros eventos destacados\n" + string.Join("\n", destacados));

            if (errorGuardado is not null)
                mensaje += "\n\n" + errorGuardado;

            MessageBox.Show(this, mensaje, "Resumen de la sesión", MessageBoxButtons.OK,
                errorGuardado is null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }
}
