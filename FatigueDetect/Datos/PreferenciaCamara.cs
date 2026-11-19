using System;
using System.IO;

namespace FatigueDetect.Datos
{
    public sealed class PreferenciaCamara(string ruta)
    {
        public string? Leer()
        {
            try
            {
                string id = File.ReadAllText(ruta).Trim();
                return id.Length == 0 ? null : id;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        public void Guardar(string? id)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            string temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporal, id ?? "");
                File.Move(temporal, ruta, overwrite: true);
            }
            finally
            {
                File.Delete(temporal);
            }
        }
    }
}
