using System;
using System.Collections.Generic;
using System.Drawing;
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

        public event Action? DetenerSolicitado;
        public event Action<bool>? PausaSolicitada;

        // Cámara
        private readonly PictureBox camaraBox = new PictureBox();
        private Label sinSenal = null!;
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
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            Control camara = ConstruirCamara();
            raiz.Controls.Add(camara, 0, 0);
            raiz.SetColumnSpan(camara, 2);
            raiz.Controls.Add(ConstruirAlerta(), 2, 0);
            raiz.Controls.Add(ConstruirGrafica(), 0, 1);
            raiz.Controls.Add(ConstruirMedidor(), 1, 1);
            raiz.Controls.Add(ConstruirListaAlertas(), 2, 1);
            Controls.Add(raiz);

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
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 30, 0, 52);

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
            cabecera.Controls.Add(Tema.Etiqueta("●  EN VIVO", 10, Tema.Rojo, Tema.Tarjeta, FontStyle.Bold), 0, 0);
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
                PausaSolicitada?.Invoke(pausado);
            };

            botones.Controls.Add(detenerBtn);
            botones.Controls.Add(pausarBtn);
            tabla.Controls.Add(botones, 0, 2);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private Control ConstruirAlerta()
        {
            tarjetaAlerta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(6) };
            tablaAlerta = Tema.TablaFilas(Tema.Tarjeta, 44, 66, 22, 54, 18, 24, 14, 24, 24, 24, 24, 0);

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
            tabla.Controls.Add(Tema.Etiqueta("Atención", 10.5f, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

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
            pausarBtn.Text = "Pausar";
            pausarBtn.Enabled = true;
            detenerBtn.Enabled = true;

            grafica.Limpiar();
            ultimoMuestreo = DateTime.MinValue;
            alarmasMostradas = -1;
            estadoMostrado = null;

            lblConductor.Text = "Conductor: " + nueva.Conductor.NombreCompleto;
            lblCedula.Text = "C.C. " + nueva.Conductor.Cedula;
            lblVehiculo.Text = nueva.Vehiculo.TipoCarro + "  ·  Placa " + nueva.Vehiculo.Placa;
            lblAlarmas.Text = "Alarmas en esta sesión: 0";
            lblDuracion.Text = "Duración: 00:00:00";
            capAlarma.Text = "Alarma a los " + config.SegundosAlarma.ToString("0.0") + " s";

            sinSenal.Text = "Iniciando cámara...";
            sinSenal.Visible = true;

            AplicarEstado(EyeState.DESPIERTO);
            medidor.Actualizar(1.0, Tema.Verde, "ALTO");
            ActualizarListaAlertas();
            reloj.Start();
        }

        public void FinalizarSesion()
        {
            reloj.Stop();
            sesion = null;
            pausado = false;

            pausarBtn.Text = "Pausar";
            pausarBtn.Enabled = false;
            detenerBtn.Enabled = false;

            Image? vieja = camaraBox.Image;
            camaraBox.Image = null;
            vieja?.Dispose();

            sinSenal.Text = "Sin sesión activa.\nVe a \"Registro / Persona\" para iniciar el monitoreo.";
            sinSenal.Visible = true;

            lblConductor.Text = "Conductor: —";
            lblCedula.Text = "C.C. —";
            lblVehiculo.Text = "Vehículo: —";
            lblAlarmas.Text = "Alarmas en esta sesión: —";
            lblDuracion.Text = "Duración: 00:00:00";
            capAlarma.Text = "Alarma a los " + config.SegundosAlarma.ToString("0.0") + " s";
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

        public void UpdateImage(Bitmap? image, EyeState estado, long tiempoMs)
        {
            // Si la pantalla sigue ocupada con el cuadro anterior, se descarta este.
            if (IsDisposed || !IsHandleCreated || Interlocked.CompareExchange(ref actualizando, 1, 0) != 0)
            {
                image?.Dispose();
                return;
            }

            try
            {
                BeginInvoke(new Action(() => Actualizar(image, estado, tiempoMs)));
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

        private void Actualizar(Bitmap? imagen, EyeState estado, long tiempoMs)
        {
            try
            {
                SesionConduccion? actual = sesion;
                if (actual == null || IsDisposed)
                {
                    imagen?.Dispose();
                    return;
                }

                if (imagen != null)
                {
                    sinSenal.Visible = false;
                    Image? vieja = camaraBox.Image;
                    camaraBox.Image = imagen;
                    vieja?.Dispose();
                }

                AplicarEstado(estado);

                double segundos = tiempoMs / 1000.0;
                alertaTiempo.Text = segundos.ToString("0.0") + " s";
                barra.Valor = segundos / Math.Max(0.1, config.SegundosAlarma);
                capAlarma.Text = "Alarma a los " + config.SegundosAlarma.ToString("0.0") + " s";

                grafica.UmbralSomnoliento = config.SegundosSomnoliento;
                grafica.UmbralAlarma = config.SegundosAlarma;
                if ((DateTime.Now - ultimoMuestreo).TotalMilliseconds >= MuestreoMs)
                {
                    grafica.AgregarValor(segundos);
                    ultimoMuestreo = DateTime.Now;
                }

                ActualizarMedidor(estado, actual);

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

        private void ActualizarMedidor(EyeState estado, SesionConduccion actual)
        {
            NivelAtencion nivel = CalculadorAtencion.Calcular(estado, actual);

            switch (nivel)
            {
                case NivelAtencion.BAJO:
                    medidor.Actualizar(0.25, Tema.Rojo, "BAJO");
                    break;
                case NivelAtencion.MEDIO:
                    medidor.Actualizar(0.6, Tema.Ambar, "MEDIO");
                    break;
                default:
                    medidor.Actualizar(1.0, Tema.Verde, "ALTO");
                    break;
            }
        }

        private void AplicarEstado(EyeState estado)
        {
            if (estadoMostrado == estado)
            {
                return;
            }
            estadoMostrado = estado;

            Color fondo;
            Color acento;
            string titulo;
            string mensaje;

            switch (estado)
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
            PintarTarjetaAlerta(Tema.Tarjeta, Tema.TextoSuave, "SIN SESIÓN",
                "Inicia una sesión desde \"Registro / Persona\".");
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
