using System.Drawing;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Botón del menú lateral; el activo se resalta en azul.</summary>
    public class BotonNavegacion : Button
    {
        private bool activo;

        public bool Activo
        {
            get { return activo; }
            set { activo = value; ActualizarEstilo(); }
        }

        public BotonNavegacion(string texto)
        {
            Text = "   " + texto;
            FlatStyle = FlatStyle.Flat;
            TextAlign = ContentAlignment.MiddleLeft;
            Height = 46;
            Font = Tema.Fuente(10.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            FlatAppearance.BorderSize = 0;
            ActualizarEstilo();
        }

        private void ActualizarEstilo()
        {
            BackColor = activo ? Tema.Azul : Tema.Lateral;
            ForeColor = activo ? Tema.TextoSobreAcento : Tema.TextoSuave;
            FlatAppearance.MouseOverBackColor = activo ? Tema.Azul : Tema.TarjetaClara;
            FlatAppearance.MouseDownBackColor = Tema.AzulOscuro;
        }
    }
}
