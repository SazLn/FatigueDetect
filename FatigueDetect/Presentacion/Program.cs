using System;
using System.IO;
using System.Windows.Forms;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Archivos guardados en: Documentos\FatigueDetect\
            string documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string carpeta = Path.Combine(documentos, "FatigueDetect");

            var sesionService = new SesionService(new HistorialRepository(Path.Combine(carpeta, "historial.csv")));
            var catalogoTipos = new CatalogoTiposCarro(new TiposCarroRepository(Path.Combine(carpeta, "tipos_carro.txt")));
            var configuracion = new Configuracion();

            Application.Run(new MainForm(sesionService, catalogoTipos, configuracion));
        }
    }
}
