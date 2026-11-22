using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Botón redondeado del menú lateral.</summary>
    public class BotonNavegacion : Button
    {
        private bool activo;
        private bool encima;
        private bool presionado;

        public bool Activo
        {
            get => activo;
            set { activo = value; Invalidate(); }
        }

        public BotonNavegacion(string texto)
        {
            Text = texto;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(14, 0, 8, 0);
            Height = 43;
            Font = Tema.Fuente(10.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { encima = presionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { presionado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { presionado = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Tema.Lateral);
            var rectangulo = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fondo = activo ? Tema.Azul
                : presionado ? Tema.Borde
                : encima ? Tema.Tarjeta : Tema.Lateral;
            using (GraphicsPath ruta = PanelRedondeado.CrearRuta(rectangulo, 12))
            {
                if (activo)
                {
                    using var degradado = new LinearGradientBrush(
                        rectangulo, Tema.AzulOscuro, Tema.Lila, LinearGradientMode.Horizontal);
                    e.Graphics.FillPath(degradado, ruta);
                }
                else if (fondo.ToArgb() != Tema.Lateral.ToArgb())
                {
                    using var relleno = new SolidBrush(fondo);
                    e.Graphics.FillPath(relleno, ruta);
                }
            }

            var texto = new Rectangle(Padding.Left, 0,
                Math.Max(0, Width - Padding.Horizontal), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, texto,
                activo ? Tema.TextoSobreAcento : encima ? Tema.Texto : Tema.TextoSuave,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(rectangulo, -3, -3),
                    activo ? Tema.TextoSobreAcento : Tema.Texto, fondo);
        }
    }
}
