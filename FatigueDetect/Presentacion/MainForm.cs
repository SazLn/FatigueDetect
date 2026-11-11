using System;
using System.Drawing;
using System.IO;
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
        private readonly BotonNavegacion btnHistorial = new BotonNavegacion("Historial de alertas");
        private readonly BotonNavegacion btnConfig = new BotonNavegacion("Configuración");

        private readonly PaginaInicio paginaInicio;
        private readonly PaginaRegistro paginaRegistro;
        private readonly PaginaHistorial paginaHistorial;
        private readonly PaginaConfiguracion paginaConfig;

        // Sesión en curso (null cuando no se está monitoreando)
        private SesionConduccion? sesionActiva;
        private DetectionController? controlador;
        private CameraService? camara;

        private FaceDetector? detectorRostros;
        private EyeDetector? detectorOjos;

        public MainForm(SesionService sesionService, CatalogoTiposCarro catalogo, Configuracion config)
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
            paginaRegistro = new PaginaRegistro(catalogo);
            paginaHistorial = new PaginaHistorial(sesionService);
            paginaConfig = new PaginaConfiguracion(config);

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
            btnConfig.Click += (remitente, evento) => Mostrar(paginaConfig, btnConfig);

            paginaRegistro.RegistroConfirmado += IniciarSesion;
            paginaInicio.DetenerSolicitado += () => DetenerSesion(true);
            paginaInicio.PausaSolicitada += pausa => controlador?.SetPaused(pausa);

            FormClosing += (remitente, evento) =>
            {
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

        private void IniciarSesion(string cedula, string nombre, string apellidos, string tipo, string placa)
        {
            if (sesionActiva != null)
            {
                MessageBox.Show(this,
                    "Ya hay una sesión en curso. Deténla desde la pestaña Inicio antes de registrar otro conductor.",
                    "Sesión en curso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 1. Cámara
            var camaraNueva = new CameraService();
            if (!camaraNueva.IsOpened())
            {
                camaraNueva.Dispose();
                MessageBox.Show(this,
                    "No se pudo abrir la cámara.\n\n" +
                    "• Cierra otras aplicaciones que la usen (Zoom, Teams, navegador).\n" +
                    "• Revisa en Windows: Configuración > Privacidad y seguridad > Cámara, " +
                    "y activa \"Permitir que las aplicaciones de escritorio accedan a la cámara\".",
                    "Cámara no disponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Clasificadores (se cargan una sola vez)
            try
            {
                string rutaCascadas = Path.Combine(AppContext.BaseDirectory, "Resources", "cascades");
                detectorRostros ??= new FaceDetector(Path.Combine(rutaCascadas, "haarcascade_frontalface_default.xml"));
                detectorOjos ??= new EyeDetector(Path.Combine(rutaCascadas, "haarcascade_eye_tree_eyeglasses.xml"));
            }
            catch (Exception ex)
            {
                camaraNueva.Dispose();
                MessageBox.Show(this, "No se pudieron cargar los clasificadores:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 3. Sesión
            string tipoCarro = catalogo.Registrar(tipo);   // si es nuevo, queda guardado
            paginaRegistro.RecargarTipos();
            SesionConduccion sesion = sesionService.Iniciar(cedula, nombre, apellidos, tipoCarro, placa);

            camara = camaraNueva;
            sesionActiva = sesion;
            controlador = new DetectionController(
                camaraNueva, paginaInicio, detectorRostros!, detectorOjos!,
                new DrowsinessLogic(config), new AlarmService(config), sesion);

            paginaInicio.IniciarSesion(sesion);
            paginaRegistro.Limpiar();
            Mostrar(paginaInicio, btnInicio);

            controlador.Iniciar();
        }

        private void DetenerSesion(bool mostrarHistorial)
        {
            if (sesionActiva == null)
            {
                return;
            }

            SesionConduccion sesion = sesionActiva;

            controlador?.Detener();   // para el hilo y la alarma
            camara?.Dispose();        // apaga la cámara
            controlador = null;
            camara = null;
            sesionActiva = null;

            paginaInicio.FinalizarSesion();

            try
            {
                sesionService.Finalizar(sesion);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "No se pudo guardar el historial (¿está abierto en Excel?):\n" + ex.Message,
                    "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (mostrarHistorial)
            {
                paginaHistorial.Recargar(sesion);
                Mostrar(paginaHistorial, btnHistorial);
            }
        }
    }
}
