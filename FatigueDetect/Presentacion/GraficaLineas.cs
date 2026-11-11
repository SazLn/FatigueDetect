using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Gráfica de línea con los últimos valores (tiempo con ojos cerrados).</summary>
    public class GraficaLineas : Control
    {
        private readonly List<double> valores = new List<double>();
        private readonly int maxPuntos;

        public double UmbralSomnoliento { get; set; } = 1.5;
        public double UmbralAlarma { get; set; } = 3.0;

        public GraficaLineas(int maxPuntos = 120)
        {
            this.maxPuntos = maxPuntos;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Tema.Tarjeta;
        }

        public void AgregarValor(double valor)
        {
            valores.Add(valor);
            if (valores.Count > maxPuntos)
            {
                valores.RemoveAt(0);
            }
            Invalidate();
        }

        public void Limpiar()
        {
            valores.Clear();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int izquierda = 40, derecha = 10, arriba = 8, abajo = 20;
            var area = new Rectangle(izquierda, arriba, Width - izquierda - derecha, Height - arriba - abajo);
            if (area.Width < 20 || area.Height < 20)
            {
                return;
            }

            double maximo = Math.Max(UmbralAlarma + 1.0, valores.Count > 0 ? valores.Max() + 0.5 : 0);

            using var lapizRejilla = new Pen(Tema.Borde, 1);
            using var pincelTexto = new SolidBrush(Tema.TextoSuave);
            using var fuente = Tema.Fuente(8);

            // Rejilla y valores del eje Y
            for (int i = 0; i <= 4; i++)
            {
                float y = area.Bottom - (float)(area.Height * i / 4.0);
                g.DrawLine(lapizRejilla, area.Left, y, area.Right, y);
                g.DrawString((maximo * i / 4.0).ToString("0.0") + "s", fuente, pincelTexto, 0, y - 8);
            }

            g.DrawString("hace 1 min", fuente, pincelTexto, area.Left, area.Bottom + 3);
            g.DrawString("ahora", fuente, pincelTexto, area.Right - 32, area.Bottom + 3);

            // Líneas de umbral
            DibujarUmbral(g, area, maximo, UmbralSomnoliento, Tema.Ambar);
            DibujarUmbral(g, area, maximo, UmbralAlarma, Tema.Rojo);

            // Línea de datos (se desplaza hacia la izquierda con el tiempo)
            if (valores.Count >= 2)
            {
                float paso = area.Width / (float)(maxPuntos - 1);
                var puntos = new PointF[valores.Count];

                for (int i = 0; i < valores.Count; i++)
                {
                    float x = area.Right - (valores.Count - 1 - i) * paso;
                    float y = area.Bottom - (float)(area.Height * Math.Min(valores[i], maximo) / maximo);
                    puntos[i] = new PointF(x, y);
                }

                using var lapizLinea = new Pen(Tema.Azul, 2.4f);
                g.DrawLines(lapizLinea, puntos);
            }
        }

        private static void DibujarUmbral(Graphics g, Rectangle area, double maximo, double umbral, Color color)
        {
            float y = area.Bottom - (float)(area.Height * Math.Min(umbral, maximo) / maximo);
            using var lapiz = new Pen(color, 1.2f) { DashStyle = DashStyle.Dash };
            g.DrawLine(lapiz, area.Left, y, area.Right, y);
        }
    }
}
