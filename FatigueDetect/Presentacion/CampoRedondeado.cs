using System.Drawing.Drawing2D;

namespace FatigueDetect.Presentacion
{
    /// <summary>Contenedor redondeado para un TextBox o ComboBox con borde de foco lila.</summary>
    internal sealed class CampoRedondeado : Panel
    {
        private bool enfocado;
        private readonly Control entrada;

        public CampoRedondeado(Control entrada)
        {
            this.entrada = entrada;
            DoubleBuffered = true;
            BackColor = Tema.Tarjeta;
            Height = 42;
            Padding = new Padding(12, 0, 12, 0);
            Margin = Padding.Empty;

            entrada.Dock = DockStyle.None;
            entrada.Font = Tema.Fuente(10.5f);

            if (entrada is TextBox texto)
            {
                texto.BorderStyle = BorderStyle.None;
                texto.BackColor = Tema.Campo;
                texto.ForeColor = Tema.Texto;
                texto.Margin = Padding.Empty;
            }
            else if (entrada is ComboBox combo)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = Tema.Campo;
                combo.ForeColor = Tema.Texto;
            }

            entrada.GotFocus += (_, _) => { enfocado = true; Invalidate(); };
            entrada.LostFocus += (_, _) => { enfocado = false; Invalidate(); };
            Controls.Add(entrada);
            AjustarEntrada();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AjustarEntrada();
        }

        private void AjustarEntrada()
        {
            const int alto = 23;
            int ancho = Math.Max(10, Width - Padding.Horizontal);
            entrada.SetBounds(Padding.Left, (Height - alto) / 2, ancho, alto);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = Redondear(area, 10);
            using var fondo = new SolidBrush(Tema.Campo);
            e.Graphics.FillPath(fondo, ruta);
            using var borde = new Pen(enfocado ? Tema.Lila : Tema.CampoBorde, enfocado ? 1.6f : 1f);
            e.Graphics.DrawPath(borde, ruta);
        }

        private static GraphicsPath Redondear(Rectangle rect, int radio)
        {
            int d = radio * 2;
            var ruta = new GraphicsPath();
            ruta.AddArc(rect.X, rect.Y, d, d, 180, 90);
            ruta.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            ruta.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            ruta.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            ruta.CloseFigure();
            return ruta;
        }
    }
}