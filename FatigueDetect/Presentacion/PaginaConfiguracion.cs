using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;

namespace FatigueDetect.Presentacion
{
    /// <summary>Pantalla adaptable de configuración de detección, sonido, datos y cámara.</summary>
    public class PaginaConfiguracion : UserControl
    {
        private readonly Configuracion config;
        private readonly ConfiguracionDeteccionRepository repositorioAjustes;
        private readonly PanelCamaraConfiguracion panelCamara;
        private readonly ProteccionDatosService proteccionDatos;
        private readonly PoliticaProteccionRepository politicaRepository;
        private readonly SelectorNumerico somnolientoNum = new();
        private readonly SelectorNumerico alarmaNum = new();
        private readonly Interruptor sonidoCheck = new() { Text = "Reproducir el sonido de la alarma" };
        private readonly ComboBox salidaAudio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly BotonConfiguracion probarAudio = new("Probar");
        private readonly Label estadoAudio = Tema.Etiqueta("", 9, Tema.TextoSuave, Tema.Tarjeta);
        private readonly Label estadoSensibilidad = Tema.Etiqueta("", 9, Tema.Ambar, Tema.Tarjeta);
        private readonly Interruptor retencionAutomatica = new() { Text = "Eliminar datos al cumplir el plazo" };
        private readonly SelectorNumerico diasHistorial = new()
            { Minimum = 30, Maximum = 3650, MostrarBotones = false };
        private readonly SelectorNumerico diasBiometricos = new()
            { Minimum = 30, Maximum = 3650, MostrarBotones = false };
        private readonly Label estadoDatos = Tema.Etiqueta("", 8.5f, Tema.TextoSuave, Tema.Tarjeta);
        private readonly TableLayoutPanel columnas = new();
        private readonly TableLayoutPanel pagina = new();
        private readonly Control columnaIzquierda;
        private bool dosColumnas;

        public PaginaConfiguracion(Configuracion config, PreferenciaCamara preferenciaCamara,
            ConfiguracionDeteccionRepository repositorioAjustes,
            ProteccionDatosService proteccionDatos, PoliticaProteccionRepository politicaRepository)
        {
            this.config = config;
            this.repositorioAjustes = repositorioAjustes;
            this.proteccionDatos = proteccionDatos;
            this.politicaRepository = politicaRepository;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            AutoScroll = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            ConfigurarNumerico(somnolientoNum, (decimal)AjustesDeteccion.MinimoSomnolencia,
                AjustesDeteccion.MaximoSomnolencia, 1);
            ConfigurarNumerico(alarmaNum, AjustesDeteccion.MinimoAlarma, AjustesDeteccion.MaximoAlarma, 1);
            ConfigurarNumerico(diasHistorial, 30, 3650, 0);
            ConfigurarNumerico(diasBiometricos, 30, 3650, 0);
            ConfigurarSelector(salidaAudio);

            sonidoCheck.CheckedChanged += (_, _) => ActualizarEstadoSonido();
            retencionAutomatica.CheckedChanged += (_, _) => ActualizarEstadoRetencion();
            somnolientoNum.ValueChanged += (_, _) => ValidarTiempos(false);
            alarmaNum.ValueChanged += (_, _) => ValidarTiempos(false);

            CargarValores();
            CargarPolitica();

            pagina.Dock = DockStyle.Top;
            pagina.ColumnCount = 1;
            pagina.RowCount = 2;
            pagina.BackColor = Tema.Fondo;
            pagina.Margin = Padding.Empty;
            pagina.Padding = new Padding(36, 24, 36, 44);
            pagina.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pagina.RowStyles.Add(new RowStyle(SizeType.Absolute, Escalar(89)));
            pagina.RowStyles.Add(new RowStyle(SizeType.Absolute, Escalar(747)));

            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Configuración", 20, Tema.Texto, Tema.Fondo,
                FontStyle.Bold), 0, 0);
            encabezado.Controls.Add(Tema.Etiqueta("Ajusta la detección de fatiga y configura la cámara.",
                10, Tema.TextoSuave, Tema.Fondo), 0, 1);
            pagina.Controls.Add(encabezado, 0, 0);

            columnaIzquierda = ConstruirColumnaIzquierda();
            panelCamara = new PanelCamaraConfiguracion(config, preferenciaCamara);

            columnas.Dock = DockStyle.Fill;
            columnas.BackColor = Tema.Fondo;
            columnas.Margin = Padding.Empty;
            pagina.Controls.Add(columnas, 0, 1);
            Controls.Add(pagina);

