using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal sealed class DashboardHistorial : Form
    {
        private sealed record PaginaDashboard(string Nombre, Control Contenido);

        public DashboardHistorial(ResumenHistorial resumen, IReadOnlyList<SesionConduccion> sesiones, FiltroHistorial filtro)
        {
            Text = "Estadísticas del historial";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 740);
            MinimumSize = new Size(760, 560);
            BackColor = Tema.Fondo;
            Padding = new Padding(28, 24, 28, 24);

            var contenido = Tema.TablaFilas(Tema.Fondo, 46, 112, 26, 0);
            contenido.Controls.Add(Tema.Etiqueta("Estadísticas del historial", 22, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);

            var tarjetas = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Tema.Fondo,
                Margin = Padding.Empty
            };
            for (int i = 0; i < 4; i++)
            {
                tarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            }

            tarjetas.Controls.Add(NuevaTarjeta("Sesiones", resumen.TotalSesiones.ToString(), Tema.Lila), 0, 0);
            tarjetas.Controls.Add(NuevaTarjeta("Duración promedio", FormatoTiempo(resumen.PromedioSegundos), Tema.Verde), 1, 0);
            tarjetas.Controls.Add(NuevaTarjeta("Alarmas", resumen.TotalAlertas.ToString(), Tema.Ambar), 2, 0);
            tarjetas.Controls.Add(NuevaTarjeta("Intervalo medio entre alarmas",
                FormatoTiempo(resumen.IntervaloPromedioSegundos), Tema.Rosa), 3, 0);

            contenido.Controls.Add(tarjetas, 0, 1);
            contenido.Controls.Add(Tema.Etiqueta($"Alarmas sin hora registrada: {resumen.AlertasSinHora}",
                9, Tema.TextoSuave, Tema.Fondo), 0, 2);

            var paginas = new List<PaginaDashboard>();
            AgregarAlarmasPorHora(paginas, resumen);
            AgregarCategorias(paginas, "Gravedad", "Gravedad", "Eventos", resumen.PorGravedad(), Tema.Rojo);
            AgregarComparacion(paginas, sesiones, filtro);
            AgregarAvanzado(paginas, filtro.Aplicar(sesiones));
            AgregarPeriodos(paginas, "Por día", "Fecha", resumen.PorDia(), true);
            AgregarPeriodos(paginas, "Por semana", "Lunes de inicio", resumen.PorSemana(), true);
            AgregarPeriodos(paginas, "Por mes", "Mes", resumen.PorMes(), true);
            AgregarPeriodos(paginas, "Horas de uso", "Hora de inicio", resumen.PorHora(), false);

            contenido.Controls.Add(ConstruirPestanas(paginas), 0, 3);
            Controls.Add(contenido);
        }

        private static Control NuevaTarjeta(string titulo, string valor, Color acento)
        {
            return new TarjetaKpi
            {
                Dock = DockStyle.Fill,
                Titulo = titulo,
                Valor = valor,
                Acento = acento
            };
        }

        private static Control ConstruirPestanas(List<PaginaDashboard> paginas)
        {
            var contenedor = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Tema.Fondo,
                Margin = Padding.Empty
            };
            contenedor.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            contenedor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var barra = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Tema.Fondo,
                Margin = Padding.Empty
            };
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            barra.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            barra.MaximumSize = new Size(0, 42);

            var flujo = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.Fondo,
                Location = new Point(4, 2)
            };

            var botones = new List<BotonPestana>();
            var contenido = new PanelRedondeado
            {
                Dock = DockStyle.Fill,
                ColorTarjeta = Tema.Tarjeta,
                ColorBorde = Tema.Borde,
                Radio = 16,
                Padding = new Padding(18)
            };

            void Seleccionar(PaginaDashboard pagina)
            {
                contenido.Controls.Clear();
                pagina.Contenido.Dock = DockStyle.Fill;
                contenido.Controls.Add(pagina.Contenido);
                foreach (BotonPestana boton in botones)
                {
                    boton.Activo = boton.Text == pagina.Nombre;
                }
            }

            foreach (PaginaDashboard pagina in paginas)
            {
                var boton = new BotonPestana(pagina.Nombre) { Width = 150 };
                boton.Click += (_, _) => Seleccionar(pagina);
                flujo.Controls.Add(boton);
                botones.Add(boton);
            }

            var zona = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fondo };
            zona.Controls.Add(flujo);

            void Mover(int delta)
            {
                int minimo = Math.Min(0, zona.Width - flujo.Width);
                flujo.Left = Math.Clamp(flujo.Left + delta, minimo, 0);
            }

            var izquierda = new Button
            {
                Text = "‹",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Tema.TarjetaClara,
                ForeColor = Tema.Texto,
                Font = Tema.Fuente(12),
                TabStop = false,
                MaximumSize = new Size(0, 42)
            };
            izquierda.FlatAppearance.BorderSize = 0;
            izquierda.Click += (_, _) => Mover(220);

            var derecha = new Button
            {
                Text = "›",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Tema.TarjetaClara,
                ForeColor = Tema.Texto,
                Font = Tema.Fuente(12),
                TabStop = false,
                MaximumSize = new Size(0, 42)
            };
            derecha.FlatAppearance.BorderSize = 0;
            derecha.Click += (_, _) => Mover(-220);

            barra.Controls.Add(izquierda, 0, 0);
            barra.Controls.Add(zona, 1, 0);
            barra.Controls.Add(derecha, 2, 0);

            contenedor.Controls.Add(barra, 0, 0);
            contenedor.Controls.Add(contenido, 0, 1);

            if (paginas.Count > 0)
            {
                Seleccionar(paginas[0]);
            }
            return contenedor;
        }

        private static void AgregarAlarmasPorHora(List<PaginaDashboard> paginas, ResumenHistorial resumen)
        {
            var contenido = Tema.TablaFilas(Tema.Tarjeta, 28, 220, 12, 0);
            contenido.Controls.Add(Tema.Etiqueta("Alarmas", 12, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);

            var datos = resumen.AlertasPorHora().ToArray();
            var grafico = new GraficoBarrasHistorial(datos, Tema.Ambar);
            contenido.Controls.Add(grafico, 0, 1);

            var tabla = CrearTabla("Hora local", "Alarmas");
            tabla.RowTemplate.Height = 32;
            tabla.Columns[0].FillWeight = 55;
            tabla.Columns[1].FillWeight = 45;
            tabla.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 41, 92);
            tabla.DefaultCellStyle.SelectionForeColor = Tema.Texto;
            tabla.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            tabla.GridColor = Tema.Borde;
            tabla.ColumnHeadersDefaultCellStyle.BackColor = Tema.TarjetaClara;
            tabla.ColumnHeadersDefaultCellStyle.ForeColor = Tema.TextoSuave;
            tabla.EnableHeadersVisualStyles = false;

            int maximo = Math.Max(1, datos.Max(d => d.Cantidad));
            foreach (ResumenCategoria fila in datos)
            {
                tabla.Rows.Add(fila.Categoria, fila.Cantidad);
            }

            tabla.CellPainting += (remitente, e) =>
            {
                if (e.RowIndex == -1)
                {
                    e.PaintBackground(e.CellBounds, true);
                    using var fondo = new SolidBrush(Tema.TarjetaClara);
                    e.Graphics!.FillRectangle(fondo, e.CellBounds);
                    TextRenderer.DrawText(e.Graphics, tabla.Columns[e.ColumnIndex].HeaderText,
                        Tema.Fuente(9.5f, FontStyle.Bold),
                        new Rectangle(e.CellBounds.Left + 10, e.CellBounds.Top, e.CellBounds.Width - 10,
                            e.CellBounds.Height),
                        Tema.TextoSuave,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                    e.Handled = true;
                    return;
                }

                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                bool seleccionada = tabla.Rows[e.RowIndex]!.Selected;
                if (e.ColumnIndex == 0)
                {
                    e.PaintBackground(e.CellBounds, true);
                    if (seleccionada)
                    {
                        using var acento = new SolidBrush(Tema.Lila);
                        e.Graphics!.FillRectangle(acento, e.CellBounds.Left, e.CellBounds.Top, 3,
                            e.CellBounds.Height);
                    }

                    string texto = tabla.Rows[e.RowIndex]!.Cells[0].Value?.ToString() ?? "";
                    TextRenderer.DrawText(e.Graphics!, texto, tabla.DefaultCellStyle.Font,
                        new Rectangle(e.CellBounds.Left + 12, e.CellBounds.Top, e.CellBounds.Width - 12,
                            e.CellBounds.Height),
                        Tema.Texto,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                    e.Handled = true;
                }
                else if (e.ColumnIndex == 1)
                {
                    e.PaintBackground(e.CellBounds, true);
                    int valor = Convert.ToInt32(tabla.Rows[e.RowIndex]!.Cells[1].Value ?? 0);
                    string texto = valor.ToString();
                    using var pincelTexto = new SolidBrush(valor == 0 ? Tema.TextoSuave : Tema.Texto);
                    using var fuente = Tema.Fuente(9.5f);
                    e.Graphics!.DrawString(texto, fuente, pincelTexto, e.CellBounds.Left + 8,
                        e.CellBounds.Top + 7);

                    if (valor > 0)
                    {
                        int ancho = (int)(80.0 * valor / maximo);
                        using var barra = new SolidBrush(Tema.Ambar);
                        e.Graphics.FillRectangle(barra, e.CellBounds.Left + 46,
                            e.CellBounds.Top + e.CellBounds.Height / 2 - 3, Math.Max(2, ancho), 6);
                    }
                    e.Handled = true;
                }
            };

            tabla.SelectionChanged += (_, _) =>
            {
                if (tabla.SelectedRows.Count > 0)
                {
                    grafico.Seleccionado = tabla.SelectedRows[0].Index;
                }
            };

            grafico.SeleccionCambiada += indice =>
            {
                if (indice < 0 || indice >= tabla.Rows.Count) return;
                tabla.ClearSelection();
                tabla.Rows[indice].Selected = true;
                tabla.FirstDisplayedScrollingRowIndex = indice;
            };

            var contenedorTabla = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Tarjeta };
            tabla.Dock = DockStyle.Fill;
            contenedorTabla.Controls.Add(tabla);
            contenido.Controls.Add(contenedorTabla, 0, 3);

            paginas.Add(new PaginaDashboard("Alarmas por hora", contenido));
        }

        private static void AgregarComparacion(List<PaginaDashboard> paginas, IReadOnlyList<SesionConduccion> sesiones,
            FiltroHistorial filtro)
        {
            var panel = Tema.TablaFilas(Tema.Tarjeta, 36, 44, 0);
            var selector = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 250,
                AccessibleName = "Ventana de comparación"
            };
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
            paginas.Add(new PaginaDashboard("Comparar períodos", panel));
        }

        private static string FormatoTiempo(double? segundos) => segundos.HasValue
            ? Tema.FormatoDuracion(TimeSpan.FromSeconds(segundos.Value)) : "—";

        private static void AgregarAvanzado(List<PaginaDashboard> paginas, IReadOnlyList<SesionConduccion> sesiones)
        {
            var riesgos = sesiones.Select(s => (Sesion: s, Riesgo: AnalisisHistoricoAvanzado.Riesgo(s)))
                .Where(x => x.Riesgo.HasValue).OrderBy(x => x.Riesgo).ToArray();
            var riesgo = CrearTabla("Fecha", "Conductor", "Vehículo", "Riesgo /100", "Atención /100", "Calidad /100");
            foreach (var fila in riesgos)
                riesgo.Rows.Add(fila.Sesion.Inicio.ToString("dd/MM/yyyy HH:mm"), fila.Sesion.Conductor.NombreCompleto,
                    fila.Sesion.Vehiculo.Placa, fila.Riesgo!.Value.ToString("0.0"),
                    (100 - fila.Riesgo.Value).ToString("0.0"), fila.Sesion.Calidad?.Puntaje?.ToString("0.0") ?? "—");
            AgregarTabla(paginas, "Seguras y riesgosas", riesgo,
                "Menor riesgo primero. Índice experimental = 100 − atención; exige calidad ≥50. Las sesiones sin puntaje se excluyen.");

            AgregarFilas(paginas, "Por vehículo", AnalisisHistoricoAvanzado.PorVehiculo(sesiones));
            AgregarFilas(paginas, "Por horario", AnalisisHistoricoAvanzado.PorHora(sesiones));
            AgregarFilas(paginas, "Por día semanal", AnalisisHistoricoAvanzado.PorDiaSemana(sesiones));

            var mapa = CrearTabla("Día", "00–03", "04–07", "08–11", "12–15", "16–19", "20–23");
            int[,] datos = AnalisisHistoricoAvanzado.MapaCalor(sesiones);
            string[] dias = ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];
            int mayorBloque = 0;
            for (int d = 0; d < 7; d++)
                for (int b = 0; b < 6; b++)
                    mayorBloque = Math.Max(mayorBloque, Enumerable.Range(b * 4, 4).Sum(h => datos[d, h]));

            for (int dia = 0; dia < 7; dia++)
            {
                int fila = mapa.Rows.Add(dias[dia]);
                for (int bloque = 0; bloque < 6; bloque++)
                {
                    int cantidad = Enumerable.Range(bloque * 4, 4).Sum(h => datos[dia, h]);
                    var celda = mapa.Rows[fila].Cells[bloque + 1];
                    celda.Value = cantidad;
                    int intensidad = mayorBloque > 0 ? (int)(180.0 * cantidad / mayorBloque) : 0;
                    celda.Style.BackColor = Color.FromArgb(
                        Math.Clamp(235 - intensidad / 3, 0, 255),
                        Math.Clamp(245 - intensidad / 2, 0, 255),
                        Math.Clamp(250 - intensidad, 0, 255));
                    celda.Style.ForeColor = Color.Black;
                }
            }
            AgregarTabla(paginas, "Mapa día y hora", mapa,
                "Sesiones por día de semana y hora local de inicio. Color más intenso = mayor cantidad.");

            var panel = Tema.TablaFilas(Tema.Tarjeta, 40, 28, 0);
            var selector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            string[] conductores = sesiones.Select(s => s.Conductor.Cedula.Trim()).Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToArray();
            selector.Items.AddRange(conductores);
            panel.Controls.Add(selector, 0, 0);
            panel.Controls.Add(Tema.Etiqueta("Orden cronológico; cada fila conserva los valores medidos de una sesión.",
                9, Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            var tendencia = CrearTabla("Sesión", "Atención /100", "PERCLOS %", "Parpadeos/min", "Calidad /100", "Alarmas/h");
            panel.Controls.Add(tendencia, 0, 2);
            selector.SelectedIndexChanged += (_, _) =>
            {
                tendencia.Rows.Clear();
                foreach (var fila in AnalisisHistoricoAvanzado.TendenciaPersonal(sesiones, selector.Text))
                    tendencia.Rows.Add(fila.Nombre, Numero(fila.Indicadores.Atencion), Porcentaje(fila.Indicadores.Perclos),
                        Numero(fila.Indicadores.ParpadeosPorMinuto), Numero(fila.Indicadores.Calidad),
                        Numero(fila.Indicadores.AlarmasPorHora));
            };
            if (selector.Items.Count > 0) selector.SelectedIndex = 0;
            paginas.Add(new PaginaDashboard("Tendencia personal", panel));

            var comparador = Tema.TablaFilas(Tema.Tarjeta, 30, 0, 42, 220);
            comparador.Controls.Add(Tema.Etiqueta("Marca dos o más sesiones; se comparan únicamente las seleccionadas.",
                9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
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
                estado.Text = elegidas.Length >= 2
                    ? $"{elegidas.Length} sesiones seleccionadas"
                    : "Selecciona al menos dos sesiones.";
                if (elegidas.Length < 2) return;
                foreach (var s in elegidas)
                    AgregarComparada(resultado, $"{s.Inicio:dd/MM HH:mm} · {s.Id.ToString()[..8]}",
                        AnalisisHistoricoAvanzado.Calcular([s]));
                AgregarComparada(resultado, "Conjunto seleccionado", AnalisisHistoricoAvanzado.Calcular(elegidas));
            }));
            paginas.Add(new PaginaDashboard("Comparar conductores", comparador));
        }

        private static void AgregarComparada(DataGridView tabla, string nombre, IndicadorHistorico x) =>
            tabla.Rows.Add(nombre, Numero(x.Atencion), Porcentaje(x.Perclos), Numero(x.AlarmasPorHora),
                Numero(x.Calidad), Porcentaje(x.Distraccion));

        private static void AgregarFilas(List<PaginaDashboard> paginas, string nombre, IReadOnlyList<FilaHistorica> filas)
        {
            var tabla = CrearTabla("Grupo", "Sesiones", "Horas activas", "Alarmas/h", "Atención /100",
                "PERCLOS %", "Distracción %");
            foreach (var fila in filas)
                tabla.Rows.Add(fila.Nombre, fila.Indicadores.Sesiones, Numero(fila.Indicadores.HorasActivas),
                    Numero(fila.Indicadores.AlarmasPorHora), Numero(fila.Indicadores.Atencion),
                    Porcentaje(fila.Indicadores.Perclos), Porcentaje(fila.Indicadores.Distraccion));
            AgregarTabla(paginas, nombre, tabla,
                "Agrupado por hora local de inicio cuando corresponde. Alarmas/h usa tiempo activo; — indica datos insuficientes.");
        }

        private static void AgregarTabla(List<PaginaDashboard> paginas, string nombre, DataGridView tabla, string nota)
        {
            var contenido = Tema.TablaFilas(Tema.Tarjeta, 45, 0);
            contenido.Controls.Add(Tema.Etiqueta(nota, 9, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            contenido.Controls.Add(tabla, 0, 1);
            paginas.Add(new PaginaDashboard(nombre, contenido));
        }

        private static string Numero(double? valor) => valor?.ToString("0.0") ?? "—";
        private static string Porcentaje(double? valor) => valor?.ToString("P1") ?? "—";

        private static void AgregarPeriodos(List<PaginaDashboard> paginas, string nombre, string periodo,
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
            Agregar(paginas, nombre, tabla, (cronologico ? grafico.Reverse() : grafico).ToArray(), Tema.Azul,
                cronologico && categorias.Count > 18 ? "Últimos 18 periodos con sesiones" : "Sesiones");
        }

        private static void AgregarCategorias(List<PaginaDashboard> paginas, string nombre, string categoria, string unidad,
            IEnumerable<ResumenCategoria> filas, Color color)
        {
            var tabla = CrearTabla(categoria, unidad);
            var datos = filas.ToArray();
            foreach (var fila in datos)
                tabla.Rows.Add(fila.Categoria, fila.Cantidad);
            Agregar(paginas, nombre, tabla, datos, color, unidad);
        }

        private static void Agregar(List<PaginaDashboard> paginas, string nombre, DataGridView tabla,
            IReadOnlyList<ResumenCategoria> datos, Color color, string unidad)
        {
            var contenido = Tema.TablaFilas(Tema.Tarjeta, 25, 0, 0);
            contenido.RowStyles[1].Height = 58;
            contenido.RowStyles[2].Height = 42;
            contenido.Controls.Add(Tema.Etiqueta(unidad, 10, Tema.Texto, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            if (datos.Count == 0)
            {
                tabla.Dispose();
                contenido.Controls.Add(Tema.Etiqueta("Sin sesiones para los filtros seleccionados", 11,
                    Tema.TextoSuave, Tema.Tarjeta), 0, 1);
            }
            else
            {
                contenido.Controls.Add(new GraficoBarrasHistorial(datos, color), 0, 1);
                contenido.Controls.Add(tabla, 0, 2);
            }
            paginas.Add(new PaginaDashboard(nombre, contenido));
        }

        internal static DataGridView CrearTabla(params string[] columnas)
        {
            var tabla = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Tema.Tarjeta,
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                GridColor = Tema.TarjetaClara
            };
            tabla.ColumnHeadersDefaultCellStyle.BackColor = Tema.TarjetaClara;
            tabla.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Texto;
            tabla.DefaultCellStyle.BackColor = Tema.Tarjeta;
            tabla.DefaultCellStyle.ForeColor = Tema.Texto;
            tabla.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 41, 92);
            tabla.DefaultCellStyle.SelectionForeColor = Tema.Texto;
            for (int i = 0; i < columnas.Length; i++)
                tabla.Columns.Add($"columna{i}", columnas[i]);
            return tabla;
        }
    }
}