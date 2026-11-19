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
            var general = CrearHoja(libro, "Resumen", "Sesiones", "Duración promedio (s)");
            Escribir(general, 2, resumen.TotalSesiones, resumen.PromedioSegundos);
            FormatoDecimal(general, 2);

            var detalle = CrearHoja(libro, "Sesiones", "ID de sesión", "Conductor", "Cédula",
                "Vehículo", "Placa", "Inicio local", "Fin local", "Duración (s)", "Alarmas",
                "FPS", "Fotogramas analizables", "Rostro válido (s)", "Fallos de cámara");
            detalle.Columns(6, 7).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            FormatoDecimal(detalle, 8, 10, 12);
            detalle.Column(11).Style.NumberFormat.Format = "0.00%";

            var eventos = CrearHoja(libro, "Eventos", "ID de sesión", "Tipo", "Inicio local",
                "Fin local", "Duración (s)");
            eventos.Columns(3, 4).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss.000";
            FormatoDecimal(eventos, 5);

            var tipos = CrearHoja(libro, "Resumen de eventos", "ID de sesión", "Tipo", "Cantidad",
                "Duración total (s)", "Duración máxima (s)", "Sin duración");
            FormatoDecimal(tipos, 4, 5);
            int filaSesion = 2, filaEvento = 2, filaTipo = 2;

            foreach (SesionConduccion sesion in sesiones)
            {
                string id = sesion.Id.ToString();
                CalidadSesion? calidad = sesion.Calidad;
                Escribir(detalle, filaSesion++, id, sesion.Conductor.NombreCompleto,
                    sesion.Conductor.Cedula, sesion.Vehiculo.TipoCarro, sesion.Vehiculo.Placa,
                    sesion.Inicio, sesion.Fin, sesion.Fin.HasValue ? sesion.Duracion.TotalSeconds : null,
                    sesion.CantidadAlarmas, calidad?.Fps, calidad?.ProporcionAnalizable,
                    calidad?.TiempoRostroValido.TotalSeconds, calidad?.FallosCamara);

                List<EventoSesion> registrados = sesion.Eventos;
                foreach (EventoSesion evento in registrados)
                    Escribir(eventos, filaEvento++, id, PaginaHistorial.NombreEvento(evento.Tipo),
                        evento.Inicio.LocalDateTime, evento.Fin?.LocalDateTime, evento.DuracionMilisegundos / 1000.0);

                foreach (ResumenEvento tipo in ResumenHistorial.PorTipo(sesion, registrados))
                    Escribir(tipos, filaTipo++, id, PaginaHistorial.NombreEvento(tipo.Tipo), tipo.Cantidad,
                        tipo.TotalMs / 1000.0, tipo.MaximoMs / 1000.0, tipo.SinDuracion);
            }

            AgregarPeriodos(libro, "Por día", "Fecha", resumen.PorDia());
            AgregarPeriodos(libro, "Por semana", "Lunes de inicio", resumen.PorSemana());
            AgregarPeriodos(libro, "Por mes", "Mes", resumen.PorMes());
            AgregarPeriodos(libro, "Por hora", "Hora de inicio", resumen.PorHora());

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
                File.Move(temporal, ruta, overwrite: true);
            }
            finally
            {
                File.Delete(temporal);
            }
        }

        private static void AgregarPeriodos(XLWorkbook libro, string nombre, string periodo,
            IEnumerable<ResumenPeriodo> periodos)
        {
            var hoja = CrearHoja(libro, nombre, periodo, "Sesiones", "Duración promedio (s)");
            FormatoDecimal(hoja, 3);
            int fila = 2;
            foreach (ResumenPeriodo resumen in periodos)
                Escribir(hoja, fila++, resumen.Periodo, resumen.Sesiones, resumen.PromedioSegundos);
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
