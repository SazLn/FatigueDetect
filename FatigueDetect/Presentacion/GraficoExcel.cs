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
    internal sealed record GraficoExcel(string Hoja, string Unidad, IReadOnlyList<ResumenCategoria> Datos)
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
            foreach (GraficoExcel grafico in graficos.Where(g => g.Datos.Count > 0))
            {
                var hoja = (WorksheetPart)libro.GetPartById(hojas[grafico.Hoja].Id!.Value!);
                DrawingsPart dibujo = hoja.AddNewPart<DrawingsPart>();
                ChartPart parte = dibujo.AddNewPart<ChartPart>();
                parte.ChartSpace = new Charts.ChartSpace(grafico.CrearContenido().ToString(SaveOptions.DisableFormatting));
                parte.ChartSpace.Save();

                string nombre = grafico.Hoja + " · " + grafico.Unidad;
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
            int final = Datos.Count + 1;
            string rangoA = $"'{nombre}'!$A$2:$A${final}";
            string rangoB = $"'{nombre}'!$B$2:$B${final}";
            string titulo = Hoja + " · " + Unidad;
            var serie = new XElement(C + "ser", Valor("idx", 0), Valor("order", 0),
                new XElement(C + "tx", new XElement(C + "v", Unidad)),
                new XElement(C + "spPr", new XElement(A + "solidFill",
                    new XElement(A + "srgbClr", new XAttribute("val", "307CCA")))),
                new XElement(C + "cat", new XElement(C + "strRef", new XElement(C + "f", rangoA),
                    new XElement(C + "strCache", Valor("ptCount", Datos.Count),
                        Datos.Select((d, i) => Punto(i, d.Categoria))))),
                new XElement(C + "val", new XElement(C + "numRef", new XElement(C + "f", rangoB),
                    new XElement(C + "numCache", new XElement(C + "formatCode", "0"),
                        Valor("ptCount", Datos.Count),
                        Datos.Select((d, i) => Punto(i, d.Cantidad.ToString(CultureInfo.InvariantCulture)))))));

            return new XElement(C + "chartSpace", new XElement(C + "chart",
                new XElement(C + "title", new XElement(C + "tx", new XElement(C + "rich",
                    new XElement(A + "bodyPr"), new XElement(A + "lstStyle"),
                    new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", titulo)))))),
                new XElement(C + "plotArea", new XElement(C + "layout"),
                    new XElement(C + "barChart", Valor("barDir", "col"), Valor("grouping", "clustered"),
                        serie, Valor("axId", 101), Valor("axId", 102)),
                    Eje("catAx", 101, 102, "b"), Eje("valAx", 102, 101, "l")),
                Valor("plotVisOnly", 1)));
        }

        private static XElement Eje(string nombre, int id, int cruzado, string posicion)
        {
            bool numerico = nombre == "valAx";
            return new XElement(C + nombre, Valor("axId", id),
                new XElement(C + "scaling", Valor("orientation", "minMax"), numerico ? Valor("min", 0) : null),
                Valor("axPos", posicion), numerico ? new XElement(C + "majorGridlines") : null,
                Valor("tickLblPos", "nextTo"), Valor("crossAx", cruzado), Valor("crosses", "autoZero"));
        }

        private static XElement Valor(string nombre, object valor) =>
            new(C + nombre, new XAttribute("val", valor));

        private static XElement Punto(int indice, string valor) =>
            new(C + "pt", new XAttribute("idx", indice), new XElement(C + "v", valor));

        private static XElement CrearAnclaje(string id, string nombre) =>
            new(Xdr + "wsDr",
                new XElement(Xdr + "twoCellAnchor", Marcador("from", 4, 1), Marcador("to", 16, 19),
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
