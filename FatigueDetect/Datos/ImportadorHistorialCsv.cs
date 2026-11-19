using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FatigueDetect.Entidades;

namespace FatigueDetect.Datos
{
    internal static class ImportadorHistorialCsv
    {
        public static IEnumerable<SesionConduccion> Leer(string ruta)
        {
            IEnumerable<string> lineas;
            try
            {
                lineas = File.ReadLines(ruta);
            }
            catch (FileNotFoundException)
            {
                yield break;
            }
            catch (DirectoryNotFoundException)
            {
                yield break;
            }

            foreach (string linea in lineas.Skip(1))
            {
                string[] campos = linea.Split(';');
                if (campos.Length < 8 || LeerFecha(campos[5]) is not DateTime inicio)
                    continue;

                int.TryParse(campos[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out int alarmas);
                var sesion = new SesionConduccion
                {
                    Conductor = new Conductor { Cedula = campos[0], Nombre = campos[1], Apellidos = campos[2] },
                    Vehiculo = new Vehiculo { TipoCarro = campos[3], Placa = campos[4] },
                    Inicio = inicio,
                    Fin = LeerFecha(campos[6]),
                    CantidadAlarmas = Math.Max(0, alarmas)
                };

                if (campos.Length >= 9)
                {
                    foreach (string texto in campos[8].Split('|'))
                    {
                        if (LeerFecha(texto) is DateTime hora)
                            sesion.RegistrarEvento(new EventoSesion(TipoEventoSesion.Alarma, new DateTimeOffset(hora), null));
                    }
                }
                yield return sesion;
            }
        }

        private static DateTime? LeerFecha(string texto)
        {
            return DateTime.TryParseExact(texto, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out DateTime fecha) ? fecha : null;
        }
    }
}
