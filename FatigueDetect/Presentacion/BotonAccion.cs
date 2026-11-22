using System.Drawing.Drawing2D;

namespace FatigueDetect.Presentacion
{
    /// <summary>Botón con gradiente lila (primario) o fondo oscuro (secundario/ghost).</summary>
    internal sealed class BotonAccion : Button
    {
        private bool encima;
        private bool presionado;

        public bool Secundario { get; }
        public bool Fantasma { get; }

        public BotonAccion(string texto, bool secundario = false, bool fantasma = false)
        {
            Text = texto;
            Secundario = secundario;
            Fantasma = fantasma;
            Height = 42;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Tema.Fuente(10, FontStyle.Bold);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { encima = presionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { presionado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { presionado = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Tema.Fondo);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath ruta = Redondear(area, 10);

            if (Fantasma)
            {
                using var fondo = new SolidBrush(encima ? Tema.TarjetaClara : Tema.Tarjeta);
                g.FillPath(fondo, ruta);
                using var borde = new Pen(Tema.Borde);
                g.DrawPath(borde, ruta);
            }
            else if (Secundario)
            {
                using var fondo = new SolidBrush(encima ? Tema.TarjetaClara : Tema.Campo);
                g.FillPath(fondo, ruta);
                using var borde = new Pen(Tema.CampoBorde);
                g.DrawPath(borde, ruta);
            }
            else
            {
                Color desde = presionado ? Tema.AzulOscuro : Color.FromArgb(132, 102, 240);
                Color hasta = presionado ? Color.FromArgb(150, 120, 250) : Tema.Lila;
                using var degradado = new LinearGradientBrush(area, desde, hasta, LinearGradientMode.Horizontal);
                g.FillPath(degradado, ruta);
            }

            Color colorTexto = Fantasma || Secundario ? Tema.Texto : Color.FromArgb(20, 15, 38);
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            using var pincel = new SolidBrush(colorTexto);
            g.DrawString(Text, Font, pincel, area, formato);
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