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

            var sesionService = new SesionService(new HistorialSqliteRepository(
                Path.Combine(carpeta, "historial.sqlite"), Path.Combine(carpeta, "historial.csv")));
            var catalogoTipos = new CatalogoTiposCarro(new TiposCarroRepository(Path.Combine(carpeta, "tipos_carro.txt")));
            var configuracion = new Configuracion();
            var preferenciaCamara = new PreferenciaCamara(Path.Combine(carpeta, "camara_preferida.txt"));
            try
            {
                configuracion.CamaraId = preferenciaCamara.Leer();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show("No se pudo leer la cámara guardada. Se usará la selección automática.\n" + ex.Message,
                    "Configuración de cámara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            string cascadaRostros = Path.Combine(
                AppContext.BaseDirectory, "Resources", "cascades", "haarcascade_frontalface_default.xml");
            var servicioBiometrico = new ServicioBiometrico(
                new PerfilBiometricoRepository(Path.Combine(carpeta, "perfiles_biometricos.json")),
                cascadaRostros, configuracion);

            Application.Run(new MainForm(sesionService, catalogoTipos, configuracion, servicioBiometrico, preferenciaCamara));
        }
    }
}
