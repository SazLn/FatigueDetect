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

        private readonly PanelDobleBuffer contenido = new PanelDobleBuffer();
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
        private bool repintadoPendiente;

        public MainForm(SesionService sesionService, CatalogoTiposCarro catalogo, Configuracion config,
            ServicioBiometrico servicioBiometrico, PreferenciaCamara preferenciaCamara,
            ConfiguracionDeteccionRepository repositorioAjustes,
            ProteccionDatosService proteccionDatos, PoliticaProteccionRepository politicaDatos)
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
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            paginaInicio = new PaginaInicio(config);
            paginaRegistro = new PaginaRegistro(catalogo, servicioBiometrico);
            paginaHistorial = new PaginaHistorial(sesionService);
            paginaConfig = new PaginaConfiguracion(config, preferenciaCamara, repositorioAjustes,
                proteccionDatos, politicaDatos);

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

            Shown += (_, _) => AjustarVentanaAlAreaUtil();
            SizeChanged += (_, _) => SolicitarRepintadoCompleto();

            Mostrar(paginaRegistro, btnRegistro);
        }

        private void AjustarVentanaAlAreaUtil()
        {
            if (WindowState != FormWindowState.Normal)
                return;

            Rectangle area = Screen.FromControl(this).WorkingArea;
            int ancho = Math.Min(Width, area.Width);
            int alto = Math.Min(Height, area.Height);
            ancho = Math.Max(Math.Min(MinimumSize.Width, area.Width), ancho);
            alto = Math.Max(Math.Min(MinimumSize.Height, area.Height), alto);
            Bounds = new Rectangle(
                area.Left + Math.Max(0, (area.Width - ancho) / 2),
                area.Top + Math.Max(0, (area.Height - alto) / 2),
                ancho,
                alto);
        }

        private void SolicitarRepintadoCompleto()
        {
            if (!IsHandleCreated || IsDisposed || repintadoPendiente)
                return;

            repintadoPendiente = true;
            BeginInvoke(() =>
            {
                repintadoPendiente = false;
                if (IsDisposed)
                    return;
                contenido.PerformLayout();
                contenido.Invalidate(true);
            });
        }

        private Control ConstruirMenu()
        {
            var lateral = new Panel
            {
                Dock = DockStyle.Left,
                Width = 236,
                BackColor = Tema.Lateral,
                Padding = new Padding(10, 14, 10, 14)
            };

            var tabla = Tema.TablaFilas(Tema.Lateral, 96, 214, 0, 64);

            var marca = Tema.TablaFilas(Tema.Lateral, 56, 22);
            Label nombre = Tema.Etiqueta("Sistema de detección de\nfatiga al volante", 11, Tema.Texto,
                Tema.Lateral, FontStyle.Bold);
            Label lema = Tema.Etiqueta("Seguridad vial inteligente", 8.5f, Tema.TextoSuave, Tema.Lateral);
            nombre.Padding = lema.Padding = new Padding(10, 0, 0, 0);
            marca.Controls.Add(nombre, 0, 0);
            marca.Controls.Add(lema, 0, 1);
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
                boton.Width = 216;
                boton.Margin = new Padding(0, 0, 0, 8);
                menu.Controls.Add(boton);
            }
            tabla.Controls.Add(menu, 0, 1);

            Label pie = Tema.Etiqueta("Conduce seguro.\nLlega seguro.", 9.5f, Tema.TextoSuave,
                Tema.Lateral, FontStyle.Italic);
            pie.Padding = new Padding(10, 0, 0, 0);
            tabla.Controls.Add(pie, 0, 3);

            lateral.Controls.Add(tabla);
            return lateral;
        }

        private void Mostrar(Control pagina, BotonNavegacion boton)
        {
            if (!ReferenceEquals(pagina, paginaConfig) && paginaConfig.Visible)
                paginaConfig.DetenerVistaPrevia();

            SuspendLayout();
            contenido.SuspendLayout();
            foreach (Control c in contenido.Controls)
            {
                c.Visible = ReferenceEquals(c, pagina);
            }
            pagina.BringToFront();

            btnInicio.Activo = ReferenceEquals(boton, btnInicio);
            btnRegistro.Activo = ReferenceEquals(boton, btnRegistro);
            btnHistorial.Activo = ReferenceEquals(boton, btnHistorial);
            btnConfig.Activo = ReferenceEquals(boton, btnConfig);
            contenido.ResumeLayout(true);
            ResumeLayout(true);
            SolicitarRepintadoCompleto();
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
                string modeloFacial = Path.Combine(AppContext.BaseDirectory, "Resources", "modelos", "face-landmark.onnx");

                string placaNormalizada = ValidadorRegistro.NormalizarPlaca(placa);
                PerfilPersonalConductor? perfil = sesionService.ObtenerPerfil(cedula, config.CamaraId, placaNormalizada);
                using var calibracion = new VentanaCalibracionGuiada(config, placaNormalizada, perfil);
                if (calibracion.ShowDialog(this) != DialogResult.OK || calibracion.Resultado is null)
                    return;

                UmbralesPersonalizados umbrales = PersonalizacionConductor.CalcularUmbrales(perfil, config.Deteccion);
                SesionConduccion sesion = sesionService.Iniciar(cedula, nombre, apellidos, tipo, placa,
                    config.CamaraId, calibracion.Resultado, umbrales);
                preparando = new DetectionController(
                    paginaInicio, detectorRostros, modeloFacial,
                    new DrowsinessLogic(config, umbrales), new AlarmService(config), sesion,
                    calibracion.Resultado);
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

        internal static string FormatearPostura(SesionConduccion sesion, MetricasPostura lectura)
        {
            string tiempo = lectura.TiempoPoseMs > 0
                ? $"{lectura.TiempoFueraFrenteMs / 1000.0:0.0} s · Máximo {lectura.DistraccionMaximaMs / 1000.0:0.0} s"
                : "—";
            return $"Fuera del frente: {tiempo} · {lectura.ProporcionDistraida(sesion.Calidad?.TiempoActivoMs)?.ToString("P1") ?? "—"} de sesión activa\n" +
                $"Pose observada: {lectura.TiempoPoseMs / 1000.0:0.0} s · Distancia aprox.: {lectura.DistanciaMediaCm?.ToString("0") ?? "—"} cm\n" +
                $"Posibles bostezos: {(lectura.TiempoBocaMs > 0 ? lectura.Bostezos.ToString() : "—")} · " +
                $"{lectura.BostezosPorHora?.ToString("0.0") ?? "—"}/h observada";
        }

        private void MostrarResumenFinSesion(SesionConduccion sesion, string? errorGuardado)
        {
            var eventos = sesion.Eventos;
            int ausencias = eventos.Count(evento => evento.Tipo == TipoEventoSesion.RostroNoDetectado);
            var destacados = eventos
                .Where(evento => evento.Tipo is not (TipoEventoSesion.Alarma or TipoEventoSesion.RostroNoDetectado
                    or TipoEventoSesion.CierreOcularProlongado or TipoEventoSesion.CierreOcularAnomalo
                    or TipoEventoSesion.Cabeceo))
                .GroupBy(evento => evento.Tipo)
                .OrderBy(grupo => grupo.Key)
                .Select(grupo => $"• {PaginaHistorial.NombreEvento(grupo.Key)}: {grupo.Count()}")
                .ToArray();

            string mensaje = $"Duración: {Tema.FormatoDuracion(sesion.Duracion)}\n" +
                $"Alarmas: {sesion.CantidadAlarmas}\n" +
                $"Episodios sin rostro detectado: {ausencias}\n" +
                (sesion.Calidad is CalidadSesion calidad
                    ? $"\nCalidad de detección {calidad.Puntaje?.ToString("0.0") ?? "—"}/100\n" +
                      $"Vídeo: {calidad.Fps?.ToString("0.0") ?? "—"} FPS · Caídas: {calidad.CaidasFps?.ToString() ?? "—"}" +
                      $" · FPS bajos: {(calidad.TiempoFpsBajoMs / 1000.0)?.ToString("0.0") ?? "—"} s\n" +
                      $"Fotogramas analizables: {calidad.ProporcionAnalizable?.ToString("P0") ?? "—"}" +
                      $" · Cobertura facial: {calidad.CoberturaRostro?.ToString("P0") ?? "—"}" +
                      $" · Fallos de cámara: {calidad.FallosCamara}\n" +
                      $"Cámara/entorno: {calidad.PuntajeCamaraEntorno?.ToString("0.0") ?? "—"}/100" +
                      $" · Colocación: {calidad.PuntajeColocacion?.ToString("0.0") ?? "—"}/100\n" +
                      $"Brillo {calidad.BrilloPromedio?.ToString("0") ?? "—"} · Contraste {calidad.ContrastePromedio?.ToString("0") ?? "—"}" +
                      $" · Nitidez {calidad.NitidezPromedio?.ToString("0") ?? "—"} · Posición {calidad.PosicionFacialPromedio?.ToString("0") ?? "—"}\n" +
                      calidad.ExplicarReducciones() + "\n"
                    : "\nCalidad sin medición\n") +
                $"\nAtención (indicador ocular)\nOjos abiertos durante el tiempo observado: {sesion.OjosAbiertosObservados?.ToString("P1") ?? "—"}" +
                $" · Cobertura ocular: {sesion.CoberturaOcular?.ToString("P0") ?? "—"}\n" +
                (sesion.MetricasFatiga is MetricasFatiga fatiga
                    ? $"Parpadeos: {fatiga.Parpadeos} ({fatiga.ParpadeosPorMinuto?.ToString("0.0") ?? "—"}/min)" +
                      $" · Microsueños: {fatiga.Microsuenos}\n" +
                      $"Cortos: {fatiga.ParpadeosCortos} · Prolongados: {fatiga.ParpadeosProlongados}" +
                      $" · Cierres anómalos: {fatiga.CierresAnomalos}\n" +
                      $"PERCLOS: {fatiga.Perclos?.ToString("P1") ?? "—"} · Tiempo cerrado: {fatiga.TiempoOcularCerradoMs / 1000.0:0.0} s\n" +
                      $"Cierre más largo: {fatiga.CierreMasLargoMs / 1000.0:0.000} s" +
                      $" · Posibles cabeceos: {fatiga.Cabeceos}\n"
                    : "") + "\n" +
                (sesion.Postura is { } postura ? FormatearPostura(sesion, postura) + "\n\n" : "") +
                (destacados.Length == 0 ? "Sin otros eventos destacados."
                    : "Otros eventos destacados\n" + string.Join("\n", destacados));

            mensaje += "\n\n" + AnalisisAtencion.Resumir(sesion.Atencion);
            mensaje += "\n\n" + PersonalizacionConductor.Resumir(sesion.ComparacionPersonal);
            mensaje += "\n\n" + AnalisisConfiabilidadEvento.Resumir(sesion);
            if (errorGuardado is not null)
                mensaje += "\n\n" + errorGuardado;

            using var resumenAtencion = new VentanaInformeAtencion(sesion, mensaje);
            resumenAtencion.ShowDialog(this);
        }
    }
}
