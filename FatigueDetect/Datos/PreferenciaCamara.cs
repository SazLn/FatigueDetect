using System;
using System.IO;
using System.Globalization;
using FatigueDetect.Entidades;

namespace FatigueDetect.Datos
{
    public sealed class PreferenciaCamara(string ruta)
    {
        public (string? Id, FormatoCamara Formato) Leer()
        {
            try
            {
                string[] lineas = File.ReadAllLines(ruta);
                string? id = lineas.Length > 0 ? lineas[0].Trim() : null;
                FormatoCamara formato = FormatoCamara.Automatico;
                if (lineas.Length >= 4 && int.TryParse(lineas[1], CultureInfo.InvariantCulture, out int ancho)
                    && int.TryParse(lineas[2], CultureInfo.InvariantCulture, out int alto)
                    && int.TryParse(lineas[3], CultureInfo.InvariantCulture, out int fps))
                {
                    var guardado = new FormatoCamara(ancho, alto, fps);
                    if (guardado.EsValido())
                        formato = guardado;
                }

                return (string.IsNullOrEmpty(id) ? null : id, formato);
            }
            catch (FileNotFoundException)
            {
                return (null, FormatoCamara.Automatico);
            }
            catch (DirectoryNotFoundException)
            {
                return (null, FormatoCamara.Automatico);
            }
        }

        public void Guardar(string? id, FormatoCamara formato)
        {
            if (!formato.EsValido())
                throw new ArgumentOutOfRangeException(nameof(formato));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
            string temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temporal, new[] { id ?? "", formato.Ancho.ToString(CultureInfo.InvariantCulture),
                    formato.Alto.ToString(CultureInfo.InvariantCulture), formato.Fps.ToString(CultureInfo.InvariantCulture) });
                File.Move(temporal, ruta, overwrite: true);
            }
            finally
            {
                File.Delete(temporal);
            }
        }
    }
}
