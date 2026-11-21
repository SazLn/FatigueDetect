using ClosedXML.Excel;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal static class ExportadorHistorialExcel
    {
        public static void Guardar(string ruta, IReadOnlyList<SesionConduccion> sesiones, string? alcance = null)
        {
            using var libro = new XLWorkbook();
            var resumen = new ResumenHistorial(sesiones);
            var general = CrearHoja(libro, "Resumen", "Sesiones", "Duración promedio (s)",
                "Alarmas", "Alarmas sin hora", "Intervalo medio entre alarmas (s)");
            AgregarNota(general, 5, 1, 5, "Alcance de la exportación",
                alcance ?? "Sesiones proporcionadas por el historial.");
            AgregarNota(general, 8, 1, 5, "Conclusión",
                sesiones.Count == 0 ? "No hay sesiones en el conjunto exportado."
                : $"Se registraron {resumen.TotalAlertas} alarmas en {sesiones.Count} sesiones. " +
                  $"{resumen.AlertasSinHora} alarmas carecen de hora y se excluyen del gráfico horario.");
            AgregarNota(general, 11, 1, 5, "Cómo leer los indicadores",
                "Calidad (0–100) combina fotogramas analizables (45 %), cobertura facial (35 %) y estabilidad de FPS (20 %). " +
                "La estabilidad usa ventanas de al menos 2 s y un umbral de 12 FPS. " +
                "Ojos abiertos observados equivale a 1 − PERCLOS. La cobertura indica cuánto tiempo pudo evaluarse.");
            AgregarNota(general, 14, 1, 5, "Datos disponibles",
                $"Calidad calculable en {sesiones.Count(s => s.Calidad?.Puntaje is not null)} sesiones. " +
                $"Indicador ocular disponible en {sesiones.Count(s => s.OjosAbiertosObservados is not null)} sesiones. " +
                "Las celdas vacías indican que falta una medición.");
            Escribir(general, 2, resumen.TotalSesiones, resumen.PromedioSegundos,
                resumen.TotalAlertas, resumen.AlertasSinHora, resumen.IntervaloPromedioSegundos);
            FormatoDecimal(general, 2, 5);

            var detalle = CrearHoja(libro, "Sesiones", "ID de sesión", "Conductor", "Cédula",
                "Vehículo", "Placa", "Inicio local", "Fin local", "Duración (s)", "Alarmas",
                "FPS medios", "Fotogramas analizables (%)", "Rostro válido (s)", "Fallos de cámara",
                "Parpadeos", "Microsueños", "Cabeceos", "PERCLOS de sesión (%)", "Tiempo ocular válido (s)",
                "Parpadeos por minuto", "Parpadeos cortos", "Parpadeos prolongados", "Cierres anómalos",
                "Duración promedio de parpadeo (ms)", "Duración mínima de parpadeo (ms)",
                "Duración máxima de parpadeo (ms)", "Microsueño más largo (s)", "Cierre más largo (s)",
                                "Tiempo con ojos cerrados (s)", "Diferencia ocular promedio (%)", "Calidad (0–100)",
                "Caídas de FPS", "Tiempo con FPS bajos (s)", "Ojos abiertos observados (%)", "Cobertura ocular (%)");
            detalle.Columns(6, 7).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            FormatoDecimal(detalle, 8, 10, 12, 18, 19, 23, 24, 25, 26, 27, 28, 30, 32);
            foreach (int columna in new[] { 11, 17, 29, 33, 34 })
                detalle.Column(columna).Style.NumberFormat.Format = "0.00%";

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
                    fatiga?.TiempoOcularCerradoMs / 1000.0, detalleOcular?.DiferenciaAperturaPromedio,
                    calidad?.Puntaje, calidad?.CaidasFps, calidad?.TiempoFpsBajoMs / 1000.0,
                    sesion.OjosAbiertosObservados, sesion.CoberturaOcular);

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
                    Escribir(eventos, filaEvento++, id, PaginaHistorial.NombreEvento(evento),
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
                AgregarPeriodos(libro, "Por día", "Fecha local", "Sesiones registradas por día", resumen.PorDia()),
                AgregarPeriodos(libro, "Por semana", "Semana (lunes de inicio)", "Sesiones registradas por semana", resumen.PorSemana()),
                AgregarPeriodos(libro, "Por mes", "Mes local", "Sesiones registradas por mes", resumen.PorMes()),
                AgregarPeriodos(libro, "Por hora", "Hora local de inicio", "Horario de inicio de las sesiones", resumen.PorHora(), cronologico: false),
                AgregarCategorias(libro, "Alarmas por hora", "Hora local", "Alarmas", "Alarmas registradas por hora del día", resumen.AlertasPorHora(),
                    $"Solo se agrupan alarmas con hora registrada. Alarmas excluidas: {resumen.AlertasSinHora}."),
                AgregarCategorias(libro, "Eventos por gravedad", "Gravedad", "Eventos", "Eventos registrados por gravedad", resumen.PorGravedad(),
                    "Gravedad definida por el tipo de evento. El recuento incluye alarmas sin hora.")
            };

            var atencion = CrearHoja(libro, "Atención", "ID de sesión", "Puntaje medio /100",
                "Primer síntoma activo (s)", "Minuto activo de mayor fatiga", "Tendencia", "Resumen automático");
            var evolucionAtencion = CrearHoja(libro, "Evolución atención", "ID de sesión", "Instante local",
                "Tiempo activo (s)", "Tiempo representado (s)", "Puntaje /100", "Calidad /100", "Distracción medida", "Descuentos (puntos)");
            evolucionAtencion.Column(2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss.000";
            var respuestas = CrearHoja(libro, "Respuesta a alarmas", "ID de sesión", "Alarma local",
                "Respuesta ocular (s)", "Recuperación confirmada (s)", "Resultado");
            respuestas.Column(2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss.000";
            int filaAtencion = 2, filaMuestra = 2, filaRespuesta = 2;
            foreach (var s in sesiones)
            {
                var informe = s.Atencion;
                Escribir(atencion, filaAtencion++, s.Id.ToString(), informe is null ? null : AnalisisAtencion.Promedio(informe.Muestras),
                    informe?.PrimerSintomaActivoMs / 1000.0, informe is null ? null : AnalisisAtencion.PeorMinuto(informe)?.Minuto,
                    informe is null ? "Sin medición" : AnalisisAtencion.Tendencia(informe), AnalisisAtencion.Resumir(informe));
                if (informe is null) continue;
                foreach (var m in informe.Muestras)
                    Escribir(evolucionAtencion, filaMuestra++, s.Id.ToString(), m.Instante.LocalDateTime,
                        m.ActivoMs / 1000.0, m.PesoMs / 1000.0, m.Puntaje, m.Calidad,
                        m.DistraccionDisponible ? "Sí" : "No", string.Join("; ", m.Factores.Select(f => $"{f.Nombre}: −{f.Descuento:0.0}")));
                foreach (var r in informe.Respuestas)
                    Escribir(respuestas, filaRespuesta++, s.Id.ToString(), r.Inicio.LocalDateTime,
                        r.RespuestaMs / 1000.0, r.RecuperacionMs / 1000.0, r.Estado);
            }
            FormatoDecimal(atencion, 2, 3);
            FormatoDecimal(evolucionAtencion, 3, 4, 5, 6);
            FormatoDecimal(respuestas, 3, 4);

            var personal = CrearHoja(libro, "Comparación personal", "ID de sesión", "Cédula",
                "Sesiones base antes", "Sesiones base después", "Perfil calibrado antes",
                "Parpadeos/min actual", "Parpadeos/min base", "Diferencia parpadeos/min",
                "PERCLOS actual", "PERCLOS base", "Diferencia PERCLOS",
                "Atención actual /100", "Atención base /100", "Diferencia atención",
                "Umbral parpadeos/min", "Umbral PERCLOS", "Umbral atención",
                "Cambio de parpadeos anormal", "PERCLOS anormal", "Atención anormal",
                "Incorporada al perfil", "Estado");
            personal.Columns(9, 11).Style.NumberFormat.Format = "0.00%";
            personal.Column(16).Style.NumberFormat.Format = "0.00%";
            FormatoDecimal(personal, 6, 7, 8, 12, 13, 14, 15, 17);
            int filaPersonal = 2;
            foreach (SesionConduccion s in sesiones)
            {
                ComparacionPersonal? c = s.ComparacionPersonal;
                Escribir(personal, filaPersonal++, s.Id.ToString(), s.Conductor.Cedula,
                    c?.SesionesBaseAntes, c?.SesionesBaseDespues,
                    c is null ? "Sin medición" : c.PerfilCalibradoAntes ? "Sí" : "No",
                    c?.ParpadeosActual, c?.ParpadeosBase, c?.DiferenciaParpadeos,
                    c?.PerclosActual, c?.PerclosBase, c?.DiferenciaPerclos,
                    c?.AtencionActual, c?.AtencionBase, c?.DiferenciaAtencion,
                    c?.UmbralParpadeos, c?.UmbralPerclos, c?.UmbralAtencion,
                    c is null ? "Sin medición" : c.CambioParpadeosAnormal ? "Sí" : "No",
                    c is null ? "Sin medición" : c.PerclosAnormal ? "Sí" : "No",
                    c is null ? "Sin medición" : c.AtencionAnormal ? "Sí" : "No",
                    c is null ? "Sin medición" : c.IncorporadaAlPerfil ? "Sí" : "No",
                    c?.Estado ?? "Sin comparación personal");
            }

            foreach (IXLWorksheet hoja in libro.Worksheets)
            {

                int columnas = hoja.Row(1).LastCellUsed()!.Address.ColumnNumber;
                int filas = hoja.Column(1).LastCellUsed()!.Address.RowNumber;
                if (hoja == general)
                    filas = 2;
                for (int columna = 1; columna <= columnas; columna++)
                    hoja.Column(columna).AdjustToContents(1, Math.Min(200, filas), 13, 40);

                hoja.SheetView.FreezeRows(1);
                hoja.Range(1, 1, filas, columnas).SetAutoFilter();
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
            string titulo, IEnumerable<ResumenPeriodo> periodos, bool cronologico = true)
        {
            var hoja = CrearHoja(libro, nombre, periodo, "Sesiones", "Duración promedio (s)");
            FormatoDecimal(hoja, 3);
            int fila = 2;
            var datos = new List<ResumenCategoria>();
            foreach (ResumenPeriodo resumen in cronologico ? periodos.Reverse() : periodos)
            {
                Escribir(hoja, fila++, resumen.Periodo, resumen.Sesiones, resumen.PromedioSegundos);
                datos.Add(new ResumenCategoria(resumen.Periodo, resumen.Sesiones));
            }
            int desplazamiento = cronologico ? Math.Max(0, datos.Count - 24) : 0;
            var mostrados = datos.Skip(desplazamiento).Take(24).ToArray();
            string contexto = cronologico
                ? $"Se muestran {mostrados.Length} de {datos.Count} periodos con sesiones, comenzando por el más antiguo del gráfico."
                : "Se agrupan las sesiones por la hora local en que comenzaron.";
            AgregarNotasGrafico(hoja, mostrados, "sesiones", contexto);
            return new GraficoExcel(nombre, titulo, periodo, "Sesiones", mostrados, desplazamiento + 2);
        }

        private static GraficoExcel AgregarCategorias(XLWorkbook libro, string nombre, string categoria,
            string unidad, string titulo, IEnumerable<ResumenCategoria> categorias, string contexto)
        {
            var hoja = CrearHoja(libro, nombre, categoria, unidad);
            var datos = categorias.ToArray();
            for (int i = 0; i < datos.Length; i++)
                Escribir(hoja, i + 2, datos[i].Categoria, datos[i].Cantidad);
            AgregarNotasGrafico(hoja, datos, unidad.ToLowerInvariant(), contexto);
            return new GraficoExcel(nombre, titulo, categoria, unidad, datos);
        }

        private static void AgregarNotasGrafico(IXLWorksheet hoja, IReadOnlyList<ResumenCategoria> datos,
            string unidad, string contexto)
        {
            AgregarNota(hoja, 1, 5, 16, "Qué muestra", contexto + " Consulta el alcance en la hoja Resumen.");
            int maximo = datos.Select(d => d.Cantidad).DefaultIfEmpty().Max();
            var picos = datos.Where(d => d.Cantidad == maximo).ToArray();
            string conclusion = maximo == 0 ? $"Sin {unidad} para graficar."
                : picos.Length == 1 ? $"El mayor recuento fue {maximo} ({picos[0].Categoria})."
                : $"El mayor recuento fue {maximo}, compartido por {picos.Length} categorías.";
            AgregarNota(hoja, maximo > 0 ? 25 : 5, 5, 16, "Conclusión", conclusion);
        }

        private static void AgregarNota(IXLWorksheet hoja, int fila, int desde, int hasta,
            string etiqueta, string contenido)
        {
            var titulo = hoja.Range(fila, desde, fila, hasta).Merge();
            titulo.FirstCell().Value = etiqueta;
            titulo.Style.Font.Bold = true;
            titulo.Style.Font.FontColor = XLColor.FromHtml("#244863");
            var nota = hoja.Range(fila + 1, desde, fila + 1, hasta).Merge();
            nota.FirstCell().Value = contenido;
            nota.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF2F8");
            nota.Style.Alignment.WrapText = true;
            hoja.Row(fila + 1).Height = fila == 11 && desde == 1 ? 52 : 32;
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
