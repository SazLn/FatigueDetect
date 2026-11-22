using System.Drawing.Drawing2D;

namespace FatigueDetect.Presentacion
{
    /// <summary>Tarjeta de indicador con borde superior de acento y número grande.</summary>
    internal sealed class TarjetaKpi : Control
    {
        public string Titulo { get; set; } = "";
        public string Valor { get; set; } = "—";
        public Color Acento { get; set; } = Tema.Azul;
        public string Detalle { get; set; } = "";

        public TarjetaKpi()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Tema.Fondo;
            Margin = new Padding(7, 0, 7, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            using var ruta = Redondear(area, 14);
            using var fondo = new LinearGradientBrush(
                area, Color.FromArgb(29, 24, 48), Color.FromArgb(25, 21, 42), LinearGradientMode.Vertical);
            using var borde = new Pen(Tema.Borde);
            g.FillPath(fondo, ruta);
            g.DrawPath(borde, ruta);

            // Borde superior de acento (3 px).
            using var acento = new Pen(Acento, 3f);
            g.DrawLine(acento, 1, 1.5f, Width - 2, 1.5f);

            using var fuenteTitulo = Tema.Fuente(9);
            using var fuenteValor = Tema.Fuente(24, FontStyle.Bold);
            using var fuenteDetalle = Tema.Fuente(8);
            using var textoTitulo = new SolidBrush(Tema.TextoSuave);
            using var textoValor = new SolidBrush(Acento);
            using var textoDetalle = new SolidBrush(Tema.TextoSuave);

            g.DrawString(Titulo, fuenteTitulo, textoTitulo, 18, 14);
            g.DrawString(Valor, fuenteValor, textoValor, 17, 34);
            if (!string.IsNullOrEmpty(Detalle))
            {
                g.DrawString(Detalle, fuenteDetalle, textoDetalle, 18, Height - 24);
            }
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