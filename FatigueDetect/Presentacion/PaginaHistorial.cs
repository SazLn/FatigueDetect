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
        private List<SesionConduccion> sesiones = new();
        private readonly ComboBox filtroAlarmas = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox filtroCalidad = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private List<SesionConduccion> visibles = new();
        private readonly DateTimePicker desde = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
        private readonly DateTimePicker hasta = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
        private readonly TextBox conductor = new() { PlaceholderText = "Nombre o cédula" };
        private readonly TextBox vehiculo = new() { PlaceholderText = "Placa o tipo" };
        private Button lineaTemporal = null!;
        private Button informeAtencion = null!;
        private Label estadoFiltros = null!;
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

            var raiz = Tema.TablaFilas(Tema.Fondo, 84, 70, 0);

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
            raiz.Controls.Add(ConstruirFiltros(), 0, 1);
            raiz.Controls.Add(cuerpo, 0, 2);

            Controls.Add(raiz);
            MostrarDetalle(null);
        }

        private Control ConstruirFiltros()
        {
            var filtros = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 2 };
            filtros.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            filtros.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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

            Button limpiar = Tema.Boton("Limpiar filtros", Tema.TarjetaClara, Tema.Texto);
            limpiar.Dock = DockStyle.Fill;
            limpiar.Click += (_, _) =>
            {
                desde.Checked = hasta.Checked = false;
                conductor.Clear();
                vehiculo.Clear();
                filtroAlarmas.SelectedIndex = filtroCalidad.SelectedIndex = 0;
                AplicarFiltros();
            };
            filtros.Controls.Add(limpiar, 6, 0);
            estadoFiltros = Tema.Etiqueta("", 9, Tema.TextoSuave, Tema.Fondo);
            filtros.Controls.Add(estadoFiltros, 0, 1);
            filtros.SetColumnSpan(estadoFiltros, 7);

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
            var campo = Tema.TablaFilas(Tema.Fondo, 18, 0);
            campo.Margin = new Padding(0, 0, 10, 0);
            campo.Controls.Add(Tema.Etiqueta(etiqueta, 9, Tema.TextoSuave, Tema.Fondo), 0, 0);
            control.Dock = DockStyle.Fill;
            control.AccessibleName = etiqueta;
            campo.Controls.Add(control, 0, 1);
            filtros.Controls.Add(campo, columna, 0);
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
            var tabla2 = Tema.TablaFilas(Tema.Tarjeta, 36, 39, 39, 39, 39, 39, 39, 39, 39, 250, 26, 0, 26, 0, 88);

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

            lineaTemporal = Tema.Boton("Ver línea temporal", Tema.TarjetaClara, Tema.Texto);
            lineaTemporal.Dock = DockStyle.Fill;
            lineaTemporal.Click += (_, _) =>
            {
                if (tabla.CurrentRow?.Tag is SesionConduccion sesion)
                {
                    using var vista = new VentanaLineaTemporal(sesion);
                    vista.ShowDialog(this);
                }
            };
            var acciones = Tema.TablaFilas(Tema.Tarjeta, 42, 42);
            informeAtencion = Tema.Boton("Atención y resumen", Tema.TarjetaClara, Tema.Texto);
            informeAtencion.Dock = DockStyle.Fill;
            informeAtencion.Click += (_, _) =>
            {
                if (tabla.CurrentRow?.Tag is SesionConduccion s)
                { using var ventana = new VentanaInformeAtencion(s); ventana.ShowDialog(this); }
            };
            acciones.Controls.Add(lineaTemporal, 0, 0);
            acciones.Controls.Add(informeAtencion, 0, 1);
            tabla2.Controls.Add(acciones, 0, 14);
            var desplazamiento = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Tarjeta };
            tabla2.Dock = DockStyle.Top;
            desplazamiento.SizeChanged += (_, _) => tabla2.Height = Math.Max(760, desplazamiento.ClientSize.Height);
            tabla2.Height = 710;
            desplazamiento.Controls.Add(tabla2);
            tarjeta.Controls.Add(desplazamiento);
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
            lineaTemporal.Enabled = s != null;
            informeAtencion.Enabled = s != null;
            dResumen.Items.Clear();
            dEventos.Items.Clear();
            dCalidad.Text = s?.Calidad is CalidadSesion calidad
                ? $"Calidad: {calidad.Puntaje?.ToString("0.0") ?? "—"}/100 · FPS medios: {calidad.Fps?.ToString("0.0") ?? "—"}\n" +
                  $"Analizables: {calidad.ProporcionAnalizable?.ToString("P1") ?? "—"} · Cobertura facial: {calidad.CoberturaRostro?.ToString("P0") ?? "—"}\n" +
                  $"Caídas de FPS: {calidad.CaidasFps?.ToString() ?? "—"} · Fallos de cámara: {calidad.FallosCamara}\n" +
                  $"Ojos abiertos observados: {s.OjosAbiertosObservados?.ToString("P1") ?? "—"}" +
                  $" · Cobertura ocular: {s.CoberturaOcular?.ToString("P0") ?? "—"}"
                : "Calidad: sin medición";
            dCalidad.Text += s?.MetricasFatiga is MetricasFatiga fatiga
                ? FormatearMetricasOculares(fatiga)
                : "\nMétricas faciales: sin medición";

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
            if (s.Postura is { } postura)
                foreach (string linea in MainForm.FormatearPostura(s, postura).Split('\n'))
                    dResumen.Items.Add(linea);

            if (dResumen.Items.Count == 0)
                dResumen.Items.Add("Sin eventos registrados");

            if (eventos.Count == 0 && s.CantidadAlarmas == 0)
                dEventos.Items.Add("Sin eventos registrados");

            foreach (EventoSesion evento in eventos.OrderBy(e => e.Inicio))
            {
                string inicio = evento.Inicio.LocalDateTime.ToString("dd/MM HH:mm:ss.fff");
                string duracion = evento.DuracionMilisegundos is long ms
                    ? $" · {ms / 1000.0:0.000} s" : "";
                string confianza = evento.Confiabilidad?.Puntaje is double puntaje
                    ? $" · confianza {puntaje:0}/100 ({evento.Confiabilidad.Nivel.ToLowerInvariant()})"
                    : " · confianza sin medición";
                dEventos.Items.Add($"{inicio} · {NombreEvento(evento)}{duracion}{confianza}");
            }

            if (alarmasSinHora > 0)
                dEventos.Items.Add($"Alarmas sin hora registrada · {alarmasSinHora}");
        }

        private static string FormatearMetricasOculares(MetricasFatiga fatiga)
        {
            string texto = $"\nParpadeos: {fatiga.Parpadeos} · Microsueños: {fatiga.Microsuenos} · Cabeceos: {fatiga.Cabeceos}" +
                $"\nPERCLOS de sesión: {fatiga.Perclos?.ToString("P1") ?? "—"} · Tiempo cerrado: {fatiga.TiempoOcularCerradoMs / 1000.0:0.0} s" +
                $" · Tiempo ocular válido: {fatiga.TiempoOcularValidoMs / 1000.0:0.0} s";
            if (fatiga.VersionMetricasOculares < 1)
                return texto + "\nDetalle ocular avanzado: sin medición";

            texto += $"\nFrecuencia: {fatiga.ParpadeosPorMinuto?.ToString("0.0") ?? "—"}/min" +
                $" · Cortos: {fatiga.ParpadeosCortos} · Prolongados: {fatiga.ParpadeosProlongados}" +
                $" · Cierres anómalos: {fatiga.CierresAnomalos}" +
                $"\nDuración parpadeo mín./prom./máx.: {FormatoMs(fatiga.DuracionParpadeoMinMs)} /" +
                $" {FormatoMs(fatiga.DuracionParpadeoPromedioMs)} / {FormatoMs(fatiga.DuracionParpadeoMaxMs)}" +
                $"\nCierre más largo: {fatiga.CierreMasLargoMs / 1000.0:0.000} s" +
                $" · Microsueño más largo: {fatiga.MicrosuenoMasLargoMs / 1000.0:0.000} s" +
                $"\nDiferencia ocular promedio: {fatiga.DiferenciaAperturaPromedio?.ToString("P1") ?? "—"}";
            if (fatiga.EvolucionPerclos.Count >= 2 && fatiga.CambioPerclos is double cambio)
                texto += $" · PERCLOS {fatiga.EvolucionPerclos[0].Valor:P1} → {fatiga.EvolucionPerclos[^1].Valor:P1}" +
                    $" ({cambio * 100:+0.0;-0.0;0.0} pp)";
            return texto;
        }

        private static string FormatoMs(double? milisegundos) =>
            milisegundos is double valor ? $"{valor:0} ms" : "—";

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
