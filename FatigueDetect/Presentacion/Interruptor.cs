using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Casilla accesible con apariencia de interruptor y foco visible.</summary>
    internal sealed class Interruptor : CheckBox
    {
        public Interruptor()
        {
            AutoSize = false;
            Height = 32;
            Font = Tema.Fuente(10);
            ForeColor = Tema.Texto;
            BackColor = Tema.Tarjeta;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            int alto = 24;
            int ancho = 42;
            int y = (Height - alto) / 2;
            var pista = new Rectangle(1, y, ancho, alto);
            using GraphicsPath ruta = PanelRedondeado.CrearRuta(pista, alto / 2);
            Color fondo = Checked ? Tema.Azul : Tema.TarjetaClara;
            Color borde = Checked ? Tema.Azul : Tema.Borde;
            if (!Enabled)
            {
                fondo = Color.FromArgb(45, fondo);
                borde = Color.FromArgb(90, borde);
            }
            using var relleno = new SolidBrush(fondo);
            using var lapiz = new Pen(borde);
            e.Graphics.FillPath(relleno, ruta);
            e.Graphics.DrawPath(lapiz, ruta);

            int diametro = 18;
            int x = Checked ? pista.Right - diametro - 3 : pista.Left + 3;
            using var circulo = new SolidBrush(Enabled ? Color.White : Tema.TextoSuave);
            e.Graphics.FillEllipse(circulo, x, y + 3, diametro, diametro);

            var texto = new Rectangle(55, 0, Math.Max(0, Width - 55), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, texto,
                Enabled ? ForeColor : Tema.TextoSuave,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                var foco = Rectangle.Inflate(ClientRectangle, -1, -1);
                ControlPaint.DrawFocusRectangle(e.Graphics, foco, Tema.Texto, BackColor);
            }
        }
    }
}
