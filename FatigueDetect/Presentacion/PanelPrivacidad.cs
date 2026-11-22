using System.Drawing.Drawing2D;

namespace FatigueDetect.Presentacion
{
    /// <summary>Recuadro de privacidad con ícono de candado y fondo lila tenue.</summary>
    internal sealed class PanelPrivacidad : Panel
    {
        public PanelPrivacidad()
        {
            DoubleBuffered = true;
            BackColor = Tema.Tarjeta;
            Padding = new Padding(44, 12, 12, 12);
            Margin = new Padding(0, 10, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = Redondear(area, 10);
            using var fondo = new SolidBrush(Color.FromArgb(32, 26, 49));
            g.FillPath(fondo, ruta);
            using var borde = new Pen(Color.FromArgb(70, 55, 105));
            g.DrawPath(borde, ruta);

            using var icono = new Pen(Tema.Lila, 1.6f);
            g.DrawRectangle(icono, 16, 17, 12, 9);
            g.DrawArc(icono, 18, 11, 8, 10, 180, 180);
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