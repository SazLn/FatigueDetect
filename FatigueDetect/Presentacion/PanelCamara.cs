using System.Drawing.Drawing2D;

namespace FatigueDetect.Presentacion
{
    /// <summary>Área de cámara con borde punteado, esquinas redondeadas y marcador de posición.</summary>
    internal sealed class PanelCamara : Panel
    {
        public PictureBox Vista { get; }

        public PanelCamara()
        {
            DoubleBuffered = true;
            BackColor = Tema.Tarjeta;
            Padding = new Padding(2);
            Margin = new Padding(0, 4, 0, 8);

            Vista = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(14, 11, 23),
                Visible = false
            };
            Controls.Add(Vista);
        }

        public void MostrarImagen(Bitmap? imagen)
        {
            Image? anterior = Vista.Image;
            Vista.Image = imagen;
            Vista.Visible = imagen != null;
            anterior?.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = Redondear(area, 14);
            using var fondo = new SolidBrush(Color.FromArgb(14, 11, 23));
            g.FillPath(fondo, ruta);
            using var borde = new Pen(Color.FromArgb(70, 61, 102), 1.2f)
            {
                DashStyle = DashStyle.Custom,
                DashPattern = new[] { 4f, 4f }
            };
            g.DrawPath(borde, ruta);

            if (Vista.Image != null)
            {
                return;
            }

            int cx = Width / 2;
            int cy = Height / 2 - 24;
            using var icono = new Pen(Tema.Lila, 2f);
            g.DrawEllipse(icono, cx - 12, cy - 20, 24, 24);
            g.DrawArc(icono, cx - 22, cy + 2, 44, 34, 180, 180);

            var texto = new Rectangle(0, cy + 44, Width, 44);
            TextRenderer.DrawText(g, "La vista de la cámara\naparecerá aquí.", Tema.Fuente(10),
                texto, Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
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