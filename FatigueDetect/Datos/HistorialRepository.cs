using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FatigueDetect.Entidades;

namespace FatigueDetect.Datos
{
    /// <summary>
    /// Guarda y lee el historial de sesiones en un archivo CSV (separado por ';',
    /// se puede abrir directamente con Excel).
    /// </summary>
    public class HistorialRepository
    {
        private const char Separador = ';';
        private const char SeparadorHoras = '|';
        private const string FormatoFecha = "yyyy-MM-dd HH:mm:ss";
        private const string Cabecera = "Cedula;Nombre;Apellidos;TipoCarro;Placa;Inicio;Fin;Alarmas;HorasAlarmas";

        private static readonly object candado = new object();
        private readonly string rutaArchivo;

        public string RutaArchivo => rutaArchivo;

        public HistorialRepository(string rutaArchivo)
        {
            this.rutaArchivo = rutaArchivo;
        }

        public void Guardar(SesionConduccion sesion)
        {
            lock (candado)
            {
                string? carpeta = Path.GetDirectoryName(rutaArchivo);
                if (!string.IsNullOrEmpty(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                bool archivoNuevo = !File.Exists(rutaArchivo) || new FileInfo(rutaArchivo).Length == 0;
                if (!archivoNuevo)
                {
                    ActualizarCabecera();
                }

                var texto = new StringBuilder();
                if (archivoNuevo)
                {
                    texto.AppendLine(Cabecera);
                }

                string horas = string.Join(SeparadorHoras,
                    sesion.HorasAlarmas.Select(h => h.ToString(FormatoFecha, CultureInfo.InvariantCulture)));

                string[] campos =
                {
                    Limpiar(sesion.Conductor.Cedula),
                    Limpiar(sesion.Conductor.Nombre),
                    Limpiar(sesion.Conductor.Apellidos),
                    Limpiar(sesion.Vehiculo.TipoCarro),
                    Limpiar(sesion.Vehiculo.Placa),
                    sesion.Inicio.ToString(FormatoFecha, CultureInfo.InvariantCulture),
                    (sesion.Fin ?? DateTime.Now).ToString(FormatoFecha, CultureInfo.InvariantCulture),
                    sesion.CantidadAlarmas.ToString(CultureInfo.InvariantCulture),
                    horas
                };
                texto.AppendLine(string.Join(Separador, campos));

                // UTF-8 con BOM para que Excel muestre bien las tildes y la ñ.
                File.AppendAllText(rutaArchivo, texto.ToString(), new UTF8Encoding(true));
            }
        }

        public List<SesionConduccion> LeerTodas()
        {
            var lista = new List<SesionConduccion>();

            lock (candado)
            {
                if (!File.Exists(rutaArchivo))
                {
                    return lista;
                }

                foreach (string linea in File.ReadLines(rutaArchivo, Encoding.UTF8).Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(linea)) continue;

                    string[] c = linea.Split(Separador);
                    if (c.Length < 8) continue;

                    if (!DateTime.TryParseExact(c[5], FormatoFecha, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTime inicio))
                    {
                        continue;
                    }

                    DateTime? fin = null;
                    if (DateTime.TryParseExact(c[6], FormatoFecha, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTime finLeido))
                    {
                        fin = finLeido;
                    }

                    int.TryParse(c[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out int alarmas);

                    var sesion = new SesionConduccion
                    {
                        Conductor = new Conductor { Cedula = c[0], Nombre = c[1], Apellidos = c[2] },
                        Vehiculo = new Vehiculo { TipoCarro = c[3], Placa = c[4] },
                        Inicio = inicio,
                        Fin = fin,
                        CantidadAlarmas = alarmas
                    };

                    // Columna 9 (solo en registros nuevos): hora de cada alarma.
                    if (c.Length >= 9 && c[8].Length > 0)
                    {
                        var horas = new List<DateTime>();
                        foreach (string texto in c[8].Split(SeparadorHoras))
                        {
                            if (DateTime.TryParseExact(texto, FormatoFecha, CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out DateTime hora))
                            {
                                horas.Add(hora);
                            }
                        }
                        sesion.EstablecerHorasAlarmas(horas);
                    }

                    lista.Add(sesion);
                }
            }

            return lista;
        }

        public void Borrar()
        {
            lock (candado)
            {
                if (File.Exists(rutaArchivo))
                {
                    File.Delete(rutaArchivo);
                }
            }
        }

        // Si el archivo viene de una versión anterior (sin la columna de horas), actualiza la cabecera.
        private void ActualizarCabecera()
        {
            string[] lineas = File.ReadAllLines(rutaArchivo, Encoding.UTF8);
            if (lineas.Length > 0 && lineas[0] != Cabecera)
            {
                lineas[0] = Cabecera;
                File.WriteAllLines(rutaArchivo, lineas, new UTF8Encoding(true));
            }
        }

        // Evita que un ';' o un salto de línea escrito por el usuario dañe el archivo.
        private static string Limpiar(string valor)
        {
            return (valor ?? "")
                .Replace(Separador, ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }
    }
}
