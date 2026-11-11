using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FatigueDetect.Datos
{
    /// <summary>Guarda los tipos de carro que el usuario va registrando (un tipo por línea).</summary>
    public class TiposCarroRepository
    {
        private static readonly object candado = new object();
        private readonly string rutaArchivo;

        public TiposCarroRepository(string rutaArchivo)
        {
            this.rutaArchivo = rutaArchivo;
        }

        public List<string> LeerTodos()
        {
            lock (candado)
            {
                if (!File.Exists(rutaArchivo))
                {
                    return new List<string>();
                }

                return File.ReadAllLines(rutaArchivo, Encoding.UTF8)
                    .Select(linea => linea.Trim())
                    .Where(linea => linea.Length > 0)
                    .ToList();
            }
        }

        public void Agregar(string tipo)
        {
            lock (candado)
            {
                string? carpeta = Path.GetDirectoryName(rutaArchivo);
                if (!string.IsNullOrEmpty(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                File.AppendAllText(rutaArchivo, tipo + Environment.NewLine, new UTF8Encoding(false));
            }
        }
    }
}
