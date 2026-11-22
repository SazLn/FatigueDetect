using System.Drawing;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    internal sealed class VentanaContrasenaRespaldo : Form
    {
        private readonly TextBox entrada = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
        public string Contrasena => entrada.Text;

        public VentanaContrasenaRespaldo(string titulo)
        {
            Text = titulo;
            Size = new Size(470, 205);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Tema.Fondo;
            ForeColor = Tema.Texto;
            var tabla = Tema.TablaFilas(Tema.Fondo, 45, 34, 55);
            tabla.Padding = new Padding(18);
            tabla.Controls.Add(Tema.Etiqueta("Contraseña del respaldo (mínimo 10 caracteres)", 10,
                Tema.Texto, Tema.Fondo), 0, 0);
            entrada.BackColor = Tema.TarjetaClara; entrada.ForeColor = Tema.Texto;
            tabla.Controls.Add(entrada, 0, 1);
            var botones = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                BackColor = Tema.Fondo };
            var aceptar = Tema.Boton("Aceptar", Tema.Azul, Color.White);
            var cancelar = Tema.Boton("Cancelar", Tema.TarjetaClara, Tema.Texto);
            aceptar.Click += (_, _) => { if (Contrasena.Length >= 10) { DialogResult = DialogResult.OK; Close(); }
                else MessageBox.Show(this, "La contraseña debe tener al menos 10 caracteres."); };
            cancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            botones.Controls.Add(aceptar); botones.Controls.Add(cancelar); tabla.Controls.Add(botones, 0, 2);
            Controls.Add(tabla); AcceptButton = aceptar; CancelButton = cancelar;
        }
    }
}
