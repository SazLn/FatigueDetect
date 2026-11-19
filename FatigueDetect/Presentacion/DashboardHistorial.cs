using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal sealed class DashboardHistorial : Form
    {
        public DashboardHistorial(ResumenHistorial resumen)
        {
            Text = "Estadísticas del historial";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 690);
            MinimumSize = new Size(760, 520);
            BackColor = Tema.Fondo;
            Padding = new Padding(20);

            var contenido = Tema.TablaFilas(Tema.Fondo, 38, 76, 30, 0);
            contenido.Controls.Add(Tema.Etiqueta("Estadísticas del historial", 19, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            var tarjetas = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            foreach (var dato in new[]
            {
                ("Sesiones", resumen.TotalSesiones.ToString()),
                ("Duración promedio", FormatoTiempo(resumen.PromedioSegundos)),
                ("Alarmas", resumen.TotalAlertas.ToString()),
                ("Intervalo medio entre alarmas", FormatoTiempo(resumen.IntervaloPromedioSegundos))
            })
            {
                tarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var tarjeta = new PanelRedondeado { Dock = DockStyle.Fill };
                var datos = Tema.TablaFilas(Tema.Tarjeta, 22, 0);
                datos.Controls.Add(Tema.Etiqueta(dato.Item1, 9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
                datos.Controls.Add(Tema.Etiqueta(dato.Item2, 17, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 1);
                tarjeta.Controls.Add(datos);
                tarjetas.Controls.Add(tarjeta);
            }
            contenido.Controls.Add(tarjetas, 0, 1);
            contenido.Controls.Add(Tema.Etiqueta($"Alarmas sin hora registrada: {resumen.AlertasSinHora}",
                9, Tema.TextoSuave, Tema.Fondo), 0, 2);

            var pestañas = new TabControl { Dock = DockStyle.Fill };
            AgregarPeriodos(pestañas, "Por día", "Fecha", resumen.PorDia(), true);
            AgregarPeriodos(pestañas, "Por semana", "Lunes de inicio", resumen.PorSemana(), true);
            AgregarPeriodos(pestañas, "Por mes", "Mes", resumen.PorMes(), true);
            AgregarPeriodos(pestañas, "Horas de uso", "Hora de inicio", resumen.PorHora(), false);
            AgregarCategorias(pestañas, "Alarmas por hora", "Hora local", "Alarmas", resumen.AlertasPorHora(), Tema.Ambar);
            AgregarCategorias(pestañas, "Gravedad", "Gravedad", "Eventos", resumen.PorGravedad(), Tema.Rojo);
            contenido.Controls.Add(pestañas, 0, 3);
            Controls.Add(contenido);
        }

        private static string FormatoTiempo(double? segundos) => segundos.HasValue
            ? Tema.FormatoDuracion(TimeSpan.FromSeconds(segundos.Value)) : "—";

        private static void AgregarPeriodos(TabControl pestañas, string nombre, string periodo,
            IEnumerable<ResumenPeriodo> filas, bool cronologico)
        {
            var tabla = CrearTabla(periodo, "Sesiones", "Duración promedio");
            var categorias = new List<ResumenCategoria>();
            foreach (ResumenPeriodo fila in filas)
            {
                tabla.Rows.Add(fila.Periodo, fila.Sesiones, FormatoTiempo(fila.PromedioSegundos));
                categorias.Add(new ResumenCategoria(fila.Periodo, fila.Sesiones));
            }

            var grafico = categorias.Take(cronologico ? 18 : 24);
            Agregar(pestañas, nombre, tabla, (cronologico ? grafico.Reverse() : grafico).ToArray(), Tema.Azul,
                cronologico && categorias.Count > 18 ? "Últimos 18 periodos con sesiones" : "Sesiones");
        }

        private static void AgregarCategorias(TabControl pestañas, string nombre, string categoria, string unidad,
            IEnumerable<ResumenCategoria> filas, Color color)
        {
            var tabla = CrearTabla(categoria, unidad);
            var datos = filas.ToArray();
            foreach (var fila in datos)
                tabla.Rows.Add(fila.Categoria, fila.Cantidad);
            Agregar(pestañas, nombre, tabla, datos, color, unidad);
        }

        private static void Agregar(TabControl pestañas, string nombre, DataGridView tabla,
            IReadOnlyList<ResumenCategoria> datos, Color color, string unidad)
        {
            var pagina = new TabPage(nombre) { BackColor = Tema.Tarjeta, Padding = new Padding(12) };
            var contenido = Tema.TablaFilas(Tema.Tarjeta, 25, 0, 0);
            contenido.RowStyles[1].Height = 58;
            contenido.RowStyles[2].Height = 42;
            contenido.Controls.Add(Tema.Etiqueta(unidad, 10, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            if (datos.Count == 0)
            {
                tabla.Dispose();
                contenido.Controls.Add(Tema.Etiqueta("Sin sesiones para los filtros seleccionados", 11, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            }
            else
            {
                contenido.Controls.Add(new GraficoBarrasHistorial(datos, color), 0, 1);
                contenido.Controls.Add(tabla, 0, 2);
            }
            pagina.Controls.Add(contenido);
            pestañas.TabPages.Add(pagina);
        }

        internal static DataGridView CrearTabla(params string[] columnas)
        {
            var tabla = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, RowHeadersVisible = false, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Tema.Tarjeta,
                BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false, GridColor = Tema.TarjetaClara
            };
            tabla.ColumnHeadersDefaultCellStyle.BackColor = Tema.TarjetaClara;
            tabla.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Texto;
            tabla.DefaultCellStyle.BackColor = Tema.Tarjeta;
            tabla.DefaultCellStyle.ForeColor = Tema.Texto;
            tabla.DefaultCellStyle.SelectionBackColor = Tema.Azul;
            tabla.DefaultCellStyle.SelectionForeColor = Color.White;
            for (int i = 0; i < columnas.Length; i++)
                tabla.Columns.Add($"columna{i}", columnas[i]);
            return tabla;
        }
    }
}
