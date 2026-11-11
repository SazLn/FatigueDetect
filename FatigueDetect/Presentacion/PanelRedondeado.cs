using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Tarjeta con esquinas redondeadas y borde.</summary>
    public class PanelRedondeado : Panel
    {
        private Color colorTarjeta = Tema.Tarjeta;
        private Color colorBorde = Tema.Borde;

        public Color ColorTarjeta
        {
            get { return colorTarjeta; }
            set { colorTarjeta = value; Invalidate(); }
        }

        public Color ColorBorde
        {
            get { return colorBorde; }
            set { colorBorde = value; Invalidate(); }
        }

        public int Radio { get; set; } = 14;

        public PanelRedondeado()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Tema.Fondo;
            Padding = new Padding(14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rectangulo = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = CrearRuta(rectangulo, Radio);
            using var relleno = new SolidBrush(colorTarjeta);
            using var lapiz = new Pen(colorBorde, 1.5f);

            e.Graphics.FillPath(relleno, ruta);
            e.Graphics.DrawPath(lapiz, ruta);
        }

        public static GraphicsPath CrearRuta(Rectangle r, int radio)
        {
            int d = radio * 2;
            var ruta = new GraphicsPath();
            ruta.AddArc(r.X, r.Y, d, d, 180, 90);
            ruta.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            ruta.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            ruta.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            ruta.CloseFigure();
            return ruta;
        }
    }
}
