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
        private Label dConductor = null!;
        private Label dCedula = null!;
        private Label dVehiculo = null!;
        private Label dPlaca = null!;
        private Label dInicio = null!;
        private Label dFin = null!;
        private Label dDuracion = null!;
        private Label dAlarmas = null!;
        private Label dCalidad = null!;
        private ListBox dResumen = null!;
        private ListBox dEventos = null!;

        public PaginaHistorial(SesionService sesionService)
        {
            this.sesionService = sesionService;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(22);

            var raiz = Tema.TablaFilas(Tema.Fondo, 84, 0);

            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Historial de sesiones", 20, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            Label archivo = Tema.Etiqueta(sesionService.RutaHistorial, 9.5f, Tema.TextoSuave, Tema.Fondo);
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

            Button estadisticas = Tema.Boton("Estadísticas", Tema.TarjetaClara, Tema.Texto);
            estadisticas.Anchor = AnchorStyles.None;
            estadisticas.Width = 112;
            estadisticas.Click += (remitente, evento) => MostrarEstadisticas();
            cabecera.Controls.Add(estadisticas, 1, 0);

            Button excel = Tema.Boton("Exportar Excel", Tema.TarjetaClara, Tema.Texto);
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
                BackColor = Tema.Fondo
            };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            cuerpo.Controls.Add(ConstruirTabla(), 0, 0);
            cuerpo.Controls.Add(ConstruirDetalle(), 1, 0);
            raiz.Controls.Add(cuerpo, 0, 1);

            Controls.Add(raiz);
            MostrarDetalle(null);
        }

        private Control ConstruirTabla()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            var contenido = Tema.TablaFilas(Tema.Tarjeta, 0, 52);

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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Tema.Tarjeta,
                BorderStyle = BorderStyle.None,
                GridColor = Tema.Borde,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 38,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                Margin = new Padding(0)
            };
            tabla.RowTemplate.Height = 34;

            tabla.ColumnHeadersDefaultCellStyle.BackColor = Tema.TarjetaClara;
            tabla.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Texto;
            tabla.ColumnHeadersDefaultCellStyle.SelectionBackColor = Tema.TarjetaClara;
            tabla.ColumnHeadersDefaultCellStyle.Font = Tema.Fuente(9.5f, FontStyle.Bold);

            tabla.DefaultCellStyle.BackColor = Tema.Tarjeta;
            tabla.DefaultCellStyle.ForeColor = Tema.Texto;
            tabla.DefaultCellStyle.SelectionBackColor = Tema.AzulOscuro;
            tabla.DefaultCellStyle.SelectionForeColor = Color.White;
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
            tabla.Columns["conductor"]!.FillWeight = 150;
            tabla.Columns["fecha"]!.FillWeight = 120;
            tabla.Columns["alarmas"]!.FillWeight = 70;
            tabla.Columns["placa"]!.FillWeight = 80;

            tabla.SelectionChanged += (remitente, evento) =>
            {
                if (tabla.SelectedRows.Count > 0)
                {
                    MostrarDetalle(tabla.SelectedRows[0].Tag as SesionConduccion);
                }
            };

            contenido.Controls.Add(tabla, 0, 0);

            // Botones inferiores
            var botones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.Tarjeta,
                Margin = new Padding(0),
                Padding = new Padding(0, 10, 0, 0)
            };

            Button eliminar = Tema.Boton("Eliminar historial", Tema.Rojo, Color.White);
            eliminar.Width = 180;
            eliminar.Click += Eliminar_Click;

            Button carpeta = Tema.Boton("Abrir carpeta", Tema.TarjetaClara, Tema.Texto);
            carpeta.Width = 150;
            carpeta.Click += (remitente, evento) => AbrirCarpeta();

            Button actualizar = Tema.Boton("Actualizar", Tema.TarjetaClara, Tema.Texto);
            actualizar.Width = 130;
            actualizar.Click += (remitente, evento) => Recargar(null);

            botones.Controls.Add(eliminar);
            botones.Controls.Add(carpeta);
            botones.Controls.Add(actualizar);
            contenido.Controls.Add(botones, 0, 1);

            tarjeta.Controls.Add(contenido);
            return tarjeta;
        }

        private Control ConstruirDetalle()
        {
            var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 0, 0) };
            var tabla2 = Tema.TablaFilas(Tema.Tarjeta, 36, 39, 39, 39, 39, 39, 39, 39, 39, 65, 26, 0, 26, 0);

            tabla2.Controls.Add(Tema.Etiqueta("Detalle de la sesión", 13, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla2.Controls.Add(Dato("Conductor", out dConductor), 0, 1);
            tabla2.Controls.Add(Dato("Cédula", out dCedula), 0, 2);
            tabla2.Controls.Add(Dato("Vehículo", out dVehiculo), 0, 3);
            tabla2.Controls.Add(Dato("Placa", out dPlaca), 0, 4);
            tabla2.Controls.Add(Dato("Inicio", out dInicio), 0, 5);
            tabla2.Controls.Add(Dato("Fin", out dFin), 0, 6);
            tabla2.Controls.Add(Dato("Duración", out dDuracion), 0, 7);
            tabla2.Controls.Add(Dato("Alarmas activadas", out dAlarmas), 0, 8);
            dCalidad = Tema.Etiqueta("Calidad: sin medición", 9, Tema.Texto, Tema.Tarjeta);
            tabla2.Controls.Add(dCalidad, 0, 9);
            tabla2.Controls.Add(Tema.Etiqueta("Resumen de eventos", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 10);

            dResumen = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Tema.Tarjeta,
                ForeColor = Tema.Texto,
                BorderStyle = BorderStyle.None,
                Font = Tema.Fuente(9),
                Margin = new Padding(0),
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
            tabla2.Controls.Add(dResumen, 0, 11);
            tabla2.Controls.Add(Tema.Etiqueta("Eventos de la sesión", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 12);

            dEventos = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Tema.Tarjeta,
                ForeColor = Tema.Texto,
                BorderStyle = BorderStyle.None,
                Font = Tema.Fuente(10),
                Margin = new Padding(0),
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
            tabla2.Controls.Add(dEventos, 0, 13);

            tarjeta.Controls.Add(tabla2);
            return tarjeta;
        }

        // Título pequeño + valor en negrita.
        private static Control Dato(string titulo, out Label valor)
        {
            var celda = Tema.TablaFilas(Tema.Tarjeta, 16, 21);
            celda.Controls.Add(Tema.Etiqueta(titulo, 8.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            valor = Tema.Etiqueta("—", 11, Tema.Texto, Tema.Tarjeta, FontStyle.Bold);
            celda.Controls.Add(valor, 0, 1);
            return celda;
        }

        /// <summary>Recarga la base de datos y selecciona la sesión indicada.</summary>
        public void Recargar(SesionConduccion? resaltar)
        {
            List<SesionConduccion> sesiones;
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

            tabla.Rows.Clear();
            DataGridViewRow? filaResaltada = null;

            foreach (SesionConduccion s in sesiones.OrderByDescending(x => x.Inicio))
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

                if (s.Id == resaltar?.Id)
                {
                    filaResaltada = fila;
                }
            }

            if (tabla.Rows.Count > 0)
            {
                DataGridViewRow seleccionada = filaResaltada ?? tabla.Rows[0];
                tabla.ClearSelection();
                seleccionada.Selected = true;
                tabla.CurrentCell = seleccionada.Cells[0];
                MostrarDetalle(seleccionada.Tag as SesionConduccion);
            }
            else
            {
                MostrarDetalle(null);
            }
        }

        private void MostrarDetalle(SesionConduccion? s)
        {
            dResumen.Items.Clear();
            dEventos.Items.Clear();
            dCalidad.Text = s?.Calidad is CalidadSesion calidad
                ? $"FPS: {calidad.Fps?.ToString("0.0") ?? "—"}   ·   Analizables: {calidad.ProporcionAnalizable?.ToString("P1") ?? "—"}\n" +
                  $"Rostro válido: {calidad.TiempoRostroValido.TotalSeconds:0.0} s\n" +
                  $"Fallos de cámara: {calidad.FallosCamara}"
                : "Calidad: sin medición";

            if (s == null)
            {
                dConductor.Text = "—";
                dCedula.Text = "—";
                dVehiculo.Text = "—";
                dPlaca.Text = "—";
                dInicio.Text = "—";
                dFin.Text = "—";
                dDuracion.Text = "—";
                dAlarmas.Text = "—";
                dResumen.Items.Add("Sin registros");
                dEventos.Items.Add("Sin registros");
                return;
            }

            dConductor.Text = s.Conductor.NombreCompleto;
            dCedula.Text = s.Conductor.Cedula;
            dVehiculo.Text = s.Vehiculo.TipoCarro;
            dPlaca.Text = s.Vehiculo.Placa;
            dInicio.Text = s.Inicio.ToString("dd/MM/yyyy HH:mm:ss");
            dFin.Text = s.Fin?.ToString("dd/MM/yyyy HH:mm:ss") ?? "-";
            dDuracion.Text = s.Fin.HasValue ? Tema.FormatoDuracion(s.Duracion) : "—";
            dAlarmas.Text = s.CantidadAlarmas.ToString();

            List<EventoSesion> eventos = s.Eventos;
            int alarmasSinHora = ResumenHistorial.AlarmasSinHora(s, eventos);
            foreach (ResumenEvento resumen in ResumenHistorial.PorTipo(s, eventos))
            {
                string tiempo = resumen.TotalMs.HasValue
                    ? $" · total {resumen.TotalMs / 1000.0:0.0} s · máximo {resumen.MaximoMs / 1000.0:0.0} s"
                    : "";
                string desconocidas = resumen.SinDuracion > 0 ? $" · {resumen.SinDuracion} sin duración" : "";
                dResumen.Items.Add($"{NombreEvento(resumen.Tipo)} · {resumen.Cantidad}{tiempo}{desconocidas}");
            }
            if (dResumen.Items.Count == 0)
                dResumen.Items.Add("Sin eventos registrados");

            if (eventos.Count == 0 && s.CantidadAlarmas == 0)
                dEventos.Items.Add("Sin eventos registrados");

            foreach (EventoSesion evento in eventos.OrderBy(e => e.Inicio))
            {
                string inicio = evento.Inicio.LocalDateTime.ToString("dd/MM HH:mm:ss.fff");
                string duracion = evento.DuracionMilisegundos is long ms
                    ? $" · {ms / 1000.0:0.000} s" : "";
                dEventos.Items.Add($"{inicio} · {NombreEvento(evento.Tipo)}{duracion}");
            }

            if (alarmasSinHora > 0)
                dEventos.Items.Add($"Alarmas sin hora registrada · {alarmasSinHora}");
        }

        internal static string NombreEvento(TipoEventoSesion tipo) => tipo switch
        {
            TipoEventoSesion.CierreOcularProlongado => "Posible cierre ocular prolongado",
            TipoEventoSesion.Alarma => "Alarma",
            TipoEventoSesion.RostroNoDetectado => "Rostro no detectado",
            TipoEventoSesion.VariosRostros => "Varios rostros",
            TipoEventoSesion.LuzInsuficiente => "Luz insuficiente",
            TipoEventoSesion.LuzExcesiva => "Luz excesiva",
            _ => tipo.ToString()
        };

        private void MostrarEstadisticas()
        {
            try
            {
                using var dashboard = new DashboardHistorial(new ResumenHistorial(sesionService.ObtenerHistorial()));
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
                await System.Threading.Tasks.Task.Run(() =>
                    ExportadorHistorialExcel.Guardar(ruta, sesionService.ObtenerHistorial()));
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
