using System;
using System.Drawing;
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
        private readonly ComboBox tipoCarroBox = new ComboBox();
        private readonly TextBox placaBox = new TextBox();
        private readonly PictureBox vistaCamara = new PictureBox();
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
                BackColor = Tema.Fondo
            };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            raiz.Controls.Add(ConstruirFormulario(), 0, 0);
            raiz.Controls.Add(ConstruirBiometria(), 1, 0);
            Controls.Add(raiz);

            RecargarTipos();
        }

        private void ConfigurarEntradas()
        {
            foreach (Control control in new Control[] { cedulaBox, nombreBox, apellidosBox, placaBox, tipoCarroBox })
            {
                control.Font = Tema.Fuente(11);
                control.BackColor = Tema.TarjetaClara;
                control.ForeColor = Tema.Texto;
            }

            cedulaBox.BorderStyle = BorderStyle.FixedSingle;
            nombreBox.BorderStyle = BorderStyle.FixedSingle;
            apellidosBox.BorderStyle = BorderStyle.FixedSingle;
            placaBox.BorderStyle = BorderStyle.FixedSingle;
            tipoCarroBox.FlatStyle = FlatStyle.Flat;
            cedulaBox.MaxLength = 10;
            nombreBox.MaxLength = 40;
            apellidosBox.MaxLength = 40;
            placaBox.MaxLength = 15;
            placaBox.CharacterCasing = CharacterCasing.Upper;
            tipoCarroBox.DropDownStyle = ComboBoxStyle.DropDown;
            tipoCarroBox.MaxLength = 30;

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
            var izquierda = Tema.TablaFilas(Tema.Fondo, 84, 200, 140, 64, 0);
            izquierda.Margin = new Padding(0, 0, 14, 0);

            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Registro de usuario", 20, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            encabezado.Controls.Add(Tema.Etiqueta("Registra tu rostro una vez; después podrás identificarte con la cámara.", 10, Tema.TextoSuave, Tema.Fondo), 0, 1);
            izquierda.Controls.Add(encabezado, 0, 0);

            var tarjetaPersonal = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
            var tablaPersonal = Tema.TablaFilas(Tema.Tarjeta, 34, 66, 66);
            tablaPersonal.Controls.Add(Tema.Etiqueta("Datos personales", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tablaPersonal.Controls.Add(Campo("Número de cédula", cedulaBox), 0, 1);
            tablaPersonal.Controls.Add(DosCampos(Campo("Nombre", nombreBox), Campo("Apellidos", apellidosBox)), 0, 2);
            tarjetaPersonal.Controls.Add(tablaPersonal);
            izquierda.Controls.Add(tarjetaPersonal, 0, 1);

            var tarjetaVehiculo = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
            var tablaVehiculo = Tema.TablaFilas(Tema.Tarjeta, 34, 66);
            tablaVehiculo.Controls.Add(Tema.Etiqueta("Información del vehículo", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tablaVehiculo.Controls.Add(DosCampos(
                Campo("Tipo de carro (elige o escribe uno nuevo)", tipoCarroBox),
                Campo("Placa", placaBox)), 0, 1);
            tarjetaVehiculo.Controls.Add(tablaVehiculo);
            izquierda.Controls.Add(tarjetaVehiculo, 0, 2);

            var botones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.Fondo,
                Margin = new Padding(0),
                Padding = new Padding(0, 6, 0, 0)
            };
            Button iniciar = Tema.Boton("Iniciar monitoreo", Tema.Azul, Color.White);
            iniciar.Width = 210;
            iniciar.Height = 44;
            iniciar.Click += Iniciar_Click;
            Button limpiar = Tema.Boton("Limpiar", Tema.TarjetaClara, Tema.Texto);
            limpiar.Width = 130;
            limpiar.Height = 44;
            limpiar.Click += (remitente, evento) => Limpiar();
            botones.Controls.Add(iniciar);
            botones.Controls.Add(limpiar);
            izquierda.Controls.Add(botones, 0, 3);
            return izquierda;
        }

        private Control ConstruirBiometria()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(14, 0, 0, 0) };
            tablaBiometria = Tema.TablaFilas(Tema.Tarjeta, 40, 62, 270, 52, 52, 24, 28, 70, 0);
            var tabla = tablaBiometria;

            tabla.Controls.Add(Tema.Etiqueta("Identificación biométrica", 14, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta(
                "Si ya te registraste, identifícate para completar tus datos. La primera vez, " +
                "llena el formulario y registra tu rostro.",
                10, Tema.TextoSuave, Tema.Tarjeta), 0, 1);

            vistaCamara.Dock = DockStyle.Fill;
            vistaCamara.Margin = new Padding(0, 6, 0, 6);
            vistaCamara.BackColor = Color.FromArgb(24, 29, 39);
            vistaCamara.SizeMode = PictureBoxSizeMode.Zoom;
            tabla.Controls.Add(vistaCamara, 0, 2);

            estadoBiometrico.Dock = DockStyle.Fill;
            estadoBiometrico.Text = "Cámara apagada.";
            estadoBiometrico.TextAlign = ContentAlignment.MiddleCenter;
            estadoBiometrico.Font = Tema.Fuente(9.5f);
            estadoBiometrico.ForeColor = Tema.TextoSuave;
            estadoBiometrico.BackColor = Tema.Tarjeta;
            tabla.Controls.Add(estadoBiometrico, 0, 3);

            var acciones = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Tema.Tarjeta,
                Margin = new Padding(0)
            };
            acciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            acciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            acciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
            btnIdentificar = Tema.Boton("Identificarme", Tema.Azul, Color.White);
            btnIdentificar.Dock = DockStyle.Fill;
            btnIdentificar.Margin = new Padding(0, 0, 5, 0);
            btnIdentificar.Click += Identificar_Click;
            btnRegistrarRostro = Tema.Boton("Registrar rostro", Tema.TarjetaClara, Tema.Texto);
            btnRegistrarRostro.Dock = DockStyle.Fill;
            btnRegistrarRostro.Margin = new Padding(5, 0, 5, 0);
            btnRegistrarRostro.Click += RegistrarRostro_Click;
            btnCancelarCaptura = Tema.Boton("Cancelar", Tema.TarjetaClara, Tema.Texto);
            btnCancelarCaptura.Dock = DockStyle.Fill;
            btnCancelarCaptura.Margin = new Padding(5, 0, 0, 0);
            btnCancelarCaptura.Enabled = false;
            btnCancelarCaptura.Click += (remitente, evento) => capturaCts?.Cancel();
            acciones.Controls.Add(btnIdentificar, 0, 0);
            acciones.Controls.Add(btnRegistrarRostro, 1, 0);
            acciones.Controls.Add(btnCancelarCaptura, 2, 0);
            tabla.Controls.Add(acciones, 0, 4);

            enlaceEliminar = new LinkLabel
            {
                Text = "Eliminar perfil biométrico",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Tema.Fuente(9),
                LinkColor = Tema.TextoSuave,
                ActiveLinkColor = Tema.Azul,
                BackColor = Tema.Tarjeta
            };
            enlaceEliminar.Click += EliminarPerfil_Click;
            tabla.Controls.Add(enlaceEliminar, 0, 5);

            tabla.Controls.Add(Tema.Etiqueta("Privacidad", 11, Tema.Azul, Tema.Tarjeta, FontStyle.Bold), 0, 6);
            tabla.Controls.Add(Tema.Etiqueta(
                "Se guardan descriptores numéricos en Documentos\\FatigueDetect; no se guardan fotografías. " +
                "La función es experimental y permite continuar manualmente si la cámara no reconoce al usuario.",
                9.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 7);
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
            // PictureBoxSizeMode.Zoom mantiene además la proporción real de cada cámara.
            int alto = tablaBiometria.ClientSize.Width * 10 / 16;
            alto = Math.Clamp(alto, LogicalToDeviceUnits(210), LogicalToDeviceUnits(300));
            tablaBiometria.RowStyles[2].SizeType = SizeType.Absolute;
            tablaBiometria.RowStyles[2].Height = alto;
            vistaCamara.Invalidate();
        }

        private static Control Campo(string etiqueta, Control entrada)
        {
            var celda = Tema.TablaFilas(Tema.Tarjeta, 22, 38);
            celda.Controls.Add(Tema.Etiqueta(etiqueta, 9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            entrada.Dock = DockStyle.Top;
            celda.Controls.Add(entrada, 0, 1);
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
                Margin = new Padding(0)
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            izquierdo.Margin = new Padding(0, 0, 8, 0);
            derecho.Margin = new Padding(8, 0, 0, 0);
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
                estadoBiometrico.ForeColor = Color.SeaGreen;
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
                    estadoBiometrico.ForeColor = Color.Firebrick;
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
                estadoBiometrico.ForeColor = Color.SeaGreen;
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
                estadoBiometrico.ForeColor = Color.Firebrick;
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
                Image? anterior = vistaCamara.Image;
                vistaCamara.Image = progreso.Imagen;
                vistaCamara.Invalidate();
                anterior?.Dispose();
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
            tipoCarroBox.Items.Clear();
            tipoCarroBox.Items.AddRange(catalogo.ObtenerTodos().ToArray());
            if (actual.Length > 0)
                tipoCarroBox.Text = actual;
            else if (tipoCarroBox.Items.Count > 0)
                tipoCarroBox.SelectedIndex = 0;
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
            RecargarTipos();
            cedulaBox.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                capturaCts?.Cancel();
                capturaCts?.Dispose();
                vistaCamara.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
