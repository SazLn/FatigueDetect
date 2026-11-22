using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    /// <summary>Pantalla de registro manual y reconocimiento biométrico del conductor.</summary>
    public class PaginaRegistro : UserControl
    {
        private readonly CatalogoTiposCarro catalogo;
        private readonly ServicioBiometrico servicioBiometrico;
        private readonly TextBox cedulaBox = new TextBox();
        private readonly TextBox nombreBox = new TextBox();
        private readonly TextBox apellidosBox = new TextBox();
        private readonly TextBox tipoCarroBox = new TextBox();
        private readonly ContextMenuStrip menuTipos = new ContextMenuStrip();
        private readonly Label botonTipos = new Label();
        private readonly TextBox placaBox = new TextBox();
        private readonly PanelCamara panelCamara = new PanelCamara();
        private readonly EtiquetaEstado estadoCamara = new EtiquetaEstado();
        private readonly Label estadoBiometrico = new Label();
        private TableLayoutPanel tablaBiometria = null!;
        private Button btnIdentificar = null!;
        private Button btnRegistrarRostro = null!;
        private Button btnCancelarCaptura = null!;
        private LinkLabel enlaceEliminar = null!;
        private CancellationTokenSource? capturaCts;
        private string? cedulaIdentificada;

        /// <summary>cédula, nombre, apellidos, tipo de carro, placa.</summary>
        public event Action<string, string, string, string, string>? RegistroConfirmado;

        public PaginaRegistro(CatalogoTiposCarro catalogo, ServicioBiometrico servicioBiometrico)
        {
            this.catalogo = catalogo;
            this.servicioBiometrico = servicioBiometrico;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(22);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScroll = true;
            AutoScrollMinSize = new Size(0, LogicalToDeviceUnits(650));

            ConfigurarEntradas();

            var raiz = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Tema.Fondo,
                Margin = Padding.Empty
            };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            raiz.Controls.Add(ConstruirFormulario(), 0, 0);
            raiz.Controls.Add(ConstruirBiometria(), 1, 0);
            Controls.Add(raiz);

            RecargarTipos();
        }

        private void ConfigurarEntradas()
        {
            foreach (Control control in new Control[] { cedulaBox, nombreBox, apellidosBox, placaBox, tipoCarroBox })
            {
                control.Font = Tema.Fuente(10.5f);
                control.BackColor = Tema.Campo;
                control.ForeColor = Tema.Texto;
                control.Margin = Padding.Empty;
            }

            cedulaBox.MaxLength = 10;
            nombreBox.MaxLength = 40;
            apellidosBox.MaxLength = 40;
            placaBox.MaxLength = 15;
            placaBox.CharacterCasing = CharacterCasing.Upper;
            tipoCarroBox.MaxLength = 30;

            cedulaBox.PlaceholderText = "Ej. 1065234567";
            nombreBox.PlaceholderText = "Ej. Laura";
            apellidosBox.PlaceholderText = "Ej. Pérez Gómez";
            placaBox.PlaceholderText = "Ej. ABC123";

            tipoCarroBox.BorderStyle = BorderStyle.None;
            tipoCarroBox.BackColor = Tema.Campo;
            tipoCarroBox.ForeColor = Tema.Texto;
            menuTipos.BackColor = Tema.Tarjeta;
            menuTipos.ForeColor = Tema.Texto;

            cedulaBox.KeyPress += (remitente, evento) =>
            {
                if (!char.IsControl(evento.KeyChar) && !char.IsDigit(evento.KeyChar))
                    evento.Handled = true;
            };
            cedulaBox.TextChanged += (remitente, evento) =>
            {
                if (cedulaIdentificada != null && cedulaBox.Text.Trim() != cedulaIdentificada)
                {
                    cedulaIdentificada = null;
                    estadoBiometrico.Text = "La identidad debe confirmarse de nuevo.";
                    estadoBiometrico.ForeColor = Tema.TextoSuave;
                }
            };
        }

        private Control ConstruirFormulario()
        {
            var izquierda = Tema.TablaFilas(Tema.Fondo, 86, 224, 150, 70, 0);
            izquierda.Margin = new Padding(0, 0, 20, 0);

            var encabezado = Tema.TablaFilas(Tema.Fondo, 44, 34);
            encabezado.Controls.Add(Tema.Etiqueta("Registro de usuario", 22, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            encabezado.Controls.Add(Tema.Etiqueta(
                "Registra tu rostro una vez; después podrás identificarte con la cámara.",
                10, Tema.TextoSuave, Tema.Fondo), 0, 1);
            izquierda.Controls.Add(encabezado, 0, 0);

            izquierda.Controls.Add(ConstruirDatosPersonales(), 0, 1);
            izquierda.Controls.Add(ConstruirDatosVehiculo(), 0, 2);

            var botones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.Fondo,
                Margin = Padding.Empty,
                Padding = new Padding(0, 8, 0, 0)
            };

            var iniciar = new BotonAccion("Iniciar monitoreo") { Width = 200 };
            iniciar.Click += Iniciar_Click;
            var limpiar = new BotonAccion("Limpiar", secundario: true) { Width = 110 };
            limpiar.Click += (remitente, evento) => Limpiar();

            botones.Controls.Add(iniciar);
            botones.Controls.Add(limpiar);
            izquierda.Controls.Add(botones, 0, 3);
            return izquierda;
        }

        private Control ConstruirDatosPersonales()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 26, 24, 64, 64, 0);

            tabla.Controls.Add(Tema.Etiqueta("Datos personales", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta("Usa los mismos datos de tu documento.", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            tabla.Controls.Add(Campo("Número de cédula", cedulaBox), 0, 2);
            tabla.Controls.Add(DosCampos(Campo("Nombre", nombreBox), Campo("Apellidos", apellidosBox)), 0, 3);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private Control ConstruirDatosVehiculo()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 26, 24, 64, 0);

            tabla.Controls.Add(Tema.Etiqueta("Información del vehículo", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta("Elige un tipo de la lista o escribe uno nuevo.", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            tabla.Controls.Add(DosCampos(
                Campo("Tipo de carro", ConstruirCampoTipo()),
                Campo("Placa", placaBox)), 0, 2);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private Control ConstruirBiometria()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(20, 0, 0, 0) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 32, 46, 0, 24, 54, 30, 104);
            tablaBiometria = tabla;

            var cabecera = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Tema.Tarjeta,
                Margin = Padding.Empty
            };
            cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            cabecera.Controls.Add(Tema.Etiqueta("Identificación biométrica", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            estadoCamara.Establecer("Cámara apagada", false);
            estadoCamara.Dock = DockStyle.Fill;
            estadoCamara.Margin = new Padding(0, 3, 0, 3);
            cabecera.Controls.Add(estadoCamara, 1, 0);
            tabla.Controls.Add(cabecera, 0, 0);

            tabla.Controls.Add(Tema.Etiqueta(
                "Si ya te registraste, identifícate para completar tus datos. La primera vez, " +
                "llena el formulario y registra tu rostro.",
                9.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 1);

            panelCamara.Dock = DockStyle.Fill;
            tabla.Controls.Add(panelCamara, 0, 2);

            estadoBiometrico.Dock = DockStyle.Fill;
            estadoBiometrico.Text = "Cámara apagada.";
            estadoBiometrico.TextAlign = ContentAlignment.MiddleCenter;
            estadoBiometrico.Font = Tema.Fuente(9);
            estadoBiometrico.ForeColor = Tema.TextoSuave;
            estadoBiometrico.BackColor = Tema.Tarjeta;
            tabla.Controls.Add(estadoBiometrico, 0, 3);

            var acciones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.Tarjeta,
                Margin = Padding.Empty,
                Padding = new Padding(0, 6, 0, 0)
            };
            btnIdentificar = new BotonAccion("Identificarme") { Width = 128 };
            btnIdentificar.Click += Identificar_Click;
            btnRegistrarRostro = new BotonAccion("Registrar rostro", secundario: true) { Width = 140 };
            btnRegistrarRostro.Click += RegistrarRostro_Click;
            btnCancelarCaptura = new BotonAccion("Cancelar", fantasma: true) { Width = 100 };
            btnCancelarCaptura.Enabled = false;
            btnCancelarCaptura.Click += (remitente, evento) => capturaCts?.Cancel();
            acciones.Controls.Add(btnIdentificar);
            acciones.Controls.Add(btnRegistrarRostro);
            acciones.Controls.Add(btnCancelarCaptura);
            tabla.Controls.Add(acciones, 0, 4);

            enlaceEliminar = new LinkLabel
            {
                Text = "Eliminar perfil biométrico",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Tema.Fuente(8.5f),
                LinkColor = Tema.Peligro,
                ActiveLinkColor = Tema.Peligro,
                VisitedLinkColor = Tema.Peligro,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Tema.Tarjeta
            };
            enlaceEliminar.Click += EliminarPerfil_Click;
            tabla.Controls.Add(enlaceEliminar, 0, 5);

            tabla.Controls.Add(ConstruirPrivacidad(), 0, 6);
            tabla.SizeChanged += (_, _) => AjustarRelacionVistaCamara();
            tarjeta.Controls.Add(tabla);
            tarjeta.SizeChanged += (_, _) => tarjeta.Invalidate(true);
            AjustarRelacionVistaCamara();
            return tarjeta;
        }

        private void AjustarRelacionVistaCamara()
        {
            if (tablaBiometria is null || tablaBiometria.ClientSize.Width <= 0)
                return;

            // El marco conserva 16:10 al cambiar entre ventana restaurada y maximizada.
            int alto = tablaBiometria.ClientSize.Width * 10 / 16;
            alto = Math.Clamp(alto, LogicalToDeviceUnits(210), LogicalToDeviceUnits(300));
            tablaBiometria.RowStyles[2].SizeType = SizeType.Absolute;
            tablaBiometria.RowStyles[2].Height = alto;
            panelCamara.Invalidate();
        }

        private static Control ConstruirPrivacidad()
        {
            var panel = new PanelPrivacidad { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var tabla = Tema.TablaFilas(Color.FromArgb(32, 26, 49), 22, 0);
            tabla.Padding = new Padding(0, 0, 0, 0);
            tabla.Controls.Add(Tema.Etiqueta("Privacidad", 10, Tema.Lila, Color.FromArgb(32, 26, 49), FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta(
                "Se guardan descriptores numéricos en Documentos\\FatigueDetect; no se guardan fotografías. " +
                "La función es experimental y permite continuar manualmente si la cámara no reconoce al usuario.",
                8.5f, Tema.TextoSuave, Color.FromArgb(32, 26, 49)), 0, 1);
            panel.Controls.Add(tabla);
            return panel;
        }

        private Control ConstruirCampoTipo()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Campo, Margin = Padding.Empty };

            botonTipos.Text = "▾";
            botonTipos.Dock = DockStyle.Right;
            botonTipos.Width = 24;
            botonTipos.TextAlign = ContentAlignment.MiddleCenter;
            botonTipos.BackColor = Tema.Campo;
            botonTipos.ForeColor = Tema.TextoSuave;
            botonTipos.Font = Tema.Fuente(9);
            botonTipos.Cursor = Cursors.Hand;
            botonTipos.Click += (_, _) => menuTipos.Show(botonTipos, new Point(0, botonTipos.Height));

            tipoCarroBox.Dock = DockStyle.Fill;
            panel.Controls.Add(tipoCarroBox);
            panel.Controls.Add(botonTipos);
            return panel;
        }

        private static Control Campo(string etiqueta, Control entrada)
        {
            var celda = Tema.TablaFilas(Tema.Tarjeta, 22, 42);
            celda.Controls.Add(Tema.Etiqueta(etiqueta, 8.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            celda.Controls.Add(new CampoRedondeado(entrada) { Dock = DockStyle.Fill }, 0, 1);
            return celda;
        }

        private static Control DosCampos(Control izquierdo, Control derecho)
        {
            var tabla = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Tema.Tarjeta,
                Margin = Padding.Empty
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            izquierdo.Margin = new Padding(0, 0, 10, 0);
            derecho.Margin = new Padding(10, 0, 0, 0);
            tabla.Controls.Add(izquierdo, 0, 0);
            tabla.Controls.Add(derecho, 1, 0);
            return tabla;
        }

        private void Iniciar_Click(object? remitente, EventArgs evento)
        {
            string? error = ValidarFormulario();
            if (error != null)
            {
                MessageBox.Show(FindForm(), error, "Datos incompletos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cedula = cedulaBox.Text.Trim();
            if (!servicioBiometrico.TienePerfil(cedula))
            {
                MessageBox.Show(FindForm(),
                    "Antes de iniciar por primera vez, registra tu rostro con el botón de la derecha.",
                    "Registro biométrico pendiente", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (cedulaIdentificada != cedula)
            {
                DialogResult continuar = MessageBox.Show(FindForm(),
                    "No se confirmó la identidad facial en esta sesión. ¿Deseas continuar usando los datos ingresados manualmente?",
                    "Identidad sin confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (continuar != DialogResult.Yes) return;
            }

            RegistroConfirmado?.Invoke(
                cedula, nombreBox.Text.Trim(), apellidosBox.Text.Trim(),
                tipoCarroBox.Text.Trim(), placaBox.Text.Trim());
        }

        private async void RegistrarRostro_Click(object? remitente, EventArgs evento)
        {
            string? error = ValidarFormulario();
            if (error != null)
            {
                MessageBox.Show(FindForm(), error, "Datos incompletos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult consentimiento = MessageBox.Show(FindForm(),
                "Se crearán descriptores numéricos de tu rostro y se guardarán localmente en este equipo. " +
                "No se guardarán fotografías. Puedes eliminar el perfil desde esta misma pantalla.\n\n¿Aceptas continuar?",
                "Consentimiento biométrico", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (consentimiento != DialogResult.Yes) return;

            var perfil = new PerfilBiometrico
            {
                Cedula = cedulaBox.Text,
                Nombre = nombreBox.Text,
                Apellidos = apellidosBox.Text,
                TipoCarro = tipoCarroBox.Text,
                Placa = placaBox.Text
            };

            await EjecutarCapturaAsync(async token =>
            {
                await servicioBiometrico.RegistrarAsync(perfil, CrearProgreso(), token);
                cedulaIdentificada = perfil.Cedula.Trim();
                estadoBiometrico.Text = "Rostro registrado. Ya puedes iniciar el monitoreo.";
                estadoBiometrico.ForeColor = Tema.Verde;
            });
        }

        private async void Identificar_Click(object? remitente, EventArgs evento)
        {
            await EjecutarCapturaAsync(async token =>
            {
                ResultadoIdentificacion resultado = await servicioBiometrico.IdentificarAsync(CrearProgreso(), token);
                if (!resultado.Exitoso || resultado.Perfil == null)
                {
                    estadoBiometrico.Text = resultado.Mensaje;
                    estadoBiometrico.ForeColor = Tema.Peligro;
                    MessageBox.Show(FindForm(), resultado.Mensaje, "Identificación no confirmada",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                PerfilBiometrico perfil = resultado.Perfil;
                cedulaIdentificada = perfil.Cedula;
                cedulaBox.Text = perfil.Cedula;
                nombreBox.Text = perfil.Nombre;
                apellidosBox.Text = perfil.Apellidos;
                tipoCarroBox.Text = perfil.TipoCarro;
                placaBox.Text = perfil.Placa;
                cedulaIdentificada = perfil.Cedula;
                estadoBiometrico.Text = $"{resultado.Mensaje} Coincidencia: {resultado.Coincidencia:P0}.";
                estadoBiometrico.ForeColor = Tema.Verde;
            });
        }

        private async Task EjecutarCapturaAsync(Func<CancellationToken, Task> accion)
        {
            capturaCts?.Dispose();
            capturaCts = new CancellationTokenSource();
            ActivarCaptura(true);
            try
            {
                await accion(capturaCts.Token);
            }
            catch (OperationCanceledException)
            {
                estadoBiometrico.Text = "Captura cancelada.";
                estadoBiometrico.ForeColor = Tema.TextoSuave;
            }
            catch (Exception ex)
            {
                estadoBiometrico.Text = "No se pudo completar la captura.";
                estadoBiometrico.ForeColor = Tema.Peligro;
                MessageBox.Show(FindForm(), ex.Message, "Error biométrico", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ActivarCaptura(false);
                capturaCts.Dispose();
                capturaCts = null;
            }
        }

        private IProgress<ProgresoCapturaBiometrica> CrearProgreso()
        {
            return new Progress<ProgresoCapturaBiometrica>(progreso =>
            {
                panelCamara.MostrarImagen(progreso.Imagen);
                estadoBiometrico.Text = $"{progreso.Mensaje} ({progreso.Capturas}/{progreso.Requeridas})";
                estadoBiometrico.ForeColor = Tema.TextoSuave;
            });
        }

        private void ActivarCaptura(bool activa)
        {
            btnIdentificar.Enabled = !activa;
            btnRegistrarRostro.Enabled = !activa;
            btnCancelarCaptura.Enabled = activa;
            enlaceEliminar.Enabled = !activa;
            estadoCamara.Establecer(activa ? "Cámara activa" : "Cámara apagada", activa);
        }

        private void EliminarPerfil_Click(object? remitente, EventArgs evento)
        {
            string cedula = cedulaBox.Text.Trim();
            if (cedula.Length == 0)
            {
                MessageBox.Show(FindForm(), "Escribe primero la cédula del perfil que deseas eliminar.",
                    "Cédula requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirmar = MessageBox.Show(FindForm(),
                $"¿Eliminar el perfil biométrico asociado a la cédula {cedula}?",
                "Eliminar perfil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmar != DialogResult.Yes) return;

            if (servicioBiometrico.EliminarPerfil(cedula))
            {
                cedulaIdentificada = null;
                estadoBiometrico.Text = "Perfil biométrico eliminado.";
                estadoBiometrico.ForeColor = Tema.TextoSuave;
            }
            else
            {
                MessageBox.Show(FindForm(), "No se encontró un perfil biométrico para esa cédula.",
                    "Perfil no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string? ValidarFormulario()
        {
            return ValidadorRegistro.Validar(
                cedulaBox.Text, nombreBox.Text, apellidosBox.Text, tipoCarroBox.Text, placaBox.Text);
        }

        public void RecargarTipos()
        {
            string actual = tipoCarroBox.Text;
            menuTipos.Items.Clear();

            foreach (string tipo in catalogo.ObtenerTodos())
            {
                var item = new ToolStripMenuItem(tipo);
                item.Click += (_, _) => tipoCarroBox.Text = tipo;
                menuTipos.Items.Add(item);
            }

            if (actual.Length > 0)
            {
                tipoCarroBox.Text = actual;
            }
            else if (menuTipos.Items.Count > 0)
            {
                tipoCarroBox.Text = menuTipos.Items[0]!.Text;
            }
        }

        public void Limpiar()
        {
            cedulaIdentificada = null;
            cedulaBox.Clear();
            nombreBox.Clear();
            apellidosBox.Clear();
            placaBox.Clear();
            tipoCarroBox.Text = "";
            estadoBiometrico.Text = "Cámara apagada.";
            estadoBiometrico.ForeColor = Tema.TextoSuave;
            estadoCamara.Establecer("Cámara apagada", false);
            RecargarTipos();
            cedulaBox.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                capturaCts?.Cancel();
                capturaCts?.Dispose();
                panelCamara.Vista.Image?.Dispose();
                estadoCamara.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}