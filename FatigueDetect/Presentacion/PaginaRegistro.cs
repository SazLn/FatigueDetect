using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    /// <summary>Pantalla "Registro / Persona": datos del conductor y del vehículo.</summary>
    public class PaginaRegistro : UserControl
    {
        private readonly CatalogoTiposCarro catalogo;
        private readonly TextBox cedulaBox = new TextBox();
        private readonly TextBox nombreBox = new TextBox();
        private readonly TextBox apellidosBox = new TextBox();
        private readonly ComboBox tipoCarroBox = new ComboBox();
        private readonly TextBox placaBox = new TextBox();

        /// <summary>cédula, nombre, apellidos, tipo de carro, placa.</summary>
        public event Action<string, string, string, string, string>? RegistroConfirmado;

        public PaginaRegistro(CatalogoTiposCarro catalogo)
        {
            this.catalogo = catalogo;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(22);

            ConfigurarEntradas();

            var raiz = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Tema.Fondo
            };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            raiz.Controls.Add(ConstruirFormulario(), 0, 0);
            raiz.Controls.Add(ConstruirInformacion(), 1, 0);
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

            // Editable: se elige un tipo de la lista o se escribe uno nuevo.
            tipoCarroBox.DropDownStyle = ComboBoxStyle.DropDown;
            tipoCarroBox.MaxLength = 30;

            // La cédula solo acepta números.
            cedulaBox.KeyPress += (remitente, evento) =>
            {
                if (!char.IsControl(evento.KeyChar) && !char.IsDigit(evento.KeyChar))
                {
                    evento.Handled = true;
                }
            };
        }

        private Control ConstruirFormulario()
        {
            var izquierda = Tema.TablaFilas(Tema.Fondo, 84, 200, 140, 64, 0);
            izquierda.Margin = new Padding(0, 0, 14, 0);

            // Encabezado
            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Registro de usuario", 20, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            encabezado.Controls.Add(Tema.Etiqueta("Completa los datos para comenzar el monitoreo de fatiga.", 10, Tema.TextoSuave, Tema.Fondo), 0, 1);
            izquierda.Controls.Add(encabezado, 0, 0);

            // Datos personales
            var tarjetaPersonal = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
            var tablaPersonal = Tema.TablaFilas(Tema.Tarjeta, 34, 66, 66);
            tablaPersonal.Controls.Add(Tema.Etiqueta("Datos personales", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tablaPersonal.Controls.Add(Campo("Número de cédula", cedulaBox), 0, 1);
            tablaPersonal.Controls.Add(DosCampos(Campo("Nombre", nombreBox), Campo("Apellidos", apellidosBox)), 0, 2);
            tarjetaPersonal.Controls.Add(tablaPersonal);
            izquierda.Controls.Add(tarjetaPersonal, 0, 1);

            // Vehículo
            var tarjetaVehiculo = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
            var tablaVehiculo = Tema.TablaFilas(Tema.Tarjeta, 34, 66);
            tablaVehiculo.Controls.Add(Tema.Etiqueta("Información del vehículo", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tablaVehiculo.Controls.Add(DosCampos(Campo("Tipo de carro (elige o escribe uno nuevo)", tipoCarroBox), Campo("Placa", placaBox)), 0, 1);
            tarjetaVehiculo.Controls.Add(tablaVehiculo);
            izquierda.Controls.Add(tarjetaVehiculo, 0, 2);

            // Botones
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

        private Control ConstruirInformacion()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(14, 0, 0, 0) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 40, 96, 16, 34, 30, 30, 30, 30, 16, 80, 0);

            tabla.Controls.Add(Tema.Etiqueta("Antes de comenzar", 14, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta(
                "Se necesita registrar al conductor y su vehículo antes de iniciar el monitoreo. " +
                "La cámara se enciende al pulsar \"Iniciar monitoreo\".",
                10, Tema.TextoSuave, Tema.Tarjeta), 0, 1);

            tabla.Controls.Add(Tema.Etiqueta("Datos que se registrarán", 11, Tema.Azul, Tema.Tarjeta, FontStyle.Bold), 0, 3);
            tabla.Controls.Add(Tema.Etiqueta("•  Número de cédula", 10, Tema.Texto, Tema.Tarjeta), 0, 4);
            tabla.Controls.Add(Tema.Etiqueta("•  Nombre y apellidos", 10, Tema.Texto, Tema.Tarjeta), 0, 5);
            tabla.Controls.Add(Tema.Etiqueta("•  Tipo de carro", 10, Tema.Texto, Tema.Tarjeta), 0, 6);
            tabla.Controls.Add(Tema.Etiqueta("•  Placa del vehículo", 10, Tema.Texto, Tema.Tarjeta), 0, 7);

            tabla.Controls.Add(Tema.Etiqueta(
                "Cada sesión se guarda en el historial con su hora de inicio y fin, " +
                "las alarmas que sonaron y la hora de cada una.",
                10, Tema.TextoSuave, Tema.Tarjeta), 0, 9);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        // Etiqueta + control de entrada, en una celda de 60 px de alto.
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
            string? error = ValidadorRegistro.Validar(
                cedulaBox.Text, nombreBox.Text, apellidosBox.Text, tipoCarroBox.Text, placaBox.Text);

            if (error != null)
            {
                MessageBox.Show(FindForm(), error, "Datos incompletos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RegistroConfirmado?.Invoke(
                cedulaBox.Text.Trim(), nombreBox.Text.Trim(), apellidosBox.Text.Trim(),
                tipoCarroBox.Text.Trim(), placaBox.Text.Trim());
        }

        /// <summary>Vuelve a leer la lista de tipos de carro (incluye los que se acaban de registrar).</summary>
        public void RecargarTipos()
        {
            string actual = tipoCarroBox.Text;

            tipoCarroBox.Items.Clear();
            tipoCarroBox.Items.AddRange(catalogo.ObtenerTodos().ToArray());

            if (actual.Length > 0)
            {
                tipoCarroBox.Text = actual;
            }
            else if (tipoCarroBox.Items.Count > 0)
            {
                tipoCarroBox.SelectedIndex = 0;
            }
        }

        public void Limpiar()
        {
            cedulaBox.Clear();
            nombreBox.Clear();
            apellidosBox.Clear();
            placaBox.Clear();
            tipoCarroBox.Text = "";
            RecargarTipos();
            cedulaBox.Focus();
        }
    }
}
