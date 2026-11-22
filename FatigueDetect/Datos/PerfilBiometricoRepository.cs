using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FatigueDetect.Entidades;

namespace FatigueDetect.Datos
{
    /// <summary>Persistencia local de perfiles. No almacena imágenes del rostro.</summary>
    public class PerfilBiometricoRepository
    {
        private readonly string rutaArchivo;
        private readonly ProtectorDatosLocal protector;
        private readonly object candado = new object();
        private readonly JsonSerializerOptions opciones = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public PerfilBiometricoRepository(string rutaArchivo, ProtectorDatosLocal? protector = null)
        {
            this.rutaArchivo = rutaArchivo;
            this.protector = protector ?? new ProtectorDatosLocal(
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rutaArchivo))!, "clave_datos.bin"));
        }

        public string RutaArchivo => rutaArchivo;

        public List<PerfilBiometrico> LeerTodos()
        {
            lock (candado)
            {
                if (!File.Exists(rutaArchivo))
                {
                    return new List<PerfilBiometrico>();
                }

                string contenido = File.ReadAllText(rutaArchivo);
                bool legado = !protector.EstaCifrado(contenido);
                string json = protector.Descifrar(contenido);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<PerfilBiometrico>();
                }

                List<PerfilBiometrico> perfiles = JsonSerializer.Deserialize<List<PerfilBiometrico>>(json, opciones)
                    ?? new List<PerfilBiometrico>();
                if (legado)
                    EscribirSinCandado(perfiles);
                return perfiles;
            }
        }

        public bool Existe(string cedula)
        {
            string normalizada = cedula.Trim();
            return LeerTodos().Any(perfil => perfil.Cedula == normalizada);
        }

        public void Guardar(PerfilBiometrico perfil)
        {
            lock (candado)
            {
                List<PerfilBiometrico> perfiles = LeerTodosSinCandado();
                perfiles.RemoveAll(actual => actual.Cedula == perfil.Cedula);
                perfiles.Add(perfil);
                perfiles.Sort((a, b) => string.Compare(a.Cedula, b.Cedula, StringComparison.Ordinal));
                EscribirSinCandado(perfiles);
            }
        }

        public bool Eliminar(string cedula)
        {
            lock (candado)
            {
                List<PerfilBiometrico> perfiles = LeerTodosSinCandado();
                int eliminados = perfiles.RemoveAll(perfil => perfil.Cedula == cedula.Trim());
                if (eliminados > 0)
                {
                    EscribirSinCandado(perfiles);
                }
                return eliminados > 0;
            }
        }

        public int EliminarAnteriores(DateTime limiteUtc)
        {
            lock (candado)
            {
                List<PerfilBiometrico> perfiles = LeerTodosSinCandado();
                int eliminados = perfiles.RemoveAll(p => p.ActualizadoUtc < limiteUtc);
                if (eliminados > 0)
                    EscribirSinCandado(perfiles);
                return eliminados;
            }
        }

        private List<PerfilBiometrico> LeerTodosSinCandado()
        {
            if (!File.Exists(rutaArchivo))
            {
                return new List<PerfilBiometrico>();
            }

            string json = protector.Descifrar(File.ReadAllText(rutaArchivo));
            return string.IsNullOrWhiteSpace(json)
                ? new List<PerfilBiometrico>()
                : JsonSerializer.Deserialize<List<PerfilBiometrico>>(json, opciones)
                  ?? new List<PerfilBiometrico>();
        }

        private void EscribirSinCandado(List<PerfilBiometrico> perfiles)
        {
            string? carpeta = Path.GetDirectoryName(rutaArchivo);
            if (!string.IsNullOrEmpty(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            string temporal = rutaArchivo + ".tmp";
            File.WriteAllText(temporal, protector.Cifrar(JsonSerializer.Serialize(perfiles, opciones)));
            File.Move(temporal, rutaArchivo, true);
        }
    }
}
