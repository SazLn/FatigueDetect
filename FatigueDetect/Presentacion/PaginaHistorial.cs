using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    /// <summary>Sesiones guardadas y cronología de eventos de la sesión seleccionada.</summary>
    public class PaginaHistorial : UserControl
    {
        private readonly SesionService sesionService;

        private DataGridView tabla = null!;
        private Label tablaVacia = null!;
        private readonly DetalleHistorialControl detalle = new();
        private List<SesionConduccion> sesiones = new();
        private readonly ComboBox filtroAlarmas = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox filtroCalidad = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private List<SesionConduccion> visibles = new();
        private readonly DateTimePicker desde = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
        private readonly DateTimePicker hasta = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
        private readonly TextBox conductor = new() { PlaceholderText = "Nombre o cédula" };
        private readonly TextBox vehiculo = new() { PlaceholderText = "Placa o tipo" };
        private Label estadoFiltros = null!;

        public PaginaHistorial(SesionService sesionService)
        {
            this.sesionService = sesionService;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(30, 26, 16, 24);
            AutoScaleMode = AutoScaleMode.Dpi;

            // Reserva separación real entre cabecera y filtros incluso al escalar DPI.
            var raiz = Tema.TablaFilas(Tema.Fondo, 82, 70, 26, 0);

            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Historial de sesiones", 20, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            Label archivo = Tema.Etiqueta(sesionService.RutaHistorial, 9.5f, Tema.TextoSuave, Tema.Fondo);
            archivo.Font = new Font("Consolas", 8.5f, FontStyle.Regular);
            archivo.AutoEllipsis = true;
            encabezado.Controls.Add(archivo, 0, 1);
            var cabecera = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty
            };
            cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
            cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            cabecera.Controls.Add(encabezado, 0, 0);

            Button estadisticas = new BotonConfiguracion("Estadísticas");
            estadisticas.Anchor = AnchorStyles.None;
            estadisticas.Width = 112;
            estadisticas.Click += (remitente, evento) => MostrarEstadisticas();
            cabecera.Controls.Add(estadisticas, 1, 0);

            Button excel = new BotonConfiguracion("Exportar Excel", primario: true);
            excel.Anchor = AnchorStyles.None;
            excel.Width = 122;
            excel.Click += (remitente, evento) => ExportarExcel();
            cabecera.Controls.Add(excel, 2, 0);
            raiz.Controls.Add(cabecera, 0, 0);

            var cuerpo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Tema.Fondo,
                Margin = Padding.Empty
            };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 386));
            cuerpo.Controls.Add(ConstruirTabla(), 0, 0);
            cuerpo.Controls.Add(ConstruirDetalleNuevo(), 1, 0);
            raiz.Controls.Add(ConstruirFiltros(), 0, 1);
            raiz.Controls.Add(estadoFiltros, 0, 2);
            raiz.Controls.Add(cuerpo, 0, 3);

            Controls.Add(raiz);
            MostrarDetalle(null);
        }

        private Control ConstruirFiltros()
        {
            var filtros = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1,
                Margin = Padding.Empty, BackColor = Tema.Fondo };
            filtros.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            for (int i = 0; i < 6; i++)
                filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
            filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));

            AgregarFiltro(filtros, 0, "Desde", desde);
            AgregarFiltro(filtros, 1, "Hasta", hasta);
            AgregarFiltro(filtros, 2, "Conductor", conductor);
            AgregarFiltro(filtros, 3, "Vehículo", vehiculo);

            filtroAlarmas.Items.AddRange(["Todas", "Con alarmas", "Sin alarmas"]);
            filtroCalidad.Items.AddRange(["Todas", $"Baja (<{CalidadSesion.UmbralCalidadBaja})", "Con puntaje", "Sin puntaje", "Con caídas FPS"]);
            filtroAlarmas.SelectedIndex = filtroCalidad.SelectedIndex = 0;
            AgregarFiltro(filtros, 4, "Alarmas", filtroAlarmas);
            AgregarFiltro(filtros, 5, "Calidad de detección", filtroCalidad);

            Button limpiar = new BotonConfiguracion("Limpiar filtros", fantasma: true);
            limpiar.Dock = DockStyle.Fill;
            limpiar.Margin = Padding.Empty;
            limpiar.Click += (_, _) =>
            {
                desde.Checked = hasta.Checked = false;
                conductor.Clear();
                vehiculo.Clear();
                filtroAlarmas.SelectedIndex = filtroCalidad.SelectedIndex = 0;
                AplicarFiltros();
            };
            // Alinea esta acción con los campos; la fila superior conserva el espacio
            // de etiqueta que tienen el resto de filtros.
            var contenedorLimpiar = Tema.TablaFilas(Tema.Fondo, 20, 40);
            contenedorLimpiar.Margin = Padding.Empty;
            contenedorLimpiar.Controls.Add(limpiar, 0, 1);
            filtros.Controls.Add(contenedorLimpiar, 6, 0);
            estadoFiltros = Tema.Etiqueta("", 9, Tema.TextoSuave, Tema.Fondo);

            desde.ValueChanged += (_, _) => AplicarFiltros();
            hasta.ValueChanged += (_, _) => AplicarFiltros();
            conductor.TextChanged += (_, _) => AplicarFiltros();
            vehiculo.TextChanged += (_, _) => AplicarFiltros();
            filtroAlarmas.SelectedIndexChanged += (_, _) => AplicarFiltros();
            filtroCalidad.SelectedIndexChanged += (_, _) => AplicarFiltros();
            return filtros;
        }

        private static void AgregarFiltro(TableLayoutPanel filtros, int columna, string etiqueta, Control control)
        {
            var campo = Tema.TablaFilas(Tema.Fondo, 20, 40);
            campo.Margin = new Padding(0, 0, 10, 0);
            campo.Controls.Add(Tema.Etiqueta(etiqueta, 9, Tema.TextoSuave, Tema.Fondo), 0, 0);
            control.Dock = DockStyle.Fill;
            control.AccessibleName = etiqueta;
            if (control is TextBox texto)
            {
                texto.BorderStyle = BorderStyle.None;
                texto.Font = Tema.Fuente(9.5f);
            }
            else if (control is ComboBox lista)
            {
                lista.FlatStyle = FlatStyle.Flat;
                lista.Font = Tema.Fuente(9.5f);
            }
            else if (control is DateTimePicker fecha)
            {
                fecha.Font = Tema.Fuente(9.5f);
                fecha.CalendarForeColor = Tema.Texto;
                fecha.CalendarMonthBackground = Tema.TarjetaClara;
            }
            campo.Controls.Add(new MarcoEntrada(control) { Dock = DockStyle.Fill }, 0, 1);
            filtros.Controls.Add(campo, columna, 0);
        }

        private Control ConstruirTabla()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
            var contenido = Tema.TablaFilas(Tema.Tarjeta, 0, 56);

            tabla = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Tema.Tarjeta,
                BorderStyle = BorderStyle.None,
                GridColor = Tema.Borde,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 42,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                Margin = new Padding(0),
                Font = Tema.Fuente(9.5f)
            };
            tabla.RowTemplate.Height = 43;

            tabla.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(38, 32, 64);
            tabla.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Texto;
            tabla.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 32, 64);
            tabla.ColumnHeadersDefaultCellStyle.Font = Tema.Fuente(9.5f, FontStyle.Bold);

            tabla.DefaultCellStyle.BackColor = Tema.Tarjeta;
            tabla.DefaultCellStyle.ForeColor = Tema.Texto;
            tabla.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 41, 92);
            tabla.DefaultCellStyle.SelectionForeColor = Tema.Texto;
            tabla.DefaultCellStyle.Font = Tema.Fuente(9.5f);

            tabla.Columns.Add("fecha", "Fecha y hora");
            tabla.Columns.Add("conductor", "Conductor");
            tabla.Columns.Add("cedula", "Cédula");
            tabla.Columns.Add("vehiculo", "Vehículo");
            tabla.Columns.Add("placa", "Placa");
            tabla.Columns.Add("fin", "Fin");
            tabla.Columns.Add("duracion", "Duración");
            tabla.Columns.Add("alarmas", "Alarmas");
            tabla.Columns.Add("estado", "Estado");
            tabla.Columns["fecha"]!.Width = 155;
            tabla.Columns["conductor"]!.Width = 142;
            tabla.Columns["cedula"]!.Width = 104;
            tabla.Columns["vehiculo"]!.Width = 92;
            tabla.Columns["placa"]!.Width = 80;
            tabla.Columns["fin"]!.Width = 92;
            tabla.Columns["duracion"]!.Width = 92;
            tabla.Columns["alarmas"]!.Width = 76;
            tabla.Columns["estado"]!.Width = 120;
            tabla.Columns["fecha"]!.Frozen = true;

            tabla.SelectionChanged += (remitente, evento) =>
            {
                if (tabla.SelectedRows.Count > 0)
                {
                    MostrarDetalle(tabla.SelectedRows[0].Tag as SesionConduccion);
                }
            };

            tabla.CellPainting += PintarCeldaTabla;

            var marcoTabla = new PanelRedondeado
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Radio = 12,
                ColorTarjeta = Tema.Tarjeta,
                ColorBorde = Tema.Borde
            };
            tablaVacia = Tema.Etiqueta("Ninguna sesión coincide con los filtros.\nPrueba con «Limpiar filtros».",
                10, Tema.TextoSuave, Tema.Tarjeta, alineacion: ContentAlignment.MiddleCenter);
            tablaVacia.Visible = false;
            marcoTabla.Controls.Add(tabla);
            marcoTabla.Controls.Add(tablaVacia);
            contenido.Controls.Add(marcoTabla, 0, 0);

            // Botones inferiores
            var botones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.Tarjeta,
                Margin = new Padding(0),
                Padding = new Padding(0, 10, 0, 0)
            };

            Button eliminar = new BotonConfiguracion("Eliminar historial", peligro: true);
            eliminar.Width = 140;
            eliminar.Click += Eliminar_Click;

            Button carpeta = new BotonConfiguracion("Abrir carpeta");
            carpeta.Width = 116;
            carpeta.Click += (remitente, evento) => AbrirCarpeta();

            Button actualizar = new BotonConfiguracion("Actualizar");
            actualizar.Width = 102;
            actualizar.Click += (remitente, evento) => Recargar(null);

            botones.Controls.Add(eliminar);
            botones.Controls.Add(carpeta);
            botones.Controls.Add(actualizar);
            contenido.Controls.Add(botones, 0, 1);

            tarjeta.Controls.Add(contenido);
            return tarjeta;
        }

        private Control ConstruirDetalleNuevo()
        {
            var tarjeta = new PanelRedondeado
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0),
                Padding = new Padding(14),
                Radio = 16
            };
            tarjeta.Controls.Add(detalle);
            return tarjeta;
        }

        private void PintarCeldaTabla(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            Graphics? graphics = e.Graphics;
            if (graphics is null)
                return;

            if (tabla.Columns[e.ColumnIndex].Name == "estado")
            {
                e.PaintBackground(e.CellBounds, true);
                bool conAlertas = tabla.Rows[e.RowIndex].Tag is SesionConduccion s && s.CantidadAlarmas > 0;
                Color color = conAlertas ? Tema.Rojo : Tema.Verde;
                int y = e.CellBounds.Top + (e.CellBounds.Height - 8) / 2;
                using var punto = new SolidBrush(color);
                graphics.FillEllipse(punto, e.CellBounds.Left + 12, y, 8, 8);
                var texto = new Rectangle(e.CellBounds.Left + 28, e.CellBounds.Top,
                    Math.Max(0, e.CellBounds.Width - 30), e.CellBounds.Height);
                TextRenderer.DrawText(graphics, conAlertas ? "Con alertas" : "Sin alertas",
                    tabla.Font, texto, color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.Handled = true;
                return;
            }

            if (e.ColumnIndex == 0 && tabla.Rows[e.RowIndex].Selected)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All);
                using var acento = new SolidBrush(Tema.Azul);
                graphics.FillRectangle(acento, e.CellBounds.Left, e.CellBounds.Top, 3, e.CellBounds.Height);
                e.Handled = true;
            }
        }

        /// <summary>Recarga la base de datos y selecciona la sesión indicada.</summary>
        public void Recargar(SesionConduccion? resaltar)
        {
            Guid? seleccionada = resaltar?.Id ?? (tabla.CurrentRow?.Tag as SesionConduccion)?.Id;
            try
            {
                sesiones = sesionService.ObtenerHistorial();
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "No se pudo leer el historial:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                sesiones = new List<SesionConduccion>();
            }

            AplicarFiltros(seleccionada);
        }

        private void AplicarFiltros(Guid? resaltar = null)
        {
            resaltar ??= (tabla.CurrentRow?.Tag as SesionConduccion)?.Id;
            FiltroHistorial filtro = FiltroActual();
            bool rangoValido = filtro.Desde <= filtro.Hasta || !filtro.Desde.HasValue || !filtro.Hasta.HasValue;
            visibles = rangoValido ? filtro.Aplicar(sesiones) : new();
            estadoFiltros.Text = rangoValido
                ? $"{visibles.Count} de {sesiones.Count} sesiones"
                : "La fecha inicial debe ser anterior o igual a la final.";
            estadoFiltros.ForeColor = rangoValido ? Tema.TextoSuave : Tema.Rojo;
            tabla.Rows.Clear();
            DataGridViewRow? filaResaltada = null;

            foreach (SesionConduccion s in visibles.OrderByDescending(x => x.Inicio))
            {
                int indice = tabla.Rows.Add(
                    s.Inicio.ToString("dd/MM/yyyy HH:mm"),
                    s.Conductor.NombreCompleto,
                    s.Conductor.Cedula,
                    s.Vehiculo.TipoCarro,
                    s.Vehiculo.Placa,
                    s.Fin?.ToString("HH:mm:ss") ?? "-",
                    s.Fin.HasValue ? Tema.FormatoDuracion(s.Duracion) : "—",
                    s.CantidadAlarmas,
                    s.CantidadAlarmas > 0 ? "Con alertas" : "Sin alertas");

                DataGridViewRow fila = tabla.Rows[indice];
                fila.Tag = s;
                fila.Cells["estado"].Style.ForeColor = s.CantidadAlarmas > 0 ? Tema.Rojo : Tema.Verde;

                if (s.Id == resaltar)
                {
                    filaResaltada = fila;
                }
            }

            if (tabla.Rows.Count > 0)
            {
                tablaVacia.Visible = false;
                DataGridViewRow seleccionada = filaResaltada ?? tabla.Rows[0];
                tabla.ClearSelection();
                seleccionada.Selected = true;
                tabla.CurrentCell = seleccionada.Cells[0];
                MostrarDetalle(seleccionada.Tag as SesionConduccion);
            }
            else
            {
                tablaVacia.Visible = true;
                tablaVacia.BringToFront();
                MostrarDetalle(null);
            }
        }

        private void MostrarDetalle(SesionConduccion? s)
        {
            detalle.Mostrar(s);
        }

        internal static string NombreEvento(EventoSesion evento) => evento.Tipo == TipoEventoSesion.EventoExterno
            ? evento.CodigoTipo ?? "Evento externo" : NombreEvento(evento.Tipo);

        internal static string NombreEvento(TipoEventoSesion tipo) => tipo switch
        {
            TipoEventoSesion.CierreOcularProlongado => "Posible microsueño",
            TipoEventoSesion.Alarma => "Alarma",
            TipoEventoSesion.RostroNoDetectado => "Rostro no detectado",
            TipoEventoSesion.VariosRostros => "Varios rostros",
            TipoEventoSesion.LuzInsuficiente => "Luz insuficiente",
            TipoEventoSesion.LuzExcesiva => "Luz excesiva",
            TipoEventoSesion.CamaraTapada => "Cámara tapada",
            TipoEventoSesion.CamaraDesenfocada => "Cámara desenfocada",
            TipoEventoSesion.CamaraCongelada => "Cámara congelada",
            TipoEventoSesion.Cabeceo => "Posible cabeceo",
            TipoEventoSesion.GiroIzquierda => "Giro hacia la izquierda",
            TipoEventoSesion.GiroDerecha => "Giro hacia la derecha",
            TipoEventoSesion.CabezaArriba => "Cabeza hacia arriba",
            TipoEventoSesion.CabezaAbajo => "Cabeza hacia abajo",
            TipoEventoSesion.FueraZonaFrontal => "Fuera de la zona frontal",
            TipoEventoSesion.PosiblePosturaEncorvada => "Posible postura encorvada",
            TipoEventoSesion.RostroParcial => "Rostro parcialmente fuera del encuadre",
            TipoEventoSesion.PosibleBostezo => "Posible bostezo",
            TipoEventoSesion.CierreOcularAnomalo => "Cierre ocular anómalo",
            TipoEventoSesion.AlertaPreventiva => "Alerta preventiva",
            TipoEventoSesion.AlertaAlta => "Alerta alta",
            TipoEventoSesion.AlertaCritica => "Alerta crítica",
            TipoEventoSesion.PosiblesGafasOscuras => "Posibles gafas oscuras",
            TipoEventoSesion.PosibleMascarilla => "Posible mascarilla",
            TipoEventoSesion.PosibleGorra => "Posible gorra u obstrucción superior",
            TipoEventoSesion.MedicionFacialNoConfiable => "Medición facial no confiable",
            _ => tipo.ToString()
        };

        private FiltroHistorial FiltroActual() => new(desde.Checked ? desde.Value.Date : null,
            hasta.Checked ? hasta.Value.Date : null, conductor.Text, vehiculo.Text,
            (FiltroAlarmas)filtroAlarmas.SelectedIndex, (FiltroCalidad)filtroCalidad.SelectedIndex);

        private void MostrarEstadisticas()
        {
            if (FiltroActual() is { Desde: DateTime inicio, Hasta: DateTime fin } && inicio > fin)
                return;

            try
            {
                using var dashboard = new DashboardHistorial(new ResumenHistorial(visibles), sesiones, FiltroActual());
                dashboard.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudieron consultar las estadísticas.\n" + ex.Message,
                    "Estadísticas", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ExportarExcel()
        {
            using var dialogo = new SaveFileDialog
            {
                Filter = "Libro de Excel (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = $"FatigueDetect-{DateTime.Today:yyyy-MM-dd}.xlsx"
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;

            Enabled = false;
            try
            {
                string ruta = dialogo.FileName;
                var seleccion = visibles.ToArray();
                FiltroHistorial filtro = FiltroActual();
                string alcance = $"{seleccion.Length} sesiones exportadas. Fechas desde {filtro.Desde?.ToString("dd/MM/yyyy") ?? "el primer registro"} " +
                    $"hasta {filtro.Hasta?.ToString("dd/MM/yyyy") ?? "el último registro"}. " +
                    $"Conductor: {ValorFiltro(filtro.Conductor)}. Vehículo: {ValorFiltro(filtro.Vehiculo)}. " +
                    $"Alarmas: {filtroAlarmas.Text}. Calidad: {filtroCalidad.Text}.";
                await System.Threading.Tasks.Task.Run(() =>
                    ExportadorHistorialExcel.Guardar(ruta, seleccion, alcance));
                MessageBox.Show(this, "El historial se exportó correctamente.",
                    "Exportar Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo exportar el historial.\n" + ex.Message,
                    "Exportar Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Enabled = true;
            }
        }

        private static string ValorFiltro(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? "Todos" : valor.Trim();

        private void Eliminar_Click(object? remitente, EventArgs evento)
        {
            DialogResult respuesta = MessageBox.Show(
                FindForm(),
                "¿Está seguro de que desea borrar el historial?\n\n" +
                "Se eliminarán todas las sesiones y eventos guardados en SQLite. El CSV anterior se conserva.",
                "Confirmar eliminación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (respuesta != DialogResult.Yes)
            {
                return;
            }

            try
            {
                sesionService.BorrarHistorial();
                Recargar(null);
                MessageBox.Show(FindForm(), "El historial fue eliminado.", "Historial eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(),
                    "No se pudo eliminar el historial.\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AbrirCarpeta()
        {
            try
            {
                string ruta = sesionService.RutaHistorial;
                string argumento = System.IO.File.Exists(ruta)
                    ? $"/select,\"{ruta}\""
                    : $"\"{System.IO.Path.GetDirectoryName(ruta)}\"";

                Process.Start(new ProcessStartInfo("explorer.exe", argumento) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "No se pudo abrir la carpeta: " + ex.Message);
            }
        }
    }
}
