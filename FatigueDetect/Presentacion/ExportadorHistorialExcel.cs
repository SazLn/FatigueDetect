using ClosedXML.Excel;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal static class ExportadorHistorialExcel
    {
        public static void Guardar(string ruta, IReadOnlyList<SesionConduccion> sesiones)
        {
            using var libro = new XLWorkbook();
            var resumen = new ResumenHistorial(sesiones);
            var general = CrearHoja(libro, "Resumen", "Sesiones", "Duración promedio (s)",
                "Alarmas", "Alarmas sin hora", "Intervalo medio entre alarmas (s)");
            Escribir(general, 2, resumen.TotalSesiones, resumen.PromedioSegundos,
                resumen.TotalAlertas, resumen.AlertasSinHora, resumen.IntervaloPromedioSegundos);
            FormatoDecimal(general, 2, 5);

            var detalle = CrearHoja(libro, "Sesiones", "ID de sesión", "Conductor", "Cédula",
                "Vehículo", "Placa", "Inicio local", "Fin local", "Duración (s)", "Alarmas",
                "FPS", "Fotogramas analizables", "Rostro válido (s)", "Fallos de cámara",
                "Parpadeos", "Microsueños", "Cabeceos", "PERCLOS de sesión", "Tiempo ocular válido (s)",
                "Parpadeos por minuto", "Parpadeos cortos", "Parpadeos prolongados", "Cierres anómalos",
                "Duración promedio de parpadeo (ms)", "Duración mínima de parpadeo (ms)",
                "Duración máxima de parpadeo (ms)", "Microsueño más largo (s)", "Cierre más largo (s)",
                "Tiempo con ojos cerrados (s)", "Diferencia ocular promedio");
            detalle.Columns(6, 7).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            FormatoDecimal(detalle, 8, 10, 12, 18, 19, 23, 24, 25, 26, 27, 28, 29);
            detalle.Column(11).Style.NumberFormat.Format = "0.00%";
            detalle.Column(17).Style.NumberFormat.Format = "0.00%";
            detalle.Column(29).Style.NumberFormat.Format = "0.00%";

            var eventos = CrearHoja(libro, "Eventos", "ID de sesión", "Tipo", "Inicio local",
                "Fin local", "Duración (s)", "Gravedad", "Desde alarma anterior (s)");
            eventos.Columns(3, 4).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss.000";
            FormatoDecimal(eventos, 5, 7);

            var tipos = CrearHoja(libro, "Resumen de eventos", "ID de sesión", "Tipo", "Cantidad",
                "Duración total (s)", "Duración máxima (s)", "Sin duración", "Gravedad");
            FormatoDecimal(tipos, 4, 5);
            var evolucionPerclos = CrearHoja(libro, "Evolución PERCLOS", "ID de sesión", "Conductor",
                "Instante local", "PERCLOS 60 s");
            evolucionPerclos.Column(3).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            evolucionPerclos.Column(4).Style.NumberFormat.Format = "0.00%";
            int filaSesion = 2, filaEvento = 2, filaTipo = 2, filaPerclos = 2;

            foreach (SesionConduccion sesion in sesiones)
            {
                string id = sesion.Id.ToString();
                CalidadSesion? calidad = sesion.Calidad;
                MetricasFatiga? fatiga = sesion.MetricasFatiga;
                MetricasFatiga? detalleOcular = fatiga is { VersionMetricasOculares: >= 1 } ? fatiga : null;
                Escribir(detalle, filaSesion++, id, sesion.Conductor.NombreCompleto,
                    sesion.Conductor.Cedula, sesion.Vehiculo.TipoCarro, sesion.Vehiculo.Placa,
                    sesion.Inicio, sesion.Fin, sesion.Fin.HasValue ? sesion.Duracion.TotalSeconds : null,
                    sesion.CantidadAlarmas, calidad?.Fps, calidad?.ProporcionAnalizable,
                    calidad?.TiempoRostroValido.TotalSeconds, calidad?.FallosCamara,
                    fatiga?.Parpadeos, fatiga?.Microsuenos, fatiga?.Cabeceos, fatiga?.Perclos,
                    fatiga?.TiempoOcularValidoMs / 1000.0, fatiga?.ParpadeosPorMinuto,
                    detalleOcular?.ParpadeosCortos, detalleOcular?.ParpadeosProlongados,
                    detalleOcular?.CierresAnomalos, detalleOcular?.DuracionParpadeoPromedioMs,
                    detalleOcular?.DuracionParpadeoMinMs, detalleOcular?.DuracionParpadeoMaxMs,
                    detalleOcular?.MicrosuenoMasLargoMs / 1000.0, detalleOcular?.CierreMasLargoMs / 1000.0,
                    fatiga?.TiempoOcularCerradoMs / 1000.0, detalleOcular?.DiferenciaAperturaPromedio);

                if (fatiga is not null)
                {
                    foreach (MuestraPerclos muestra in fatiga.EvolucionPerclos)
                        Escribir(evolucionPerclos, filaPerclos++, id, sesion.Conductor.NombreCompleto,
                            muestra.Instante.LocalDateTime, muestra.Valor);
                }

                List<EventoSesion> registrados = sesion.Eventos;
                var intervalos = ResumenHistorial.IntervalosEntreAlertas(sesion, registrados)
                    .ToDictionary<IntervaloAlerta, EventoSesion, double>(i => i.Alarma, i => i.Segundos, ReferenceEqualityComparer.Instance);
                foreach (EventoSesion evento in registrados)
                    Escribir(eventos, filaEvento++, id, PaginaHistorial.NombreEvento(evento.Tipo),
                        evento.Inicio.LocalDateTime, evento.Fin?.LocalDateTime, evento.DuracionMilisegundos / 1000.0,
                        ResumenHistorial.Gravedad(evento.Tipo).ToString(),
                        intervalos.TryGetValue(evento, out double intervalo) ? intervalo : null);

                foreach (ResumenEvento tipo in ResumenHistorial.PorTipo(sesion, registrados))
                    Escribir(tipos, filaTipo++, id, PaginaHistorial.NombreEvento(tipo.Tipo), tipo.Cantidad,
                        tipo.TotalMs / 1000.0, tipo.MaximoMs / 1000.0, tipo.SinDuracion,
                        ResumenHistorial.Gravedad(tipo.Tipo).ToString());
            }

            var graficos = new List<GraficoExcel>
            {
                AgregarPeriodos(libro, "Por día", "Fecha", resumen.PorDia()),
                AgregarPeriodos(libro, "Por semana", "Lunes de inicio", resumen.PorSemana()),
                AgregarPeriodos(libro, "Por mes", "Mes", resumen.PorMes()),
                AgregarPeriodos(libro, "Por hora", "Hora de inicio", resumen.PorHora()),
                AgregarCategorias(libro, "Alarmas por hora", "Hora local", "Alarmas", resumen.AlertasPorHora()),
                AgregarCategorias(libro, "Eventos por gravedad", "Gravedad", "Eventos", resumen.PorGravedad())
            };

            foreach (IXLWorksheet hoja in libro.Worksheets)
            {
                IXLRange rango = hoja.RangeUsed()!;
                int columnas = rango.ColumnCount();
                for (int columna = 1; columna <= columnas; columna++)
                    hoja.Column(columna).AdjustToContents(1, Math.Min(200, rango.RowCount()), 13, 40);

                hoja.SheetView.FreezeRows(1);
                rango.SetAutoFilter();
            }

            string temporal = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ruta))!,
                $".fatiguedetect-{Guid.NewGuid():N}.xlsx");
            try
            {
                libro.SaveAs(temporal);
                GraficoExcel.Guardar(temporal, graficos);
                File.Move(temporal, ruta, overwrite: true);
            }
            finally
            {
                File.Delete(temporal);
            }
        }

        private static GraficoExcel AgregarPeriodos(XLWorkbook libro, string nombre, string periodo,
            IEnumerable<ResumenPeriodo> periodos)
        {
            var hoja = CrearHoja(libro, nombre, periodo, "Sesiones", "Duración promedio (s)");
            FormatoDecimal(hoja, 3);
            int fila = 2;
            var datos = new List<ResumenCategoria>();
            foreach (ResumenPeriodo resumen in periodos)
            {
                Escribir(hoja, fila++, resumen.Periodo, resumen.Sesiones, resumen.PromedioSegundos);
                datos.Add(new ResumenCategoria(resumen.Periodo, resumen.Sesiones));
            }
            return new GraficoExcel(nombre, "Sesiones", datos.Take(24).ToArray());
        }

        private static GraficoExcel AgregarCategorias(XLWorkbook libro, string nombre, string categoria,
            string unidad, IEnumerable<ResumenCategoria> categorias)
        {
            var hoja = CrearHoja(libro, nombre, categoria, unidad);
            var datos = categorias.ToArray();
            for (int i = 0; i < datos.Length; i++)
                Escribir(hoja, i + 2, datos[i].Categoria, datos[i].Cantidad);
            return new GraficoExcel(nombre, unidad, datos);
        }

        private static IXLWorksheet CrearHoja(XLWorkbook libro, string nombre, params string[] titulos)
        {
            var hoja = libro.Worksheets.Add(nombre);
            Escribir(hoja, 1, titulos);
            var encabezado = hoja.Range(1, 1, 1, titulos.Length);
            encabezado.Style.Font.Bold = true;
            encabezado.Style.Font.FontColor = XLColor.White;
            encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#244863");
            return hoja;
        }

        private static void FormatoDecimal(IXLWorksheet hoja, params int[] columnas)
        {
            foreach (int columna in columnas)
                hoja.Column(columna).Style.NumberFormat.Format = "0.000";
        }

        private static void Escribir(IXLWorksheet hoja, int fila, params object?[] valores)
        {
            for (int columna = 0; columna < valores.Length; columna++)
            {
                if (valores[columna] is object valor)
                    hoja.Cell(fila, columna + 1).Value = XLCellValue.FromObject(valor);
            }
        }
    }
}
