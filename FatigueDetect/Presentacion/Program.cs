using System;
using System.IO;
using System.Text.Json;
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

            var protector = new ProtectorDatosLocal(Path.Combine(carpeta, "clave_datos.bin"));
            var historialRepository = new HistorialSqliteRepository(
                Path.Combine(carpeta, "historial.sqlite"), Path.Combine(carpeta, "historial.csv"), protector);
            var perfilesRepository = new PerfilBiometricoRepository(
                Path.Combine(carpeta, "perfiles_biometricos.json"), protector);
            var politicaRepository = new PoliticaProteccionRepository(Path.Combine(carpeta, "politica_datos.json"));
            var proteccionDatos = new ProteccionDatosService(carpeta, protector, historialRepository, perfilesRepository);
            try { proteccionDatos.AplicarRetencion(politicaRepository.Leer(), DateTime.Now); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                MessageBox.Show("No se pudo aplicar la política de retención. No se eliminó información.\n" + ex.Message,
                    "Protección de datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            var sesionService = new SesionService(historialRepository);
            var catalogoTipos = new CatalogoTiposCarro(new TiposCarroRepository(Path.Combine(carpeta, "tipos_carro.txt")));
            var configuracion = new Configuracion();
            var repositorioAjustes = new ConfiguracionDeteccionRepository(Path.Combine(carpeta, "configuracion.json"));
            try
            {
                configuracion.Deteccion = repositorioAjustes.Leer();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                MessageBox.Show("No se pudo leer la configuración de detección. Se usarán los valores predeterminados.\n" + ex.Message,
                    "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            var preferenciaCamara = new PreferenciaCamara(Path.Combine(carpeta, "camara_preferida.txt"));
            try
            {
                (configuracion.CamaraId, configuracion.FormatoCamara) = preferenciaCamara.Leer();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show("No se pudo leer la cámara guardada. Se usará la selección automática.\n" + ex.Message,
                    "Configuración de cámara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            string cascadaRostros = Path.Combine(
                AppContext.BaseDirectory, "Resources", "cascades", "haarcascade_frontalface_default.xml");
            var servicioBiometrico = new ServicioBiometrico(
                perfilesRepository,
                cascadaRostros, configuracion);

            Application.Run(new MainForm(sesionService, catalogoTipos, configuracion, servicioBiometrico,
                preferenciaCamara, repositorioAjustes, proteccionDatos, politicaRepository));
        }
    }
}
