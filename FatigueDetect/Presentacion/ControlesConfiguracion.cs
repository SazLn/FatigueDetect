using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Botón redondeado usado por la pantalla de Configuración.</summary>
    internal sealed class BotonConfiguracion : Button
    {
        private bool encima;
        private bool presionado;

        public bool Primario { get; }
        public bool Fantasma { get; }
        public bool Peligro { get; }

        public BotonConfiguracion(string texto, bool primario = false, bool fantasma = false,
            bool peligro = false)
        {
            Text = texto;
            Primario = primario;
            Fantasma = fantasma;
            Peligro = peligro;
            Height = 42;
            Font = Tema.Fuente(9.5f, FontStyle.Bold);
            ForeColor = primario ? Tema.TextoSobreAcento : peligro ? Color.FromArgb(255, 179, 192) : Tema.Texto;
            BackColor = primario ? Tema.Azul : peligro ? Color.FromArgb(61, 35, 57)
                : fantasma ? Tema.Tarjeta : Tema.TarjetaClara;
            Margin = new Padding(0, 0, 10, 0);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { encima = presionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { presionado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { presionado = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? Tema.Tarjeta);
            var rectangulo = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(rectangulo, 10);
            Color baseColor = Primario ? Tema.Azul : Peligro ? Color.FromArgb(61, 35, 57)
                : Fantasma ? Tema.Tarjeta : Tema.TarjetaClara;
            Color fondo = !Enabled ? Color.FromArgb(55, 50, 70)
                : presionado ? ControlPaint.Dark(baseColor, .08f)
                : encima ? ControlPaint.Light(baseColor, .08f) : baseColor;
            using var relleno = new SolidBrush(fondo);
            using var borde = new Pen(Primario ? Tema.Azul : Peligro ? Color.FromArgb(160, 75, 96)
                : Tema.Borde);
            e.Graphics.FillPath(relleno, ruta);
            e.Graphics.DrawPath(borde, ruta);

            TextRenderer.DrawText(e.Graphics, Text, Font, rectangulo,
                Enabled ? ForeColor : Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(rectangulo, -4, -4),
                    Tema.Texto, fondo);
        }
    }

    /// <summary>Marco redondeado para desplegables y otros campos nativos.</summary>
    internal sealed class MarcoEntrada : Panel
    {
        private bool enfocado;

        public MarcoEntrada(Control contenido)
        {
            Height = 40;
            BackColor = Tema.TarjetaClara;
            Padding = new Padding(10, 7, 8, 5);
            Margin = Padding.Empty;
            contenido.Dock = DockStyle.Fill;
            contenido.Margin = Padding.Empty;
            contenido.BackColor = Tema.TarjetaClara;
            contenido.ForeColor = Tema.Texto;
            contenido.Enter += (_, _) => { enfocado = true; Invalidate(); };
            contenido.Leave += (_, _) => { enfocado = false; Invalidate(); };
            Controls.Add(contenido);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rectangulo = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(rectangulo, 10);
            using var borde = new Pen(enfocado ? Tema.Azul : Tema.Borde, enfocado ? 2 : 1);
            e.Graphics.DrawPath(borde, ruta);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            AplicarRegionRedondeada(this, 10);
        }

        private static void AplicarRegionRedondeada(Control control, int radio)
        {
            if (control.Width <= 1 || control.Height <= 1)
                return;
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(
                new Rectangle(0, 0, control.Width - 1, control.Height - 1), radio);
            Region? anterior = control.Region;
            control.Region = new Region(ruta);
            anterior?.Dispose();
        }
    }

    /// <summary>Control numérico con botones menos y más como el prototipo visual.</summary>
    internal sealed class SelectorNumerico : UserControl
    {
        private readonly Button disminuir = new();
        private readonly Button aumentar = new();
        private readonly TextBox entrada = new();
        private readonly TableLayoutPanel tabla;
        private decimal valor;
        private decimal minimo;
        private decimal maximo = 100;
        private decimal incremento = 1;
        private int decimales;
        private bool enfocado;
        private bool mostrarBotones = true;

        public event EventHandler? ValueChanged;

        [DefaultValue(typeof(decimal), "0")]
        public decimal Minimum
        {
            get => minimo;
            set { minimo = value; Value = valor; }
        }

        [DefaultValue(typeof(decimal), "100")]
        public decimal Maximum
        {
            get => maximo;
            set { maximo = value; Value = valor; }
        }

        [DefaultValue(typeof(decimal), "1")]
        public decimal Increment
        {
            get => incremento;
            set => incremento = value > 0 ? value : 1;
        }

        [DefaultValue(0)]
        public int DecimalPlaces
        {
            get => decimales;
            set { decimales = Math.Max(0, value); ActualizarTexto(); }
        }

        public decimal Value
        {
            get => valor;
            set
            {
                decimal nuevo = Math.Min(maximo, Math.Max(minimo, value));
                if (nuevo == valor)
                {
                    ActualizarTexto();
                    return;
                }
                valor = nuevo;
                ActualizarTexto();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [DefaultValue(true)]
        public bool MostrarBotones
        {
            get => mostrarBotones;
            set
            {
                mostrarBotones = value;
                disminuir.Visible = aumentar.Visible = value;
                tabla.ColumnStyles[0].Width = value ? 38 : 0;
                tabla.ColumnStyles[2].Width = value ? 38 : 0;
                entrada.TextAlign = value ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                entrada.Font = Tema.Fuente(10, value ? FontStyle.Bold : FontStyle.Regular);
                entrada.Margin = value ? Padding.Empty : new Padding(10, 7, 10, 0);
                Invalidate();
            }
        }

        public SelectorNumerico()
        {
            Height = 40;
            BackColor = Tema.TarjetaClara;
            Margin = Padding.Empty;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            tabla = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(1),
                BackColor = Color.Transparent
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));

            ConfigurarPaso(disminuir, "−", "Disminuir valor");
            ConfigurarPaso(aumentar, "+", "Aumentar valor");
            disminuir.Click += (_, _) => Value -= Increment;
            aumentar.Click += (_, _) => Value += Increment;

            entrada.BorderStyle = BorderStyle.None;
            entrada.Dock = DockStyle.Fill;
            entrada.TextAlign = HorizontalAlignment.Center;
            entrada.Font = Tema.Fuente(10, FontStyle.Bold);
            entrada.BackColor = Tema.TarjetaClara;
            entrada.ForeColor = Tema.Texto;
            entrada.Margin = new Padding(5, 10, 5, 0);
            entrada.AccessibleName = "Valor numérico";
            entrada.Enter += (_, _) => { enfocado = true; Invalidate(); entrada.SelectAll(); };
            entrada.Leave += (_, _) => { ConfirmarEntrada(); enfocado = false; Invalidate(); };
            entrada.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { ConfirmarEntrada(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { Value += Increment; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down) { Value -= Increment; e.SuppressKeyPress = true; }
            };

            tabla.Controls.Add(disminuir, 0, 0);
            tabla.Controls.Add(entrada, 1, 0);
            tabla.Controls.Add(aumentar, 2, 0);
            Controls.Add(tabla);
            ActualizarTexto();
        }

        public void EnfocarEntrada() => entrada.Focus();

        private static void ConfigurarPaso(Button boton, string texto, string nombreAccesible)
        {
            boton.Text = texto;
            boton.Dock = DockStyle.Fill;
            boton.Margin = Padding.Empty;
            boton.FlatStyle = FlatStyle.Flat;
            boton.FlatAppearance.BorderSize = 0;
            boton.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 42, 73);
            boton.FlatAppearance.MouseDownBackColor = Tema.Borde;
            boton.BackColor = Tema.TarjetaClara;
            boton.ForeColor = Tema.Texto;
            boton.Font = Tema.Fuente(12, FontStyle.Bold);
            boton.Cursor = Cursors.Hand;
            boton.AccessibleName = nombreAccesible;
        }

        private void ConfirmarEntrada()
        {
            if (decimal.TryParse(entrada.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal nuevo))
                Value = nuevo;
            else
                ActualizarTexto();
        }

        private void ActualizarTexto()
        {
            entrada.Text = valor.ToString($"F{decimales}", CultureInfo.CurrentCulture);
            disminuir.Enabled = Enabled && valor > minimo;
            aumentar.Enabled = Enabled && valor < maximo;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            entrada.Enabled = Enabled;
            ActualizarTexto();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rectangulo = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(rectangulo, 10);
            using var borde = new Pen(enfocado ? Tema.Azul : Tema.Borde, enfocado ? 2 : 1);
            e.Graphics.DrawPath(borde, ruta);
            if (MostrarBotones)
            {
                using var separador = new Pen(Tema.Borde);
                e.Graphics.DrawLine(separador, 39, 1, 39, Height - 2);
                e.Graphics.DrawLine(separador, Width - 40, 1, Width - 40, Height - 2);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 1 || Height <= 1)
                return;
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(
                new Rectangle(0, 0, Width - 1, Height - 1), 10);
            Region? anterior = Region;
            Region = new Region(ruta);
            anterior?.Dispose();
        }
    }

    /// <summary>Contenedor de vista previa con borde redondeado discontinuo.</summary>
    internal sealed class MarcoVistaPrevia : Panel
    {
        public MarcoVistaPrevia()
        {
            BackColor = Tema.Lateral;
            Padding = new Padding(2);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rectangulo = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(rectangulo, 12);
            using var borde = new Pen(Color.FromArgb(70, 61, 102)) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawPath(borde, ruta);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width <= 1 || Height <= 1)
                return;
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(
                new Rectangle(0, 0, Width - 1, Height - 1), 12);
            Region? anterior = Region;
            Region = new Region(ruta);
            anterior?.Dispose();
        }
    }

    /// <summary>Estado vacío o de error dibujado sobre la vista previa.</summary>
    internal sealed class EstadoVistaCamara : Control
    {
        public EstadoVistaCamara()
        {
            Dock = DockStyle.Fill;
            BackColor = Tema.Lateral;
            ForeColor = Tema.TextoSuave;
            Font = Tema.Fuente(10);
            AccessibleName = "Estado de la vista previa de cámara";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int centroX = Width / 2;
            int centroY = Height / 2 - 24;
            var cuerpo = new Rectangle(centroX - 17, centroY - 11, 34, 26);
            using var lapiz = new Pen(Tema.Azul, 2.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using GraphicsPath camara = PanelRedondeado.CrearRuta(cuerpo, 5);
            e.Graphics.DrawPath(lapiz, camara);
            e.Graphics.DrawLines(lapiz,
            new Point[]
            {
                new Point(centroX - 9, centroY - 11),
                new Point(centroX - 5, centroY - 16),
                new Point(centroX + 5, centroY - 16),
                new Point(centroX + 9, centroY - 11)
            });
            e.Graphics.DrawEllipse(lapiz, centroX - 6, centroY - 6, 12, 12);

            var texto = new Rectangle(20, centroY + 32, Math.Max(0, Width - 40),
                Math.Max(0, Height - centroY - 32));
            TextRenderer.DrawText(e.Graphics, Text, Font, texto, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                TextFormatFlags.EndEllipsis);
        }
    }
}
