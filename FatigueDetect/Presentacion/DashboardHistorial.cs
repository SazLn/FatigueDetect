using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal sealed class DashboardHistorial : Form
    {
        public DashboardHistorial(ResumenHistorial resumen, IReadOnlyList<SesionConduccion> sesiones, FiltroHistorial filtro)
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
            AgregarComparacion(pestañas, sesiones, filtro);
            AgregarAvanzado(pestañas, filtro.Aplicar(sesiones));
            contenido.Controls.Add(pestañas, 0, 3);
            Controls.Add(contenido);
        }

        private static void AgregarComparacion(TabControl pestañas, IReadOnlyList<SesionConduccion> sesiones,
            FiltroHistorial filtro)
        {
            var pagina = new TabPage("Comparar períodos") { BackColor = Tema.Tarjeta, Padding = new Padding(12) };
            var panel = Tema.TablaFilas(Tema.Tarjeta, 36, 44, 0);
            var selector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250,
                AccessibleName = "Ventana de comparación" };
            bool tieneRango = filtro.Desde.HasValue && (!filtro.Hasta.HasValue || filtro.Desde <= filtro.Hasta);
            if (tieneRango)
                selector.Items.Add("Rango de fechas seleccionado");
            selector.Items.AddRange(["Últimos 7 días", "Últimos 30 días", "Últimos 90 días"]);
            panel.Controls.Add(selector, 0, 0);
            var contexto = Tema.Etiqueta("", 9.5f, Tema.TextoSuave, Tema.Tarjeta);
            panel.Controls.Add(contexto, 0, 1);
            var tabla = CrearTabla("Indicador", "Período actual", "Período anterior", "Diferencia");
            tabla.Columns[0].FillWeight = 160;
            panel.Controls.Add(tabla, 0, 2);

            selector.SelectedIndexChanged += (_, _) =>
            {
                int opcion = selector.SelectedIndex - (tieneRango ? 1 : 0);
                int dias = opcion switch { 0 => 7, 2 => 90, _ => 30 };
                var comparacion = new ComparacionHistorial(sesiones, filtro, DateTime.Today, dias, opcion < 0);
                string actual = ComparacionHistorial.Rango(comparacion.Desde, comparacion.Hasta);
                string anterior = ComparacionHistorial.Rango(comparacion.AnteriorDesde, comparacion.AnteriorHasta);
                contexto.Text = $"{actual} frente a {anterior}\n" +
                    "Se aplican los filtros de conductor, vehículo, alarmas y calidad a ambos períodos.";
                tabla.Rows.Clear();
                foreach (FilaComparacion fila in comparacion.Filas)
                {
                    string diferencia = fila.Diferencia is double valor
                        ? valor.ToString(fila.Formato == "0" ? "+0;-0;0" : "+0.0;-0.0;0.0") : "—";
                    if (fila.Diferencia.HasValue && fila.Indicador.EndsWith("(%)", StringComparison.Ordinal))
                        diferencia += " pp";
                    tabla.Rows.Add(fila.Indicador, fila.Actual?.ToString(fila.Formato) ?? "—",
                        fila.Anterior?.ToString(fila.Formato) ?? "—", diferencia);
                }
            };

            selector.SelectedIndex = tieneRango ? 0 : 1;
            pagina.Controls.Add(panel);
            pestañas.TabPages.Add(pagina);
        }

        private static string FormatoTiempo(double? segundos) => segundos.HasValue
            ? Tema.FormatoDuracion(TimeSpan.FromSeconds(segundos.Value)) : "—";

        private static void AgregarAvanzado(TabControl pestañas, IReadOnlyList<SesionConduccion> sesiones)
        {
            var riesgos = sesiones.Select(s => (Sesion: s, Riesgo: AnalisisHistoricoAvanzado.Riesgo(s)))
                .Where(x => x.Riesgo.HasValue).OrderBy(x => x.Riesgo).ToArray();
            var riesgo = CrearTabla("Fecha", "Conductor", "Vehículo", "Riesgo /100", "Atención /100", "Calidad /100");
            foreach (var fila in riesgos)
                riesgo.Rows.Add(fila.Sesion.Inicio.ToString("dd/MM/yyyy HH:mm"), fila.Sesion.Conductor.NombreCompleto,
                    fila.Sesion.Vehiculo.Placa, fila.Riesgo!.Value.ToString("0.0"),
                    (100 - fila.Riesgo.Value).ToString("0.0"), fila.Sesion.Calidad?.Puntaje?.ToString("0.0") ?? "—");
            AgregarTabla(pestañas, "Seguras y riesgosas", riesgo,
                "Menor riesgo primero. Índice experimental = 100 − atención; exige calidad ≥50. Las sesiones sin puntaje se excluyen.");

            AgregarFilas(pestañas, "Por vehículo", AnalisisHistoricoAvanzado.PorVehiculo(sesiones));
            AgregarFilas(pestañas, "Por horario", AnalisisHistoricoAvanzado.PorHora(sesiones));
            AgregarFilas(pestañas, "Por día semanal", AnalisisHistoricoAvanzado.PorDiaSemana(sesiones));

            var mapa = CrearTabla("Día", "00–03", "04–07", "08–11", "12–15", "16–19", "20–23");
            int[,] datos = AnalisisHistoricoAvanzado.MapaCalor(sesiones);
            string[] dias = ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];
            int mayor = datos.Cast<int>().DefaultIfEmpty(0).Max();
            for (int dia = 0; dia < 7; dia++)
            {
                int fila = mapa.Rows.Add(dias[dia]);
                for (int bloque = 0; bloque < 6; bloque++)
                {
                    int cantidad = Enumerable.Range(bloque * 4, 4).Sum(h => datos[dia, h]);
                    var celda = mapa.Rows[fila].Cells[bloque + 1];
                    celda.Value = cantidad;
                    int intensidad = mayor > 0 ? (int)(180.0 * cantidad / mayor) : 0;
                    celda.Style.BackColor = Color.FromArgb(235 - intensidad / 3, 245 - intensidad / 2, 250 - intensidad);
                    celda.Style.ForeColor = Color.Black;
                }
            }
            AgregarTabla(pestañas, "Mapa día y hora", mapa,
                "Sesiones por día de semana y hora local de inicio. Color más intenso = mayor cantidad.");

            var personal = new TabPage("Tendencia personal") { BackColor = Tema.Tarjeta, Padding = new Padding(12) };
            var panel = Tema.TablaFilas(Tema.Tarjeta, 40, 28, 0);
            var selector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            string[] conductores = sesiones.Select(s => s.Conductor.Cedula.Trim()).Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToArray();
            selector.Items.AddRange(conductores);
            panel.Controls.Add(selector, 0, 0);
            panel.Controls.Add(Tema.Etiqueta("Orden cronológico; cada fila conserva los valores medidos de una sesión.", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            var tendencia = CrearTabla("Sesión", "Atención /100", "PERCLOS %", "Parpadeos/min", "Calidad /100", "Alarmas/h");
            panel.Controls.Add(tendencia, 0, 2);
            selector.SelectedIndexChanged += (_, _) =>
            {
                tendencia.Rows.Clear();
                foreach (var fila in AnalisisHistoricoAvanzado.TendenciaPersonal(sesiones, selector.Text))
                    tendencia.Rows.Add(fila.Nombre, Numero(fila.Indicadores.Atencion), Porcentaje(fila.Indicadores.Perclos),
                        Numero(fila.Indicadores.ParpadeosPorMinuto), Numero(fila.Indicadores.Calidad), Numero(fila.Indicadores.AlarmasPorHora));
            };
            if (selector.Items.Count > 0) selector.SelectedIndex = 0;
            personal.Controls.Add(panel);
            pestañas.TabPages.Add(personal);

            var comparacion = new TabPage("Comparar sesiones") { BackColor = Tema.Tarjeta, Padding = new Padding(12) };
            var comparador = Tema.TablaFilas(Tema.Tarjeta, 30, 0, 42, 220);
            comparador.Controls.Add(Tema.Etiqueta("Marca dos o más sesiones; se comparan únicamente las seleccionadas.", 9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            var opciones = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
            var ordenadas = sesiones.OrderByDescending(s => s.Inicio).ToArray();
            foreach (var s in ordenadas)
                opciones.Items.Add($"{s.Inicio:dd/MM/yyyy HH:mm} · {s.Conductor.NombreCompleto} · {s.Vehiculo.Placa} · {s.Id.ToString()[..8]}");
            comparador.Controls.Add(opciones, 0, 1);
            var estado = Tema.Etiqueta("Selecciona al menos dos sesiones.", 9, Tema.TextoSuave, Tema.Tarjeta);
            comparador.Controls.Add(estado, 0, 2);
            var resultado = CrearTabla("Sesión", "Atención /100", "PERCLOS %", "Alarmas/h", "Calidad /100", "Distracción %");
            comparador.Controls.Add(resultado, 0, 3);
            opciones.ItemCheck += (_, _) => opciones.BeginInvoke(new Action(() =>
            {
                resultado.Rows.Clear();
                var elegidas = opciones.CheckedIndices.Cast<int>().Select(i => ordenadas[i]).ToArray();
                estado.Text = elegidas.Length >= 2 ? $"{elegidas.Length} sesiones seleccionadas" : "Selecciona al menos dos sesiones.";
                if (elegidas.Length < 2) return;
                foreach (var s in elegidas)
                    AgregarComparada(resultado, $"{s.Inicio:dd/MM HH:mm} · {s.Id.ToString()[..8]}", AnalisisHistoricoAvanzado.Calcular([s]));
                AgregarComparada(resultado, "Conjunto seleccionado", AnalisisHistoricoAvanzado.Calcular(elegidas));
            }));
            comparacion.Controls.Add(comparador);
            pestañas.TabPages.Add(comparacion);
        }

        private static void AgregarComparada(DataGridView tabla, string nombre, IndicadorHistorico x) =>
            tabla.Rows.Add(nombre, Numero(x.Atencion), Porcentaje(x.Perclos), Numero(x.AlarmasPorHora),
                Numero(x.Calidad), Porcentaje(x.Distraccion));

        private static void AgregarFilas(TabControl pestañas, string nombre, IReadOnlyList<FilaHistorica> filas)
        {
            var tabla = CrearTabla("Grupo", "Sesiones", "Horas activas", "Alarmas/h", "Atención /100",
                "PERCLOS %", "Distracción %");
            foreach (var fila in filas)
                tabla.Rows.Add(fila.Nombre, fila.Indicadores.Sesiones, Numero(fila.Indicadores.HorasActivas),
                    Numero(fila.Indicadores.AlarmasPorHora), Numero(fila.Indicadores.Atencion),
                    Porcentaje(fila.Indicadores.Perclos), Porcentaje(fila.Indicadores.Distraccion));
            AgregarTabla(pestañas, nombre, tabla,
                "Agrupado por hora local de inicio cuando corresponde. Alarmas/h usa tiempo activo; — indica datos insuficientes.");
        }

        private static void AgregarTabla(TabControl pestañas, string nombre, DataGridView tabla, string nota)
        {
            var pagina = new TabPage(nombre) { BackColor = Tema.Tarjeta, Padding = new Padding(12) };
            var contenido = Tema.TablaFilas(Tema.Tarjeta, 45, 0);
            contenido.Controls.Add(Tema.Etiqueta(nota, 9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            contenido.Controls.Add(tabla, 0, 1);
            pagina.Controls.Add(contenido);
            pestañas.TabPages.Add(pagina);
        }

        private static string Numero(double? valor) => valor?.ToString("0.0") ?? "—";
        private static string Porcentaje(double? valor) => valor?.ToString("P1") ?? "—";

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
