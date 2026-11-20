using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;

namespace FatigueDetect.Presentacion
{
    /// <summary>Pantalla de configuración de detección, sonido y cámara.</summary>
    public class PaginaConfiguracion : UserControl
    {
        private readonly Configuracion config;
        private readonly ConfiguracionDeteccionRepository repositorioAjustes;
        private readonly PanelCamaraConfiguracion panelCamara;
        private readonly NumericUpDown somnolientoNum = new NumericUpDown();
        private readonly NumericUpDown alarmaNum = new NumericUpDown();
        private readonly CheckBox sonidoCheck = new CheckBox();

        private readonly ComboBox salidaAudio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Button probarAudio = Tema.Boton("Probar", Tema.TarjetaClara, Tema.Texto);
        private readonly Label estadoAudio = Tema.Etiqueta("", 9, Tema.TextoSuave, Tema.Tarjeta);

        public PaginaConfiguracion(Configuracion config, PreferenciaCamara preferenciaCamara,
            ConfiguracionDeteccionRepository repositorioAjustes)
        {
            this.config = config;
            this.repositorioAjustes = repositorioAjustes;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(22);

            ConfigurarNumerico(somnolientoNum, (decimal)AjustesDeteccion.MinimoSomnolencia, AjustesDeteccion.MaximoSomnolencia);
            ConfigurarNumerico(alarmaNum, AjustesDeteccion.MinimoAlarma, AjustesDeteccion.MaximoAlarma);

            sonidoCheck.Text = "Reproducir el sonido de la alarma";
            sonidoCheck.Font = Tema.Fuente(11);
            sonidoCheck.ForeColor = Tema.Texto;
            sonidoCheck.BackColor = Tema.Tarjeta;
            sonidoCheck.Dock = DockStyle.Fill;
            sonidoCheck.Margin = new Padding(0);

            CargarValores();

            var raiz = Tema.TablaFilas(Tema.Fondo, 84, 0);

            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Configuración", 20, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            encabezado.Controls.Add(Tema.Etiqueta("Ajusta la detección de fatiga y configura la cámara.", 10, Tema.TextoSuave, Tema.Fondo), 0, 1);
            raiz.Controls.Add(encabezado, 0, 0);

            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 32, 22, 34, 22, 34, 8, 30, 22, 35, 32, 42);

            tabla.Controls.Add(Tema.Etiqueta("Sensibilidad de la detección", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            tabla.Controls.Add(Tema.Etiqueta("Segundos con ojos cerrados para marcar SOMNOLENCIA", 9.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            tabla.Controls.Add(Alinear(somnolientoNum), 0, 2);

            tabla.Controls.Add(Tema.Etiqueta("Segundos con ojos cerrados para activar la ALARMA", 9.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 3);
            tabla.Controls.Add(Alinear(alarmaNum), 0, 4);

            tabla.Controls.Add(sonidoCheck, 0, 6);

            tabla.Controls.Add(Tema.Etiqueta("Salida de audio de la alarma", 9.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 7);
            var audio = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0) };
            audio.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            audio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            audio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            probarAudio.Dock = DockStyle.Fill;
            var recargar = Tema.Boton("Actualizar", Tema.TarjetaClara, Tema.Texto);
            recargar.Dock = DockStyle.Fill;
            probarAudio.Margin = recargar.Margin = new Padding(3, 0, 0, 0);
            recargar.Click += (_, _) => CargarSalidas((salidaAudio.SelectedItem as SalidaAudio)?.Id);
            probarAudio.Click += async (_, _) => await ProbarSalidaAsync();
            audio.Controls.Add(salidaAudio, 0, 0);
            audio.Controls.Add(probarAudio, 1, 0);
            audio.Controls.Add(recargar, 2, 0);
            tabla.Controls.Add(audio, 0, 8);
            tabla.Controls.Add(estadoAudio, 0, 9);

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
            tabla.Controls.Add(botones, 0, 10);

            tarjeta.Controls.Add(tabla);
            panelCamara = new PanelCamaraConfiguracion(config, preferenciaCamara);
            var columnas = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            columnas.Controls.Add(tarjeta, 0, 0);
            columnas.Controls.Add(panelCamara, 1, 0);
            raiz.Controls.Add(columnas, 0, 1);

            Controls.Add(raiz);
        }

        public void ActualizarCamaras() => panelCamara.Recargar();

        public void HabilitarCamara(bool habilitar) => panelCamara.Habilitar(habilitar);

        public void DetenerVistaPrevia() => panelCamara.DetenerVistaPrevia();

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
            AjustesDeteccion ajustes = config.Deteccion;
            somnolientoNum.Value = (decimal)ajustes.SegundosSomnoliento;
            alarmaNum.Value = (decimal)ajustes.SegundosAlarma;
            sonidoCheck.Checked = ajustes.SonidoActivado;
            CargarSalidas(ajustes.SalidaAudioId);
        }

        private void CargarSalidas(string? id)
        {
            salidaAudio.Items.Clear();
            estadoAudio.Text = "";
            try
            {
                foreach (SalidaAudio salida in AlarmService.ListarSalidas())
                    salidaAudio.Items.Add(salida);
            }
            catch (Exception ex) when (AlarmService.EsErrorDeAudio(ex))
            {
                salidaAudio.Items.Add(new SalidaAudio(null, "Predeterminada de Windows"));
                estadoAudio.Text = "No se pudieron consultar las salidas de Windows.";
            }

            SalidaAudio? elegida = salidaAudio.Items.Cast<SalidaAudio>().FirstOrDefault(s => s.Id == id);
            if (elegida is null)
            {
                elegida = new SalidaAudio(id, "Salida guardada (desconectada)");
                salidaAudio.Items.Add(elegida);
                estadoAudio.Text = "Si la salida guardada falla, se usa la predeterminada.";
            }
            salidaAudio.SelectedItem = elegida;
        }

        private async Task ProbarSalidaAsync()
        {
            probarAudio.Enabled = false;
            var salida = (SalidaAudio)salidaAudio.SelectedItem!;
            estadoAudio.Text = "Reproduciendo alarma…";
            try
            {
                await AlarmService.ProbarAsync(salida.Id);
                if (!IsDisposed)
                    estadoAudio.Text = "Prueba finalizada. Comprueba si oíste la alarma.";
            }
            catch (Exception ex) when (AlarmService.EsErrorDeAudio(ex))
            {
                if (!IsDisposed)
                    estadoAudio.Text = "No se pudo reproducir en esa salida. Revisa la conexión.";
            }
            finally
            {
                if (!IsDisposed)
                    probarAudio.Enabled = true;
            }
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

            var ajustes = new AjustesDeteccion((double)somnolientoNum.Value, (double)alarmaNum.Value, sonidoCheck.Checked,
                (salidaAudio.SelectedItem as SalidaAudio)?.Id);
            AplicarYGuardar(ajustes, "Configuración guardada.");
        }

        private void Restablecer_Click(object? remitente, EventArgs evento)
        {
            AplicarYGuardar(AjustesDeteccion.Predeterminados, "Valores predeterminados guardados.");
        }

        private void AplicarYGuardar(AjustesDeteccion ajustes, string confirmacion)
        {
            try
            {
                repositorioAjustes.Guardar(ajustes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(FindForm(), "No se pudo guardar la configuración.\n" + ex.Message,
                    "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            config.Deteccion = ajustes;
            CargarValores();
            MessageBox.Show(FindForm(), confirmacion, "Configuración",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
