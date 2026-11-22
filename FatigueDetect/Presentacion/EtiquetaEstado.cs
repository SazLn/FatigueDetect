using System.Drawing.Drawing2D;

namespace FatigueDetect.Presentacion
{
    /// <summary>Píldora de estado con punto de color (cámara activa/apagada).</summary>
    internal sealed class EtiquetaEstado : Control
    {
        private bool activo;

        public bool Activo
        {
            get => activo;
            set { activo = value; Invalidate(); }
        }

        public EtiquetaEstado()
        {
            DoubleBuffered = true;
            Height = 26;
            Font = Tema.Fuente(8.5f, FontStyle.Bold);
            ForeColor = Tema.TextoSuave;
            BackColor = Tema.Tarjeta;
            Text = "Cámara apagada";
        }

        public void Establecer(string texto, bool encendida)
        {
            Text = texto;
            activo = encendida;
            Width = TextRenderer.MeasureText(texto, Font).Width + 38;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = Redondear(area, Height / 2);
            using var fondo = new SolidBrush(activo
                ? Color.FromArgb(30, 126, 224, 195)
                : Color.FromArgb(30, 167, 139, 250));
            g.FillPath(fondo, ruta);

            using var punto = new SolidBrush(activo ? Tema.Verde : Color.FromArgb(111, 102, 144));
            g.FillEllipse(punto, 11, Height / 2 - 4, 8, 8);

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(24, 0, Width - 28, Height),
                activo ? Tema.Verde : Tema.TextoSuave,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
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