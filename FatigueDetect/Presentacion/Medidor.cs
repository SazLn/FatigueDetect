using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Medidor semicircular (nivel de atención).</summary>
    public class Medidor : Control
    {
        private double valor;
        private Color colorValor = Tema.TextoSuave;
        private string texto = "—";

        public Medidor()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Tema.Tarjeta;
        }

        public void Actualizar(double nuevoValor, Color color, string nuevoTexto)
        {
            valor = Math.Max(0, Math.Min(1, nuevoValor));
            colorValor = color;
            texto = nuevoTexto;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int grosor = 14;
            int lado = Math.Min(Width - 2 * grosor, (Height - 40) * 2);
            if (lado < 60)
            {
                return;
            }

            int x = (Width - lado) / 2;
            int y = grosor;
            var rectangulo = new Rectangle(x, y, lado, lado);

            using (var pista = new Pen(Tema.Borde, grosor) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(pista, rectangulo, 180, 180);
            }

            if (valor > 0.01)
            {
                using var arco = new Pen(colorValor, grosor) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(arco, rectangulo, 180, (float)(180 * valor));
            }

            int centroY = y + lado / 2;
            using var fuenteGrande = Tema.Fuente(18, FontStyle.Bold);
            using var fuentePequena = Tema.Fuente(9);
            using var pincelValor = new SolidBrush(colorValor);
            using var pincelSuave = new SolidBrush(Tema.TextoSuave);
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(texto, fuenteGrande, pincelValor, new RectangleF(x, centroY - 46, lado, 34), formato);
            g.DrawString("Nivel de atención", fuentePequena, pincelSuave, new RectangleF(x, centroY - 12, lado, 22), formato);
        }
    }
}
