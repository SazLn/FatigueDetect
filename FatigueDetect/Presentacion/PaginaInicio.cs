using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    /// <summary>Pantalla "Inicio": cámara en vivo, estado del conductor, gráfica y alertas.</summary>
    public class PaginaInicio : UserControl, IDetectorView
    {
        private const int MuestreoMs = 500;

        private readonly Configuracion config;
        private SesionConduccion? sesion;
        private volatile bool pausado;
        private int actualizando;                       // 0 = libre, 1 = actualizando la pantalla
        private DateTime ultimoMuestreo = DateTime.MinValue;
        private int alarmasMostradas = -1;
        private EyeState? estadoMostrado;
        private TipoEventoSesion? rostroMostrado;
        private NivelAlertaProgresiva? nivelAlertaMostrado;
        private string explicacionAlertaMostrada = "";
        private long? recuperacionAlertaMostrada;

        public event Action? DetenerSolicitado;
        public event Action<bool>? PausaSolicitada;

        // Cámara
        private readonly PictureBox camaraBox = new PictureBox();
        private Label sinSenal = null!;
        private Label avisoCamara = null!;
        private Label metricasFaciales = null!;
        private Label estadoCaptura = null!;
        private Label lblDuracion = null!;
        private Button pausarBtn = null!;
        private Button detenerBtn = null!;

        // Tarjeta de alerta
        private PanelRedondeado tarjetaAlerta = null!;
        private TableLayoutPanel tablaAlerta = null!;
        private readonly List<Control> fondosAlerta = new List<Control>();
        private Label alertaTitulo = null!;
        private Label alertaMensaje = null!;
        private Label alertaTiempo = null!;
        private Label capAlarma = null!;
        private BarraFatiga barra = null!;
        private Label lblConductor = null!;
        private Label lblCedula = null!;
        private Label lblVehiculo = null!;
        private Label lblAlarmas = null!;

        // Indicadores inferiores
        private GraficaLineas grafica = null!;
        private Medidor medidor = null!;
        private ListBox listaAlertas = null!;

        private readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer();

        public PaginaInicio(Configuracion config)
        {
            this.config = config;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(16);

            var raiz = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Tema.Fondo
            };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 67));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 33));

            Control camara = ConstruirCamara();
            raiz.Controls.Add(camara, 0, 0);
            raiz.SetColumnSpan(camara, 2);
            raiz.Controls.Add(ConstruirAlerta(), 2, 0);
            raiz.Controls.Add(ConstruirGrafica(), 0, 1);
            raiz.Controls.Add(ConstruirMedidor(), 1, 1);
            raiz.Controls.Add(ConstruirListaAlertas(), 2, 1);
            var pagina = Tema.TablaFilas(Tema.Fondo, 44, 0);
            pagina.Controls.Add(Tema.Etiqueta("Monitoreo en vivo", 19, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            pagina.Controls.Add(raiz, 0, 1);
            Controls.Add(pagina);

            reloj.Interval = 1000;
            reloj.Tick += (remitente, evento) =>
            {
                if (sesion != null)
                {
                    lblDuracion.Text = "Duración: " + Tema.FormatoDuracion(sesion.Duracion);
                }
            };

            FinalizarSesion();   // estado inicial: sin sesión
        }

        // ------------------------------------------------------------------
        //  Construcción de la pantalla
        // ------------------------------------------------------------------

        private Control ConstruirCamara()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(6) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 30, 0, 40, 92, 52);

            var cabecera = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Tema.Tarjeta,
                Margin = new Padding(0)
            };
            cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            estadoCaptura = Tema.Etiqueta("Cámara en espera", 10, Tema.TextoSuave, Tema.Tarjeta, FontStyle.Bold);
            cabecera.Controls.Add(estadoCaptura, 0, 0);
            lblDuracion = Tema.Etiqueta("Duración: 00:00:00", 10, Tema.TextoSuave, Tema.Tarjeta,
                FontStyle.Regular, ContentAlignment.MiddleRight);
            cabecera.Controls.Add(lblDuracion, 1, 0);
            tabla.Controls.Add(cabecera, 0, 0);

            camaraBox.Dock = DockStyle.Fill;
            camaraBox.SizeMode = PictureBoxSizeMode.Zoom;
            camaraBox.BackColor = Tema.Fondo;
            camaraBox.Margin = new Padding(0);
            sinSenal = Tema.Etiqueta("Sin señal de cámara", 11, Tema.TextoSuave, Tema.Fondo,
                FontStyle.Italic, ContentAlignment.MiddleCenter);
            camaraBox.Controls.Add(sinSenal);
            tabla.Controls.Add(camaraBox, 0, 1);

            avisoCamara = Tema.Etiqueta("", 10, Tema.Ambar, Tema.Tarjeta, FontStyle.Bold);
            avisoCamara.AccessibleName = "Avisos de cámara";
            tabla.Controls.Add(avisoCamara, 0, 2);
            metricasFaciales = Tema.Etiqueta("", 9, Tema.Texto, Tema.Tarjeta);
            tabla.Controls.Add(metricasFaciales, 0, 3);

            var botones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Tema.Tarjeta,
                Margin = new Padding(0),
                Padding = new Padding(0, 10, 0, 0)
            };
            detenerBtn = Tema.Boton("Detener monitoreo", Tema.Rojo, Color.White);
            detenerBtn.Width = 190;
            detenerBtn.Click += (remitente, evento) => DetenerSolicitado?.Invoke();

            pausarBtn = Tema.Boton("Pausar", Tema.TarjetaClara, Tema.Texto);
            pausarBtn.Width = 130;
            pausarBtn.Click += (remitente, evento) =>
            {
                pausado = !pausado;
                pausarBtn.Text = pausado ? "Reanudar" : "Pausar";
                avisoCamara.Text = "";
                metricasFaciales.Text = pausado ? "Medición facial en pausa" : "";
                estadoCaptura.Text = pausado ? "Cámara en pausa" : "Midiendo FPS…";
                estadoCaptura.ForeColor = Tema.TextoSuave;
                PausaSolicitada?.Invoke(pausado);
            };

            botones.Controls.Add(detenerBtn);
            botones.Controls.Add(pausarBtn);
            tabla.Controls.Add(botones, 0, 4);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private Control ConstruirAlerta()
        {
            tarjetaAlerta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(6) };
            tablaAlerta = Tema.TablaFilas(Tema.Tarjeta, 36, 55, 20, 42, 15, 22, 8, 22, 22, 22, 22, 0);

            alertaTitulo = Tema.Etiqueta("SIN SESIÓN", 13, Tema.TextoSuave, Tema.Tarjeta, FontStyle.Bold);
            alertaMensaje = Tema.Etiqueta("", 10, Tema.Texto, Tema.Tarjeta);
            Label cap1 = Tema.Etiqueta("Tiempo con ojos cerrados", 9, Tema.TextoSuave, Tema.Tarjeta);
            alertaTiempo = Tema.Etiqueta("0.0 s", 26, Tema.Texto, Tema.Tarjeta, FontStyle.Bold);
            barra = new BarraFatiga { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
            capAlarma = Tema.Etiqueta("", 9, Tema.TextoSuave, Tema.Tarjeta);
            var separador = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Borde, Margin = new Padding(0, 6, 0, 6) };
            lblConductor = Tema.Etiqueta("", 10, Tema.Texto, Tema.Tarjeta, FontStyle.Bold);
            lblCedula = Tema.Etiqueta("", 10, Tema.TextoSuave, Tema.Tarjeta);
            lblVehiculo = Tema.Etiqueta("", 10, Tema.TextoSuave, Tema.Tarjeta);
            lblAlarmas = Tema.Etiqueta("", 10, Tema.Texto, Tema.Tarjeta);

            tablaAlerta.Controls.Add(alertaTitulo, 0, 0);
            tablaAlerta.Controls.Add(alertaMensaje, 0, 1);
            tablaAlerta.Controls.Add(cap1, 0, 2);
            tablaAlerta.Controls.Add(alertaTiempo, 0, 3);
            tablaAlerta.Controls.Add(barra, 0, 4);
            tablaAlerta.Controls.Add(capAlarma, 0, 5);
            tablaAlerta.Controls.Add(separador, 0, 6);
            tablaAlerta.Controls.Add(lblConductor, 0, 7);
            tablaAlerta.Controls.Add(lblCedula, 0, 8);
            tablaAlerta.Controls.Add(lblVehiculo, 0, 9);
            tablaAlerta.Controls.Add(lblAlarmas, 0, 10);

            // Estos controles cambian de color de fondo junto con la tarjeta.
            fondosAlerta.AddRange(new Control[]
            {
                alertaTitulo, alertaMensaje, cap1, alertaTiempo, barra, capAlarma,
                lblConductor, lblCedula, lblVehiculo, lblAlarmas
            });

            tarjetaAlerta.Controls.Add(tablaAlerta);
            return tarjetaAlerta;
        }

        private Control ConstruirGrafica()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(6) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 28, 0);
            tabla.Controls.Add(Tema.Etiqueta("Estado de los ojos (tiempo con ojos cerrados)", 10.5f, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            grafica = new GraficaLineas(120) { Dock = DockStyle.Fill, Margin = new Padding(0) };
            tabla.Controls.Add(grafica, 0, 1);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private Control ConstruirMedidor()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(6) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 28, 0);
            tabla.Controls.Add(Tema.Etiqueta("Atención (0–100)", 10.5f, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            medidor = new Medidor { Dock = DockStyle.Fill, Margin = new Padding(0) };
            tabla.Controls.Add(medidor, 0, 1);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private Control ConstruirListaAlertas()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(6) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 28, 0);
            tabla.Controls.Add(Tema.Etiqueta("Alertas de esta sesión", 10.5f, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            listaAlertas = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Tema.Tarjeta,
                ForeColor = Tema.Texto,
                BorderStyle = BorderStyle.None,
                Font = Tema.Fuente(10),
                Margin = new Padding(0),
                IntegralHeight = false
            };
            tabla.Controls.Add(listaAlertas, 0, 1);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        // ------------------------------------------------------------------
        //  Sesión
        // ------------------------------------------------------------------

        public void IniciarSesion(SesionConduccion nueva)
        {
            sesion = nueva;
            pausado = false;
            estadoCaptura.Text = "Cámara en espera";
            estadoCaptura.ForeColor = Tema.TextoSuave;
            pausarBtn.Text = "Pausar";
            pausarBtn.Enabled = true;
            detenerBtn.Enabled = true;

            grafica.Limpiar();
            ultimoMuestreo = DateTime.MinValue;
            alarmasMostradas = -1;
            estadoMostrado = null;
            rostroMostrado = null;
            nivelAlertaMostrado = null;
            explicacionAlertaMostrada = "";
            recuperacionAlertaMostrada = null;

            lblConductor.Text = "Conductor: " + nueva.Conductor.NombreCompleto;
            lblCedula.Text = "C.C. " + nueva.Conductor.Cedula;
            lblVehiculo.Text = nueva.Vehiculo.TipoCarro + "  ·  Placa " + nueva.Vehiculo.Placa;
            lblAlarmas.Text = "Alarmas en esta sesión: 0";
            lblDuracion.Text = "Duración: 00:00:00";
            capAlarma.Text = "Alarma a los " +
                (nueva.UmbralesAplicados?.SegundosAlarma ?? config.Deteccion.SegundosAlarma).ToString("0.0") + " s";

            sinSenal.Text = "Iniciando cámara...";
            sinSenal.Visible = true;
            avisoCamara.Text = "";
            metricasFaciales.Text = nueva.UmbralesAplicados is { } umbrales
                ? $"Calibración guiada aplicada · Somnolencia {umbrales.SegundosSomnoliento:0.00} s · " +
                  $"Alarma {umbrales.SegundosAlarma:0.00} s\n{umbrales.Explicacion}"
                : "Mira al frente con los ojos abiertos para calibrar.";

            AplicarEstado(EyeState.DESPIERTO);
            medidor.Actualizar(0, Tema.TextoSuave, "—");
            ActualizarListaAlertas();
            reloj.Start();
        }

        public void FinalizarSesion()
        {
            reloj.Stop();
            sesion = null;
            pausado = false;
            estadoCaptura.Text = "Cámara en espera";
            estadoCaptura.ForeColor = Tema.TextoSuave;

            pausarBtn.Text = "Pausar";
            pausarBtn.Enabled = false;
            detenerBtn.Enabled = false;

            Image? vieja = camaraBox.Image;
            camaraBox.Image = null;
            vieja?.Dispose();

            sinSenal.Text = "Sin sesión activa.\nVe a \"Registro / Persona\" para iniciar el monitoreo.";
            sinSenal.Visible = true;
            avisoCamara.Text = "";
            metricasFaciales.Text = "";

            lblConductor.Text = "Conductor: —";
            lblCedula.Text = "C.C. —";
            lblVehiculo.Text = "Vehículo: —";
            lblAlarmas.Text = "Alarmas en esta sesión: —";
            lblDuracion.Text = "Duración: 00:00:00";
            capAlarma.Text = "Alarma a los " + config.Deteccion.SegundosAlarma.ToString("0.0") + " s";
            alertaTiempo.Text = "0.0 s";
            barra.Valor = 0;

            grafica.Limpiar();
            medidor.Actualizar(0, Tema.TextoSuave, "—");
            ActualizarListaAlertas();
            AplicarNeutral();
        }

        // ------------------------------------------------------------------
        //  IDetectorView (se llama desde el hilo de detección)
        // ------------------------------------------------------------------

        public void UpdateImage(Bitmap? image, EyeState estado, long tiempoMs,
            EstadoIluminacion? avisoIluminacion, TipoEventoSesion? avisoRostro,
            TipoEventoSesion? avisoCalidad = null, FatigaEnVivo? fatiga = null, double? fpsRecientes = null,
            MuestraAtencion? atencion = null, EstadoAlertaProgresiva? alerta = null,
            EvaluacionAccesorios? accesorios = null)
        {
            // Si la pantalla sigue ocupada con el cuadro anterior, se descarta este.
            if (IsDisposed || !IsHandleCreated || Interlocked.CompareExchange(ref actualizando, 1, 0) != 0)
            {
                image?.Dispose();
                return;
            }

            try
            {
                BeginInvoke(new Action(() => Actualizar(image, estado, tiempoMs, avisoIluminacion,
                    avisoRostro, avisoCalidad, fatiga, fpsRecientes, atencion, alerta, accesorios)));
            }
            catch (Exception)
            {
                // La ventana se está cerrando.
                Interlocked.Exchange(ref actualizando, 0);
                image?.Dispose();
            }
        }

        public bool IsPaused()
        {
            return pausado;
        }

        // ------------------------------------------------------------------
        //  Actualización de la pantalla (hilo de la interfaz)
        // ------------------------------------------------------------------

        private void Actualizar(Bitmap? imagen, EyeState estado, long tiempoMs,
            EstadoIluminacion? avisoIluminacion, TipoEventoSesion? avisoRostro,
            TipoEventoSesion? avisoCalidad, FatigaEnVivo? fatiga, double? fpsRecientes,
            MuestraAtencion? atencion, EstadoAlertaProgresiva? alerta, EvaluacionAccesorios? accesorios)
        {
            try
            {
                SesionConduccion? actual = sesion;
                if (actual == null || IsDisposed || pausado)
                {
                    imagen?.Dispose();
                    return;
                }

                if (imagen == null)
                {
                    sinSenal.Text = "Sin señal de cámara";
                    sinSenal.Visible = true;
                }
                else
                {
                    sinSenal.Visible = false;
                }

                Image? vieja = camaraBox.Image;
                camaraBox.Image = imagen;
                vieja?.Dispose();
                string avisoRostroTexto = avisoRostro switch
                {
                    TipoEventoSesion.RostroNoDetectado => "Rostro no detectado. Vuelve al encuadre.",
                    TipoEventoSesion.VariosRostros => "Hay varias personas en la imagen.",
                    _ => ""
                };
                string avisoLuzTexto = avisoIluminacion is EstadoIluminacion luz
                    ? AvisosIluminacion.Mensaje(luz) : "";
                string avisoAccesorios = accesorios is { Confiable: false } a
                    ? "Medición facial no confiable: " + a.Explicacion : "";
                string avisos = avisoCalidad is TipoEventoSesion problema ? AvisosIluminacion.Mensaje(problema) : string.Join(Environment.NewLine,
                    new[] { avisoRostroTexto, avisoLuzTexto, avisoAccesorios }.Where(texto => texto.Length > 0));
                if (avisoCamara.Text != avisos)
                    avisoCamara.Text = avisos;
                MostrarMetricasFaciales(fatiga, accesorios);
                bool fpsBajos = fpsRecientes < CalidadSesion.UmbralFps;
                string estadoVideo = imagen is null ? "Cámara sin señal"
                    : fpsRecientes is double fps ? $"{fps:0.0} FPS {(fpsBajos ? "· Fluidez baja" : "· Cámara activa")}"
                    : "Cámara activa · Midiendo FPS…";
                if (estadoCaptura.Text != estadoVideo)
                {
                    estadoCaptura.Text = estadoVideo;
                    estadoCaptura.ForeColor = imagen is null || fpsBajos ? Tema.Ambar : Tema.Verde;
                }

                if (imagen == null)
                {
                    AplicarSinSenal();
                    return;
                }

                if (avisoCalidad.HasValue)
                {
                    AplicarSinSenal("IMAGEN NO ANALIZABLE", avisos);
                    return;
                }

                if (fatiga?.AperturaIzquierda is null && avisoRostro is null)
                {
                    AplicarSinSenal("ESTADO OCULAR SIN MEDICIÓN", "Mira al frente con los ojos abiertos y revisa la iluminación.");
                    return;
                }

                AplicarEstado(estado, avisoRostro, alerta);

                double segundos = tiempoMs / 1000.0;
                alertaTiempo.Text = segundos.ToString("0.0") + " s";
                double umbralSomnolencia = actual.UmbralesAplicados?.SegundosSomnoliento
                    ?? config.Deteccion.SegundosSomnoliento;
                double umbralAlarma = actual.UmbralesAplicados?.SegundosAlarma
                    ?? config.Deteccion.SegundosAlarma;
                barra.Valor = segundos / Math.Max(0.1, umbralAlarma);
                capAlarma.Text = "Alarma a los " + umbralAlarma.ToString("0.0") + " s";

                grafica.UmbralSomnoliento = umbralSomnolencia;
                grafica.UmbralAlarma = umbralAlarma;
                if ((DateTime.Now - ultimoMuestreo).TotalMilliseconds >= MuestreoMs)
                {
                    grafica.AgregarValor(segundos);
                    ultimoMuestreo = DateTime.Now;
                }

                if (fatiga?.AperturaIzquierda is null)
                    medidor.Actualizar(0, Tema.TextoSuave, "—");
                else
                    ActualizarMedidor(atencion);

                int alarmas = actual.CantidadAlarmas;
                if (alarmas != alarmasMostradas)
                {
                    alarmasMostradas = alarmas;
                    lblAlarmas.Text = "Alarmas en esta sesión: " + alarmas;
                    ActualizarListaAlertas();
                }
            }
            finally
            {
                Interlocked.Exchange(ref actualizando, 0);
            }
        }

        private void MostrarMetricasFaciales(FatigaEnVivo? fatiga, EvaluacionAccesorios? accesorios = null)
        {
            if (fatiga is null)
            {
                metricasFaciales.Text = "Medición facial no disponible";
                return;
            }

            string pose = fatiga.Angulos is { } angulos
                ? $"Cabeza desde el frente: vertical {angulos.Vertical:0}° · horizontal {angulos.Horizontal:0}° · lateral {angulos.Lateral:0}°"
                : "Cabeza: —";
            PosturaEnVivo postura = fatiga.Postura;
            string orientacion = postura.Orientacion switch
            {
                DireccionCabeza.Frente => "frente",
                DireccionCabeza.Izquierda => "izquierda",
                DireccionCabeza.Derecha => "derecha",
                DireccionCabeza.Arriba => "arriba",
                DireccionCabeza.Abajo => "abajo",
                _ => "—"
            };
            string avisoPostura = postura.FueraEncuadre ? " · Rostro al borde"
                : postura.Encorvada ? " · Posible postura encorvada" : "";
            MetricasFatiga acumulado = fatiga.Acumulado;
            string visibilidad = accesorios is null ? ""
                : $"\nVisibilidad: ojos {accesorios.VisibilidadOjos:P0} · boca {accesorios.VisibilidadBoca:P0}" +
                  $" · confianza facial {accesorios.ConfiabilidadMedicion:P0}";
            metricasFaciales.Text =
                $"Apertura izq./der.: {fatiga.AperturaIzquierda?.ToString("P0") ?? "—"} / {fatiga.AperturaDerecha?.ToString("P0") ?? "—"}" +
                $" · diferencia {fatiga.DiferenciaApertura?.ToString("P0") ?? "—"} · PERCLOS 60 s {fatiga.Perclos?.ToString("P1") ?? "—"}\n" +
                $"Parpadeos {acumulado.Parpadeos} ({acumulado.ParpadeosPorMinuto?.ToString("0.0") ?? "—"}/min)" +
                $" · cortos {acumulado.ParpadeosCortos} · prolongados {acumulado.ParpadeosProlongados}\n" +
                $"Microsueños {acumulado.Microsuenos} · cierres anómalos {acumulado.CierresAnomalos}" +
                $" · cierre máx. {acumulado.CierreMasLargoMs / 1000.0:0.0} s\n{pose}\n" +
                $"Mirada estimada: {orientacion} · Distancia aprox.: {postura.DistanciaCm?.ToString("0") ?? "—"} cm" +
                $" · Bostezos: {postura.Bostezos}{avisoPostura}{visibilidad}";
        }

        private void ActualizarMedidor(MuestraAtencion? atencion)
        {
            double? valor = atencion?.Puntaje;
            medidor.Actualizar((valor ?? 0) / 100, valor is null ? Tema.TextoSuave
                : valor < 50 ? Tema.Rojo : valor < 75 ? Tema.Ambar : Tema.Verde,
                valor?.ToString("0") ?? "—");
            medidor.AccessibleDescription = valor.HasValue
                ? string.Join("; ", atencion!.Factores.Select(f => $"{f.Nombre}: menos {f.Descuento:0.0} puntos"))
                : "Sin medición suficiente de atención";
        }

        private void AplicarEstado(EyeState estado, TipoEventoSesion? avisoRostro = null,
            EstadoAlertaProgresiva? alerta = null)
        {
            string explicacion = alerta?.Explicacion ?? "";
            if (estadoMostrado == estado && rostroMostrado == avisoRostro &&
                nivelAlertaMostrado == alerta?.Nivel && explicacionAlertaMostrada == explicacion &&
                recuperacionAlertaMostrada == alerta?.RecuperacionRestanteMs)
            {
                return;
            }
            estadoMostrado = estado;
            rostroMostrado = avisoRostro;
            nivelAlertaMostrado = alerta?.Nivel;
            explicacionAlertaMostrada = explicacion;
            recuperacionAlertaMostrada = alerta?.RecuperacionRestanteMs;

            Color fondo;
            Color acento;
            string titulo;
            string mensaje;

            if (alerta is { Nivel: not NivelAlertaProgresiva.Normal } progresiva)
            {
                (fondo, acento, titulo, mensaje) = progresiva.Nivel switch
                {
                    NivelAlertaProgresiva.Critica => (Color.FromArgb(72, 20, 28), Tema.Rojo,
                        "ALERTA CRÍTICA", "Detente en un lugar seguro cuando sea posible."),
                    NivelAlertaProgresiva.Alta => (Color.FromArgb(69, 35, 16), Color.FromArgb(249, 115, 22),
                        "ALERTA ALTA", "Recupera la atención y considera realizar una pausa."),
                    _ => (Color.FromArgb(62, 46, 12), Tema.Ambar,
                        "ALERTA PREVENTIVA", "Se observan señales tempranas. Mantén la atención.")
                };
                mensaje += "\n" + progresiva.Explicacion;
                if (progresiva.RecuperacionPendiente)
                    mensaje += $"\nRecuperación pendiente" +
                        (progresiva.RecuperacionRestanteMs is long restante ? $": {restante / 1000.0:0.0} s estables" : ".");
            }
            else if (estado != EyeState.ALARMA_ACTIVADA &&
                avisoRostro is (TipoEventoSesion.RostroNoDetectado or TipoEventoSesion.VariosRostros))
            {
                fondo = Color.FromArgb(62, 46, 12);
                acento = Tema.Ambar;
                titulo = avisoRostro == TipoEventoSesion.RostroNoDetectado
                    ? "ROSTRO NO DETECTADO" : "VARIAS PERSONAS";
                mensaje = avisoRostro == TipoEventoSesion.RostroNoDetectado
                    ? "Vuelve a colocarte frente a la cámara."
                    : "Deja únicamente al conductor en el encuadre.";
            }
            else switch (estado)
            {
                case EyeState.ALARMA_ACTIVADA:
                    fondo = Color.FromArgb(72, 20, 28);
                    acento = Tema.Rojo;
                    titulo = "¡ALERTA! FATIGA DETECTADA";
                    mensaje = (sesion != null ? sesion.Conductor.Nombre : "Conductor") +
                              ", mantén la atención. Tu seguridad es importante.";
                    break;
                case EyeState.SOMNOLIENTO:
                    fondo = Color.FromArgb(62, 46, 12);
                    acento = Tema.Ambar;
                    titulo = "SOMNOLENCIA";
                    mensaje = "Tus ojos llevan un momento cerrados. Mantente alerta.";
                    break;
                default:
                    fondo = Tema.Tarjeta;
                    acento = Tema.Verde;
                    titulo = "CONDUCTOR ATENTO";
                    mensaje = "Ojos abiertos. Todo en orden.";
                    break;
            }

            PintarTarjetaAlerta(fondo, acento, titulo, mensaje);
        }

        private void AplicarNeutral()
        {
            estadoMostrado = null;
            rostroMostrado = null;
            nivelAlertaMostrado = null;
            explicacionAlertaMostrada = "";
            recuperacionAlertaMostrada = null;
            PintarTarjetaAlerta(Tema.Tarjeta, Tema.TextoSuave, "SIN SESIÓN",
                "Inicia una sesión desde \"Registro / Persona\".");
        }

        private void AplicarSinSenal(string titulo = "SIN SEÑAL DE CÁMARA",
            string mensaje = "Comprueba la conexión y los permisos de la cámara.")
        {
            estadoMostrado = null;
            rostroMostrado = null;
            nivelAlertaMostrado = null;
            explicacionAlertaMostrada = "";
            recuperacionAlertaMostrada = null;
            alertaTiempo.Text = "—";
            barra.Valor = 0;
            medidor.Actualizar(0, Tema.TextoSuave, "—");
            PintarTarjetaAlerta(Tema.Tarjeta, Tema.Ambar, titulo, mensaje);
        }

        private void PintarTarjetaAlerta(Color fondo, Color acento, string titulo, string mensaje)
        {
            tarjetaAlerta.ColorTarjeta = fondo;
            tarjetaAlerta.ColorBorde = acento;
            tablaAlerta.BackColor = fondo;

            foreach (Control control in fondosAlerta)
            {
                control.BackColor = fondo;
            }

            alertaTitulo.ForeColor = acento;
            alertaTitulo.Text = titulo;
            alertaMensaje.Text = mensaje;
            barra.ColorBarra = acento;
        }

        private void ActualizarListaAlertas()
        {
            listaAlertas.BeginUpdate();
            listaAlertas.Items.Clear();

            if (sesion == null)
            {
                listaAlertas.Items.Add("Sin sesión activa");
            }
            else if (sesion.CantidadAlarmas == 0)
            {
                listaAlertas.Items.Add("Sin alertas en esta sesión");
            }
            else
            {
                List<DateTime> horas = sesion.HorasAlarmas;
                horas.Reverse();   // la más reciente primero
                foreach (DateTime hora in horas)
                {
                    listaAlertas.Items.Add("Alerta de fatiga  –  " + hora.ToString("HH:mm:ss"));
                }
            }

            listaAlertas.EndUpdate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                reloj.Dispose();
                camaraBox.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
