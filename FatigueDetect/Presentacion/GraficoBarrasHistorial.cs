using System.Drawing.Drawing2D;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal sealed class GraficoBarrasHistorial : Control
    {
        private readonly IReadOnlyList<ResumenCategoria> datos;
        private readonly Color color;

        public GraficoBarrasHistorial(IReadOnlyList<ResumenCategoria> datos, Color color)
        {
            this.datos = datos;
            this.color = color;
            Dock = DockStyle.Fill;
            BackColor = Tema.Tarjeta;
            DoubleBuffered = true;
            ResizeRedraw = true;
            AccessibleName = "Gráfico de barras. Los valores también están disponibles en la tabla.";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (datos.Count == 0 || Width <= 100 || Height <= 75)
                return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            const int inicio = 42;
            const int margenDerecho = 15;
            int baseY = Height - 43;
            float avance = (Width - inicio - margenDerecho) / (float)datos.Count;
            int maximo = Math.Max(1, datos.Max(d => d.Cantidad));
            using var fuente = Tema.Fuente(8);
            using var barra = new SolidBrush(color);
            using var grilla = new Pen(Tema.TarjetaClara);
            using var texto = new SolidBrush(Tema.Texto);
            g.DrawLine(grilla, inicio, baseY, Width - margenDerecho, baseY);
            g.DrawLine(grilla, inicio, 18, Width - margenDerecho, 18);
            g.DrawString(maximo.ToString(), fuente, texto, 4, 12);
            g.DrawString("0", fuente, texto, 4, baseY - 7);

            int salto = Math.Max(1, (int)Math.Ceiling(88 / avance));
            for (int i = 0; i < datos.Count; i++)
            {
                ResumenCategoria punto = datos[i];
                float x = inicio + (i + 0.5f) * avance;
                float alto = punto.Cantidad * (baseY - 18) / (float)maximo;
                float ancho = Math.Min(52, avance * 0.68f);
                if (alto > 0)
                    g.FillRectangle(barra, x - ancho / 2, baseY - alto, ancho, alto);
                if (i % salto == 0 || (i == datos.Count - 1 && i % salto > salto / 2))
                {
                    SizeF medida = g.MeasureString(punto.Categoria, fuente);
                    g.DrawString(punto.Categoria, fuente, texto,
                        Math.Clamp(x - medida.Width / 2, 0, Math.Max(0, Width - medida.Width)), baseY + 7);
                }
            }
        }
    }
}