            SizeChanged += (_, _) => OrganizarColumnas();
            OrganizarColumnas(forzar: true);
        }

        public void ActualizarCamaras() => panelCamara.Recargar();

        public void HabilitarCamara(bool habilitar) => panelCamara.Habilitar(habilitar);

        public void DetenerVistaPrevia() => panelCamara.DetenerVistaPrevia();

        private Control ConstruirColumnaIzquierda()
        {
            var columna = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Tema.Fondo,
                Margin = Padding.Empty
            };
            columna.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            columna.RowStyles.Add(new RowStyle(SizeType.Absolute, Escalar(443)));
            columna.RowStyles.Add(new RowStyle(SizeType.Absolute, Escalar(18)));
            columna.RowStyles.Add(new RowStyle(SizeType.Absolute, Escalar(286)));

            Control sensibilidad = ConstruirSensibilidad();
            sensibilidad.Margin = Padding.Empty;
            Control datos = ConstruirProteccionDatos();
            datos.Margin = Padding.Empty;
            columna.Controls.Add(sensibilidad, 0, 0);
            columna.Controls.Add(datos, 0, 2);
            return columna;
        }

        private Control ConstruirSensibilidad()
        {
            var tarjeta = NuevaTarjeta();
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 28, 26, 20, 40, 20, 40, 24, 36, 20, 40, 32, 48);
            tabla.Controls.Add(Tema.Etiqueta("Sensibilidad de la detección", 12, Tema.Texto, Tema.Tarjeta,
                FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta("Define cuánto tiempo con los ojos cerrados dispara cada aviso.",
                9, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            tabla.Controls.Add(Tema.Etiqueta("Segundos con ojos cerrados para marcar somnolencia", 9,
                Tema.TextoSuave, Tema.Tarjeta), 0, 2);
            tabla.Controls.Add(somnolientoNum, 0, 3);
            tabla.Controls.Add(Tema.Etiqueta("Segundos con ojos cerrados para activar la alarma", 9,
                Tema.TextoSuave, Tema.Tarjeta), 0, 4);
            tabla.Controls.Add(alarmaNum, 0, 5);
            tabla.Controls.Add(estadoSensibilidad, 0, 6);
            tabla.Controls.Add(sonidoCheck, 0, 7);
            tabla.Controls.Add(Tema.Etiqueta("Salida de audio de la alarma", 9, Tema.TextoSuave,
                Tema.Tarjeta), 0, 8);

            var audio = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
            audio.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            audio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Escalar(86)));
            audio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Escalar(106)));
            probarAudio.Dock = DockStyle.Fill;
            probarAudio.Margin = new Padding(8, 0, 0, 0);
            var recargar = new BotonConfiguracion("Actualizar", fantasma: true);
            recargar.Dock = DockStyle.Fill;
            recargar.Margin = new Padding(8, 0, 0, 0);
            recargar.Click += (_, _) => CargarSalidas((salidaAudio.SelectedItem as SalidaAudio)?.Id);
            probarAudio.Click += async (_, _) => await ProbarSalidaAsync();
            audio.Controls.Add(new MarcoEntrada(salidaAudio) { Dock = DockStyle.Fill }, 0, 0);
            audio.Controls.Add(probarAudio, 1, 0);
            audio.Controls.Add(recargar, 2, 0);
            tabla.Controls.Add(audio, 0, 9);
            tabla.Controls.Add(estadoAudio, 0, 10);

            var botones = NuevaBotonera();
            Button guardar = new BotonConfiguracion("Guardar cambios", primario: true);
            guardar.Width = Escalar(142);
            guardar.Click += Guardar_Click;
            Button restablecer = new BotonConfiguracion("Restablecer", fantasma: true);
            restablecer.Width = Escalar(120);
            restablecer.Click += Restablecer_Click;
            botones.Controls.Add(guardar);
            botones.Controls.Add(restablecer);
            tabla.Controls.Add(botones, 0, 11);

            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private Control ConstruirProteccionDatos()
        {
            var tarjeta = NuevaTarjeta();
            var tabla = Tema.TablaFilas(Tema.Tarjeta, 28, 26, 38, 58, 50, 0);
            tabla.Controls.Add(Tema.Etiqueta("Protección y conservación de datos", 11.5f, Tema.Texto,
                Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla.Controls.Add(Tema.Etiqueta("Decide cuánto tiempo se guardan los registros de cada conductor.",
                9, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            tabla.Controls.Add(retencionAutomatica, 0, 2);

            var periodos = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                BackColor = Tema.Tarjeta
            };
            periodos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            periodos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            Control historial = Campo("Historial (días)", diasHistorial);
            historial.Margin = new Padding(0, 0, 6, 0);
            Control biometria = Campo("Biometría (días)", diasBiometricos);
            biometria.Margin = new Padding(6, 0, 0, 0);
            periodos.Controls.Add(historial, 0, 0);
            periodos.Controls.Add(biometria, 1, 0);
            tabla.Controls.Add(periodos, 0, 3);

            var botones = NuevaBotonera();
            var guardarPolitica = new BotonConfiguracion("Guardar política", primario: true);
            var respaldar = new BotonConfiguracion("Crear respaldo");
            var restaurar = new BotonConfiguracion("Restaurar", fantasma: true);
            guardarPolitica.Width = Escalar(136);
            respaldar.Width = Escalar(126);
            restaurar.Width = Escalar(98);
            guardarPolitica.Click += (_, _) => GuardarPolitica();
            respaldar.Click += (_, _) => CrearRespaldo();
            restaurar.Click += (_, _) => RestaurarRespaldo();
            botones.Controls.Add(guardarPolitica);
            botones.Controls.Add(respaldar);
            botones.Controls.Add(restaurar);
            tabla.Controls.Add(botones, 0, 4);
            tabla.Controls.Add(estadoDatos, 0, 5);
            tarjeta.Controls.Add(tabla);
            return tarjeta;
        }

        private void OrganizarColumnas(bool forzar = false)
        {
            int anchoDisponible = Math.Max(0, ClientSize.Width - pagina.Padding.Horizontal);
            bool usarDos = anchoDisponible >= Escalar(880);
            if (!forzar && usarDos == dosColumnas)
            {
                pagina.Width = Math.Max(0, ClientSize.Width -
                    (VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
                return;
            }

            dosColumnas = usarDos;
            columnas.SuspendLayout();
            columnas.Controls.Clear();
            columnas.ColumnStyles.Clear();
            columnas.RowStyles.Clear();
            if (usarDos)
            {
                columnas.ColumnCount = 2;
                columnas.RowCount = 1;
                columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                columnas.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                columnaIzquierda.Margin = new Padding(0, 0, 9, 0);
                panelCamara.Margin = new Padding(9, 0, 0, 0);
                columnas.Controls.Add(columnaIzquierda, 0, 0);
                columnas.Controls.Add(panelCamara, 1, 0);
                pagina.RowStyles[1].Height = Escalar(747);
                pagina.Height = Escalar(904);
            }
            else
            {
                columnas.ColumnCount = 1;
                columnas.RowCount = 2;
                columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                columnas.RowStyles.Add(new RowStyle(SizeType.Absolute, Escalar(747)));
                columnas.RowStyles.Add(new RowStyle(SizeType.Absolute, Escalar(850)));
                columnaIzquierda.Margin = new Padding(0, 0, 0, 9);
                panelCamara.Margin = new Padding(0, 9, 0, 0);
                columnas.Controls.Add(columnaIzquierda, 0, 0);
                columnas.Controls.Add(panelCamara, 0, 1);
                pagina.RowStyles[1].Height = Escalar(1615);
                pagina.Height = Escalar(1772);
            }
            pagina.Width = Math.Max(0, ClientSize.Width -
                (VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
            columnas.ResumeLayout(true);
        }

        private PanelRedondeado NuevaTarjeta() => new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Escalar(22)),
            Radio = Escalar(16)
        };

        private static FlowLayoutPanel NuevaBotonera() => new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Tema.Tarjeta,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 0, 0)
        };

        private static Control Campo(string etiqueta, Control entrada)
        {
            var campo = Tema.TablaFilas(Tema.Tarjeta, 22, 0);
            campo.Controls.Add(Tema.Etiqueta(etiqueta, 9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            campo.Controls.Add(entrada, 0, 1);
            return campo;
        }

        private static void ConfigurarNumerico(SelectorNumerico numerico, decimal minimo, decimal maximo,
            int decimales)
        {
            numerico.DecimalPlaces = decimales;
            numerico.Increment = decimales == 0 ? 1 : 0.5M;
            numerico.Minimum = minimo;
            numerico.Maximum = maximo;
            numerico.Dock = DockStyle.Fill;
            numerico.Margin = Padding.Empty;
        }

        private static void ConfigurarSelector(ComboBox selector)
        {
            selector.FlatStyle = FlatStyle.Flat;
            selector.Font = Tema.Fuente(10);
            selector.BackColor = Tema.TarjetaClara;
            selector.ForeColor = Tema.Texto;
            selector.Margin = Padding.Empty;
        }

        private int Escalar(int valor) => LogicalToDeviceUnits(valor);

        private void ActualizarEstadoSonido()
        {
            salidaAudio.Enabled = probarAudio.Enabled = sonidoCheck.Checked;
            if (!sonidoCheck.Checked)
                estadoAudio.Text = "El sonido de la alarma está desactivado.";
            else if (estadoAudio.Text == "El sonido de la alarma está desactivado.")
                estadoAudio.Text = "";
        }

        private void ActualizarEstadoRetencion()
        {
            diasHistorial.Enabled = diasBiometricos.Enabled = retencionAutomatica.Checked;
        }

        private bool ValidarTiempos(bool enfocar)
        {
            bool valido = alarmaNum.Value > somnolientoNum.Value;
            estadoSensibilidad.Text = valido
                ? ""
                : "La alarma debe activarse después de marcar la somnolencia.";
            if (!valido && enfocar)
                alarmaNum.EnfocarEntrada();
            return valido;
        }

        private void CargarValores()
        {
            AjustesDeteccion ajustes = config.Deteccion;
            somnolientoNum.Value = (decimal)ajustes.SegundosSomnoliento;
            alarmaNum.Value = (decimal)ajustes.SegundosAlarma;
            sonidoCheck.Checked = ajustes.SonidoActivado;
            CargarSalidas(ajustes.SalidaAudioId);
            ActualizarEstadoSonido();
            ValidarTiempos(false);
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
                    probarAudio.Enabled = sonidoCheck.Checked;
            }
        }

        private void Guardar_Click(object? remitente, EventArgs evento)
        {
            if (!ValidarTiempos(true))
                return;

            var ajustes = new AjustesDeteccion((double)somnolientoNum.Value, (double)alarmaNum.Value,
                sonidoCheck.Checked, (salidaAudio.SelectedItem as SalidaAudio)?.Id);
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

        private void CargarPolitica()
        {
            try
            {
                PoliticaProteccionDatos p = politicaRepository.Leer();
                retencionAutomatica.Checked = p.EliminacionAutomatica;
                diasHistorial.Value = p.DiasHistorial;
                diasBiometricos.Value = p.DiasPerfilesBiometricos;
                ActualizarEstadoRetencion();
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            {
                estadoDatos.Text = "No se pudo leer la política: " + ex.Message;
            }
        }

        private void GuardarPolitica()
        {
            try
            {
                var p = new PoliticaProteccionDatos(retencionAutomatica.Checked,
                    (int)diasHistorial.Value, (int)diasBiometricos.Value);
                politicaRepository.Guardar(p);
                var eliminados = proteccionDatos.AplicarRetencion(p, DateTime.Now);
                estadoDatos.Text = $"Política guardada · sesiones eliminadas: {eliminados.Sesiones} · perfiles: {eliminados.Perfiles}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(FindForm(), ex.Message, "Protección de datos", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CrearRespaldo()
        {
            using var clave = new VentanaContrasenaRespaldo("Crear respaldo protegido");
            if (clave.ShowDialog(FindForm()) != DialogResult.OK) return;
            using var dialogo = new SaveFileDialog { Filter = "Respaldo FatigueDetect (*.fdbak)|*.fdbak",
                FileName = $"FatigueDetect-{DateTime.Now:yyyyMMdd-HHmm}.fdbak" };
            if (dialogo.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                proteccionDatos.CrearRespaldo(dialogo.FileName, clave.Contrasena);
                estadoDatos.Text = "Respaldo cifrado creado correctamente.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Respaldo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestaurarRespaldo()
        {
            using var dialogo = new OpenFileDialog { Filter = "Respaldo FatigueDetect (*.fdbak)|*.fdbak" };
            if (dialogo.ShowDialog(FindForm()) != DialogResult.OK) return;
            using var clave = new VentanaContrasenaRespaldo("Restaurar respaldo protegido");
            if (clave.ShowDialog(FindForm()) != DialogResult.OK) return;
            if (MessageBox.Show(FindForm(), "La restauración reemplazará los datos actuales. ¿Continuar?",
                "Restaurar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                proteccionDatos.RestaurarRespaldo(dialogo.FileName, clave.Contrasena);
                MessageBox.Show(FindForm(),
                    "Respaldo validado y restaurado. Reinicia FatigueDetect antes de continuar.",
                    "Restauración completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Restauración rechazada", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
