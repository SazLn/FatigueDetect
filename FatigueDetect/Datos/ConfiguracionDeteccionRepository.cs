using System;
using System.IO;
using System.Text.Json;
using FatigueDetect.Entidades;

namespace FatigueDetect.Datos
{
    public sealed class ConfiguracionDeteccionRepository(string ruta)
    {
        public AjustesDeteccion Leer()
        {
            try
            {
                using FileStream archivo = File.OpenRead(ruta);
                AjustesDeteccion? ajustes = JsonSerializer.Deserialize<AjustesDeteccion>(archivo);
                if (ajustes is null || !ajustes.EsValido())
                    throw new InvalidDataException("El archivo contiene valores de detección no válidos.");

                return ajustes;
            }
            catch (FileNotFoundException)
            {
                return AjustesDeteccion.Predeterminados;
            }
            catch (DirectoryNotFoundException)
            {
                return AjustesDeteccion.Predeterminados;
            }
        }

        public void Guardar(AjustesDeteccion ajustes)
        {
            if (!ajustes.EsValido())
                throw new ArgumentException("Los umbrales de detección no son válidos.", nameof(ajustes));

            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            string temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream archivo = File.Create(temporal))
                    JsonSerializer.Serialize(archivo, ajustes);

                File.Move(temporal, ruta, overwrite: true);
            }
            finally
            {
                File.Delete(temporal);
            }
        }
    }
}
