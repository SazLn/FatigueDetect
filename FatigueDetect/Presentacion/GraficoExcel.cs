using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Charts = DocumentFormat.OpenXml.Drawing.Charts;
using Drawing = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    // ClosedXML escribe las celdas. Open XML agrega gráficos nativos con referencias a esas celdas.
    internal sealed record GraficoExcel(string Hoja, string Titulo, string EjeHorizontal,
        string Serie, IReadOnlyList<ResumenCategoria> Datos, int PrimeraFila = 2)
    {
        private static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
        private static readonly XNamespace Xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void Guardar(string ruta, IEnumerable<GraficoExcel> graficos)
        {
            using var documento = SpreadsheetDocument.Open(ruta, true);
            WorkbookPart libro = documento.WorkbookPart!;
            var hojas = libro.Workbook.Sheets!.Elements<Sheet>().ToDictionary(h => h.Name!.Value!);
            foreach (GraficoExcel grafico in graficos.Where(g => g.Datos.Any(d => d.Cantidad > 0)))
            {
                var hoja = (WorksheetPart)libro.GetPartById(hojas[grafico.Hoja].Id!.Value!);
                DrawingsPart dibujo = hoja.AddNewPart<DrawingsPart>();
                ChartPart parte = dibujo.AddNewPart<ChartPart>();
                parte.ChartSpace = new Charts.ChartSpace(grafico.CrearContenido().ToString(SaveOptions.DisableFormatting));
                parte.ChartSpace.Save();

                string nombre = grafico.Titulo;
                dibujo.WorksheetDrawing = new Drawing.WorksheetDrawing(
                    CrearAnclaje(dibujo.GetIdOfPart(parte), nombre).ToString(SaveOptions.DisableFormatting));
                dibujo.WorksheetDrawing.Save();
                hoja.Worksheet.AddChild(new DocumentFormat.OpenXml.Spreadsheet.Drawing { Id = hoja.GetIdOfPart(dibujo) });
                hoja.Worksheet.Save();
            }
        }

        private XElement CrearContenido()
        {
            string nombre = Hoja.Replace("'", "''");
            int final = PrimeraFila + Datos.Count - 1;
            string rangoA = $"'{nombre}'!$A${PrimeraFila}:$A${final}";
            string rangoB = $"'{nombre}'!$B${PrimeraFila}:$B${final}";
            var serie = new XElement(C + "ser", Valor("idx", 0), Valor("order", 0),
                new XElement(C + "tx", new XElement(C + "v", Serie)),
                new XElement(C + "spPr", new XElement(A + "solidFill",
                    new XElement(A + "srgbClr", new XAttribute("val", Serie == "Alarmas" ? "B35B16" : Serie == "Eventos" ? "955087" : "236D9E")))),
                new XElement(C + "cat", new XElement(C + "strRef", new XElement(C + "f", rangoA),
                    new XElement(C + "strCache", Valor("ptCount", Datos.Count),
                        Datos.Select((d, i) => Punto(i, d.Categoria))))),
                new XElement(C + "val", new XElement(C + "numRef", new XElement(C + "f", rangoB),
                    new XElement(C + "numCache", new XElement(C + "formatCode", "0"),
                        Valor("ptCount", Datos.Count),
                        Datos.Select((d, i) => Punto(i, d.Cantidad.ToString(CultureInfo.InvariantCulture)))))));

            return new XElement(C + "chartSpace", new XElement(C + "chart",
                TituloGrafico(Titulo),
                new XElement(C + "plotArea", new XElement(C + "layout"),
                    new XElement(C + "barChart", Valor("barDir", "col"), Valor("grouping", "clustered"),
                        serie, Valor("axId", 101), Valor("axId", 102)),
                                        Eje("catAx", 101, 102, "b", EjeHorizontal, (int)Math.Ceiling(Datos.Count / 12.0)),
                    Eje("valAx", 102, 101, "l", $"{Serie} (recuento)")),
                new XElement(C + "legend", Valor("legendPos", "b"), Valor("overlay", 0)),
                Valor("plotVisOnly", 1)));
        }

        private static XElement Eje(string nombre, int id, int cruzado, string posicion, string titulo, int saltoEtiquetas = 1)
        {
            bool numerico = nombre == "valAx";
            return new XElement(C + nombre, Valor("axId", id),
                new XElement(C + "scaling", Valor("orientation", "minMax"), numerico ? Valor("min", 0) : null),
                Valor("axPos", posicion), numerico ? new XElement(C + "majorGridlines") : null,
                TituloGrafico(titulo),
                numerico ? new XElement(C + "numFmt", new XAttribute("formatCode", "0"),
                    new XAttribute("sourceLinked", 0)) : null,
                                Valor("tickLblPos", "nextTo"), Valor("crossAx", cruzado), Valor("crosses", "autoZero"),
                !numerico && saltoEtiquetas > 1 ? Valor("tickLblSkip", saltoEtiquetas) : null);
        }

        private static XElement TituloGrafico(string texto) =>
            new(C + "title", new XElement(C + "tx", new XElement(C + "rich",
                new XElement(A + "bodyPr"), new XElement(A + "lstStyle"),
                new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", texto))))),
                Valor("overlay", 0));

        private static XElement Valor(string nombre, object valor) =>
            new(C + nombre, new XAttribute("val", valor));

        private static XElement Punto(int indice, string valor) =>
            new(C + "pt", new XAttribute("idx", indice), new XElement(C + "v", valor));

        private static XElement CrearAnclaje(string id, string nombre) =>
            new(Xdr + "wsDr",
                new XElement(Xdr + "twoCellAnchor", Marcador("from", 4, 3), Marcador("to", 16, 23),
                    new XElement(Xdr + "graphicFrame", new XAttribute("macro", ""),
                        new XElement(Xdr + "nvGraphicFramePr",
                            new XElement(Xdr + "cNvPr", new XAttribute("id", 1), new XAttribute("name", nombre)),
                            new XElement(Xdr + "cNvGraphicFramePr")),
                        new XElement(Xdr + "xfrm",
                            new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                            new XElement(A + "ext", new XAttribute("cx", 0), new XAttribute("cy", 0))),
                        new XElement(A + "graphic", new XElement(A + "graphicData", new XAttribute("uri", C),
                            new XElement(C + "chart", new XAttribute(R + "id", id))))),
                    new XElement(Xdr + "clientData")));

        private static XElement Marcador(string tipo, int columna, int fila) =>
            new(Xdr + tipo, new XElement(Xdr + "col", columna), new XElement(Xdr + "colOff", 0),
                new XElement(Xdr + "row", fila), new XElement(Xdr + "rowOff", 0));
    }
}
