using System;
using System.Drawing;
using System.Windows.Forms;
using FatigueDetect.Entidades;

namespace FatigueDetect.Presentacion
{
    /// <summary>Pantalla "Configuración": sensibilidad de la detección y sonido.</summary>
    public class PaginaConfiguracion : UserControl
    {
        private readonly Configuracion config;
        private readonly NumericUpDown somnolientoNum = new NumericUpDown();
        private readonly NumericUpDown alarmaNum = new NumericUpDown();
        private readonly CheckBox sonidoCheck = new CheckBox();

        public PaginaConfiguracion(Configuracion config)
        {
            this.config = config;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(22);

            ConfigurarNumerico(somnolientoNum, 0.5M, 5M);
            ConfigurarNumerico(alarmaNum, 1M, 10M);

            sonidoCheck.Text = "Reproducir el sonido de la alarma";
            sonidoCheck.Font = Tema.Fuente(11);
            sonidoCheck.ForeColor = Tema.Texto;
            sonidoCheck.BackColor = Tema.Tarjeta;
            sonidoCheck.Dock = DockStyle.Fill;
            sonidoCheck.Margin = new Padding(0);

            CargarValores();

            var raiz = Tema.TablaFilas(Tema.Fondo, 84, 370, 0);

            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Configuración", 20, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            encabezado.Controls.Add(Tema.Etiqueta("Ajusta la sensibilidad de la detección de fatiga.", 10, Tema.TextoSuave, Tema.Fondo), 0, 1);
            raiz.Controls.Add(encabezado, 0, 0);

            var tarjeta = new PanelRedondeado { Dock = DockStyle.Left, Width = 620 };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 36, 24, 44, 24, 44, 14, 44, 18, 44, 50);

            tabla.Controls.Add(Tema.Etiqueta("Sensibilidad de la detección", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            tabla.Controls.Add(Tema.Etiqueta("Segundos con ojos cerrados para marcar SOMNOLENCIA", 9.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            tabla.Controls.Add(Alinear(somnolientoNum), 0, 2);

            tabla.Controls.Add(Tema.Etiqueta("Segundos con ojos cerrados para activar la ALARMA", 9.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 3);
            tabla.Controls.Add(Alinear(alarmaNum), 0, 4);

            tabla.Controls.Add(sonidoCheck, 0, 6);

            tabla.Controls.Add(Tema.Etiqueta(
                "Los cambios se aplican de inmediato, incluso durante una sesión, y se restablecen al cerrar el programa.",
                9, Tema.TextoSuave, Tema.Tarjeta), 0, 8);

            var botones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.Tarjeta,
                Margin = new Padding(0),
                Padding = new Padding(0, 6, 0, 0)
            };
            Button guardar = Tema.Boton("Guardar cambios", Tema.Azul, Color.White);
            guardar.Width = 180;
            guardar.Click += Guardar_Click;

            Button restablecer = Tema.Boton("Restablecer", Tema.TarjetaClara, Tema.Texto);
            restablecer.Width = 140;
            restablecer.Click += Restablecer_Click;

            botones.Controls.Add(guardar);
            botones.Controls.Add(restablecer);
            tabla.Controls.Add(botones, 0, 9);

            tarjeta.Controls.Add(tabla);
            raiz.Controls.Add(tarjeta, 0, 1);

            Controls.Add(raiz);
        }

        private static void ConfigurarNumerico(NumericUpDown numerico, decimal minimo, decimal maximo)
        {
            numerico.DecimalPlaces = 1;
            numerico.Increment = 0.5M;
            numerico.Minimum = minimo;
            numerico.Maximum = maximo;
            numerico.Font = Tema.Fuente(11);
            numerico.BackColor = Tema.TarjetaClara;
            numerico.ForeColor = Tema.Texto;
            numerico.BorderStyle = BorderStyle.FixedSingle;
            numerico.Width = 120;
        }

        // Pone el control numérico a la izquierda de su celda.
        private static Control Alinear(Control control)
        {
            var contenedor = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Tarjeta, Margin = new Padding(0) };
            control.Left = 0;
            control.Top = 6;
            contenedor.Controls.Add(control);
            return contenedor;
        }

        private void CargarValores()
        {
            somnolientoNum.Value = Limitar((decimal)config.SegundosSomnoliento, somnolientoNum);
            alarmaNum.Value = Limitar((decimal)config.SegundosAlarma, alarmaNum);
            sonidoCheck.Checked = config.SonidoActivado;
        }

        private static decimal Limitar(decimal valor, NumericUpDown numerico)
        {
            return Math.Max(numerico.Minimum, Math.Min(numerico.Maximum, valor));
        }

        private void Guardar_Click(object? remitente, EventArgs evento)
        {
            if (alarmaNum.Value <= somnolientoNum.Value)
            {
                MessageBox.Show(FindForm(),
                    "El tiempo de la alarma debe ser mayor que el de somnolencia.",
                    "Valores no válidos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            config.SegundosSomnoliento = (double)somnolientoNum.Value;
            config.SegundosAlarma = (double)alarmaNum.Value;
            config.SonidoActivado = sonidoCheck.Checked;

            MessageBox.Show(FindForm(), "Configuración guardada.", "Listo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Restablecer_Click(object? remitente, EventArgs evento)
        {
            config.SegundosSomnoliento = 1.5;
            config.SegundosAlarma = 3.0;
            config.SonidoActivado = true;
            CargarValores();
        }
    }
}
