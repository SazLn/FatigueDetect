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
    /// <summary>Pantalla "Historial de alertas": sesiones guardadas y detalle de la seleccionada.</summary>
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
        private ListBox dHoras = null!;

        public PaginaHistorial(SesionService sesionService)
        {
            this.sesionService = sesionService;

            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(22);

            var raiz = Tema.TablaFilas(Tema.Fondo, 84, 0);

            var encabezado = Tema.TablaFilas(Tema.Fondo, 42, 30);
            encabezado.Controls.Add(Tema.Etiqueta("Historial de alertas", 20, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            Label archivo = Tema.Etiqueta("Archivo: " + sesionService.RutaHistorial, 9.5f, Tema.TextoSuave, Tema.Fondo);
            archivo.AutoEllipsis = true;
            encabezado.Controls.Add(archivo, 0, 1);
            raiz.Controls.Add(encabezado, 0, 0);

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
            var tabla2 = Tema.TablaFilas(Tema.Tarjeta, 36, 44, 44, 44, 44, 44, 44, 44, 44, 26, 0);

            tabla2.Controls.Add(Tema.Etiqueta("Detalle de la sesión", 13, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            tabla2.Controls.Add(Dato("Conductor", out dConductor), 0, 1);
            tabla2.Controls.Add(Dato("Cédula", out dCedula), 0, 2);
            tabla2.Controls.Add(Dato("Vehículo", out dVehiculo), 0, 3);
            tabla2.Controls.Add(Dato("Placa", out dPlaca), 0, 4);
            tabla2.Controls.Add(Dato("Inicio", out dInicio), 0, 5);
            tabla2.Controls.Add(Dato("Fin", out dFin), 0, 6);
            tabla2.Controls.Add(Dato("Duración", out dDuracion), 0, 7);
            tabla2.Controls.Add(Dato("Alarmas que sonaron", out dAlarmas), 0, 8);
            tabla2.Controls.Add(Tema.Etiqueta("Hora de cada alarma", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 9);

            dHoras = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Tema.Tarjeta,
                ForeColor = Tema.Texto,
                BorderStyle = BorderStyle.None,
                Font = Tema.Fuente(10),
                Margin = new Padding(0),
                IntegralHeight = false
            };
            tabla2.Controls.Add(dHoras, 0, 10);

            tarjeta.Controls.Add(tabla2);
            return tarjeta;
        }

        // Título pequeño + valor en negrita.
        private static Control Dato(string titulo, out Label valor)
        {
            var celda = Tema.TablaFilas(Tema.Tarjeta, 18, 24);
            celda.Controls.Add(Tema.Etiqueta(titulo, 8.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            valor = Tema.Etiqueta("—", 11, Tema.Texto, Tema.Tarjeta, FontStyle.Bold);
            celda.Controls.Add(valor, 0, 1);
            return celda;
        }

        /// <summary>Vuelve a leer el archivo. Si se indica una sesión, queda seleccionada.</summary>
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
                    Tema.FormatoDuracion(s.Duracion),
                    s.CantidadAlarmas,
                    s.CantidadAlarmas > 0 ? "Con alertas" : "Sin alertas");

                DataGridViewRow fila = tabla.Rows[indice];
                fila.Tag = s;
                fila.Cells["estado"].Style.ForeColor = s.CantidadAlarmas > 0 ? Tema.Rojo : Tema.Verde;

                if (resaltar != null &&
                    s.Conductor.Cedula == resaltar.Conductor.Cedula &&
                    s.Inicio.ToString("s") == resaltar.Inicio.ToString("s"))
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
            dHoras.Items.Clear();

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
                dHoras.Items.Add("Sin registros");
                return;
            }

            dConductor.Text = s.Conductor.NombreCompleto;
            dCedula.Text = s.Conductor.Cedula;
            dVehiculo.Text = s.Vehiculo.TipoCarro;
            dPlaca.Text = s.Vehiculo.Placa;
            dInicio.Text = s.Inicio.ToString("dd/MM/yyyy HH:mm:ss");
            dFin.Text = s.Fin?.ToString("dd/MM/yyyy HH:mm:ss") ?? "-";
            dDuracion.Text = Tema.FormatoDuracion(s.Duracion);
            dAlarmas.Text = s.CantidadAlarmas.ToString();

            List<DateTime> horas = s.HorasAlarmas;
            if (horas.Count == 0)
            {
                dHoras.Items.Add(s.CantidadAlarmas == 0 ? "Sin alarmas" : "Hora no registrada");
            }
            else
            {
                foreach (DateTime hora in horas)
                {
                    dHoras.Items.Add(hora.ToString("HH:mm:ss"));
                }
            }
        }

        private void Eliminar_Click(object? remitente, EventArgs evento)
        {
            DialogResult respuesta = MessageBox.Show(
                FindForm(),
                "¿Está seguro de que desea borrar el historial?\n\n" +
                "Se eliminarán TODOS los registros y esta acción no se puede deshacer.",
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
                    "No se pudo eliminar el historial (¿está abierto en Excel?):\n" + ex.Message,
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
