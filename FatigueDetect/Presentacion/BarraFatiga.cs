using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Barra de progreso redondeada (tiempo con ojos cerrados hasta la alarma).</summary>
    public class BarraFatiga : Control
    {
        private double valor;
        private Color colorBarra = Tema.Verde;

        public Color ColorBarra
        {
            get { return colorBarra; }
            set { colorBarra = value; Invalidate(); }
        }

        /// <summary>De 0 a 1.</summary>
        public double Valor
        {
            get { return valor; }
            set
            {
                double nuevo = Math.Max(0, Math.Min(1, value));
                if (Math.Abs(nuevo - valor) > 0.002)
                {
                    valor = nuevo;
                    Invalidate();
                }
            }
        }

        public BarraFatiga()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Tema.Tarjeta;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var pista = new Rectangle(0, 0, Width - 1, Height - 1);
            if (pista.Width < 4 || pista.Height < 4)
            {
                return;
            }

            using GraphicsPath ruta = PanelRedondeado.CrearRuta(pista, Math.Max(1, pista.Height / 2));
            using var pincelPista = new SolidBrush(Tema.Borde);
            g.FillPath(pincelPista, ruta);

            if (valor > 0)
            {
                g.SetClip(ruta);
                using var pincelRelleno = new SolidBrush(colorBarra);
                g.FillRectangle(pincelRelleno, 0, 0, (int)(pista.Width * valor), pista.Height);
                g.ResetClip();
            }
        }
    }
}
