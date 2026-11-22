using System.Drawing.Drawing2D;

namespace FatigueDetect.Presentacion
{
    /// <summary>Pestaña plana con borde superior lila cuando está activa.</summary>
    internal sealed class BotonPestana : Control
    {
        private bool activo;
        private bool encima;

        public bool Activo
        {
            get => activo;
            set { activo = value; Invalidate(); }
        }

        public BotonPestana(string texto)
        {
            Text = texto;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Height = 38;
            Cursor = Cursors.Hand;
            Font = Tema.Fuente(9.5f, FontStyle.Bold);
            ForeColor = Tema.TextoSuave;
            BackColor = Tema.Fondo;
            Margin = new Padding(0, 0, 4, 0);
            Padding = new Padding(14, 0, 14, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            Color fondo = activo ? Tema.Tarjeta : encima ? Color.FromArgb(30, 255, 255, 255) : Tema.Fondo;
            using var pincel = new SolidBrush(fondo);
            var area = new Rectangle(0, 0, Width, Height);
            using var ruta = RedondearSuperior(area, 10);
            g.FillPath(pincel, ruta);

            if (activo)
            {
                using var acento = new Pen(Tema.Lila, 2.5f);
                g.DrawLine(acento, 1, 1.5f, Width - 2, 1.5f);
            }

            using var texto = new SolidBrush(activo ? Tema.Texto : Tema.TextoSuave);
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(Text, Font, texto, area, formato);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            encima = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            encima = false;
            Invalidate();
        }

        private static GraphicsPath RedondearSuperior(Rectangle rect, int radio)
        {
            int d = radio * 2;
            var ruta = new GraphicsPath();
            ruta.AddArc(rect.X, rect.Y, d, d, 180, 90);
            ruta.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            ruta.AddLine(rect.Right, rect.Y + radio, rect.Right, rect.Bottom);
            ruta.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            ruta.AddLine(rect.X, rect.Bottom, rect.X, rect.Y + radio);
            ruta.CloseFigure();
            return ruta;
        }
    }
}