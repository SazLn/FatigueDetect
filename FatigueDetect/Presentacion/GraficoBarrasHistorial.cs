using System.Drawing.Drawing2D;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    /// <summary>
    /// Gráfico de barras con el estilo de la referencia: gradiente ámbar,
    /// grilla punteada, selección lila y etiquetas cada 3 horas.
    /// </summary>
    internal sealed class GraficoBarrasHistorial : Control
    {
        private readonly IReadOnlyList<ResumenCategoria> datos;
        private readonly Color color;
        private int seleccionado = -1;
        private int resaltado = -1;

        public event Action<int>? SeleccionCambiada;

        public GraficoBarrasHistorial(IReadOnlyList<ResumenCategoria> datos, Color color)
        {
            this.datos = datos;
            this.color = color;
            Dock = DockStyle.Fill;
            BackColor = Tema.Tarjeta;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Cursor = Cursors.Hand;
            AccessibleName = "Gráfico de barras. Los valores también están disponibles en la tabla.";
        }

        public int Seleccionado
        {
            get => seleccionado;
            set
            {
                if (datos.Count == 0) return;
                seleccionado = Math.Clamp(value, 0, datos.Count - 1);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (datos.Count == 0 || Width <= 100 || Height <= 80)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            const int izquierda = 34;
            const int derecha = 12;
            const int arriba = 14;
            const int abajo = 30;
            int baseY = Height - abajo;

            int maximo = Math.Max(1, datos.Max(d => d.Cantidad));
            int paso = Math.Max(1, (int)Math.Ceiling(maximo / 5.0));
            int tope = paso * 5;

            using var fuente = Tema.Fuente(8);
            using var texto = new SolidBrush(Tema.TextoSuave);
            using var grilla = new Pen(Color.FromArgb(38, 255, 255, 255)) { DashStyle = DashStyle.Dash };
            using var lineaBase = new Pen(Tema.Borde);

            // Eje Y y grilla punteada.
            for (int i = 0; i <= 5; i++)
            {
                int valor = i * paso;
                float y = baseY - (float)((baseY - arriba) * (valor / (double)tope));
                if (i > 0)
                    g.DrawLine(grilla, izquierda, y, Width - derecha, y);

                string etiqueta = valor.ToString();
                SizeF medida = g.MeasureString(etiqueta, fuente);
                g.DrawString(etiqueta, fuente, texto, izquierda - 6 - medida.Width,
                    y - medida.Height / 2f);
            }
            g.DrawLine(lineaBase, izquierda, baseY, Width - derecha, baseY);

            int cantidad = datos.Count;
            float anchoZona = Width - izquierda - derecha;
            float avance = anchoZona / cantidad;
            float anchoBarra = Math.Min(26, Math.Max(4, avance * 0.55f));

            for (int i = 0; i < cantidad; i++)
            {
                ResumenCategoria punto = datos[i];
                float centro = izquierda + (i + 0.5f) * avance;
                float alto = (float)((baseY - arriba) * (punto.Cantidad / (double)tope));
                bool activa = i == seleccionado;
                bool encima = i == resaltado;

                if (alto > 0)
                {
                    var rect = new RectangleF(centro - anchoBarra / 2, baseY - alto, anchoBarra, alto);
                    if (activa || encima)
                    {
                        using var brillo = new SolidBrush(Color.FromArgb(34, 167, 139, 250));
                        g.FillRectangle(brillo, rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 3);
                    }

                    Color superior = activa || encima
                        ? Color.FromArgb(216, 200, 255)
                        : Color.FromArgb(255, 201, 138);
                    Color inferior = activa || encima ? color : color;
                    using var degradado = new LinearGradientBrush(
                        new RectangleF(rect.X, rect.Y, Math.Max(1, rect.Width), Math.Max(1, rect.Height)),
                        superior, inferior, LinearGradientMode.Vertical);
                    using var ruta = Redondear(rect, 4);
                    g.FillPath(degradado, ruta);
                }

                // Etiquetas del eje X cada 3 posiciones y la última.
                if (i % 3 == 0 || i == cantidad - 1)
                {
                    SizeF medida = g.MeasureString(punto.Categoria, fuente);
                    g.DrawString(punto.Categoria, fuente, texto,
                        Math.Clamp(centro - medida.Width / 2, 0, Math.Max(0, Width - medida.Width)),
                        baseY + 6);
                }
            }

            // Tooltip del elemento resaltado.
            if (resaltado >= 0 && resaltado < cantidad)
            {
                ResumenCategoria punto = datos[resaltado];
                string leyenda = $"{punto.Categoria} · {punto.Cantidad} {(punto.Cantidad == 1 ? "alarma" : "alarmas")}";
                SizeF medida = g.MeasureString(leyenda, fuente);
                float centro = izquierda + (resaltado + 0.5f) * avance;
                float x = Math.Clamp(centro - medida.Width / 2 - 8, 0, Math.Max(0, Width - medida.Width - 22));
                float y = arriba - 2;
                using var fondo = new SolidBrush(Tema.TarjetaClara);
                using var borde = new Pen(Tema.Borde);
                var caja = new RectangleF(x, y, medida.Width + 16, medida.Height + 8);
                g.FillRectangle(fondo, caja);
                g.DrawRectangle(borde, caja.X, caja.Y, caja.Width, caja.Height);
                g.DrawString(leyenda, fuente, new SolidBrush(Tema.Texto), x + 8, y + 4);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int indice = IndiceEn(e.X);
            if (indice != resaltado)
            {
                resaltado = indice;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (resaltado != -1)
            {
                resaltado = -1;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int indice = IndiceEn(e.X);
            if (indice >= 0)
            {
                Seleccionado = indice;
                SeleccionCambiada?.Invoke(indice);
            }
        }

        private int IndiceEn(int x)
        {
            if (datos.Count == 0 || x < 34 || x > Width - 12)
                return -1;

            float avance = (Width - 34 - 12) / (float)datos.Count;
            int indice = (int)((x - 34) / avance);
            return indice >= 0 && indice < datos.Count ? indice : -1;
        }

        private static GraphicsPath Redondear(RectangleF rect, int radio)
        {
            var ruta = new GraphicsPath();
            int d = Math.Min(radio * 2, (int)Math.Min(rect.Width, rect.Height));
            if (d <= 0)
            {
                ruta.AddRectangle(rect);
                return ruta;
            }

            ruta.AddArc(rect.X, rect.Y, d, d, 180, 90);
            ruta.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            ruta.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            ruta.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            ruta.CloseFigure();
            return ruta;
        }
    }
}