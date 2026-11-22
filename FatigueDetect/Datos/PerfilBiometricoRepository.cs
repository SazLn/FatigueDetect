using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using FatigueDetect.Entidades;

namespace FatigueDetect.Datos
{
    /// <summary>Persistencia local de perfiles. No almacena imágenes del rostro.</summary>
    public class PerfilBiometricoRepository
    {
        private readonly string rutaArchivo;
        private ProtectorDatosLocal? protector;
        private bool cifradoActivo;
        private readonly object candado = new object();
        private readonly JsonSerializerOptions opciones = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public PerfilBiometricoRepository(string rutaArchivo)
        {
            this.rutaArchivo = rutaArchivo;
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

                string json = LeerContenido(File.ReadAllText(rutaArchivo));
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<PerfilBiometrico>();
                }

                return JsonSerializer.Deserialize<List<PerfilBiometrico>>(json, opciones)
                       ?? new List<PerfilBiometrico>();
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

        private List<PerfilBiometrico> LeerTodosSinCandado()
        {
            if (!File.Exists(rutaArchivo))
            {
                return new List<PerfilBiometrico>();
            }

            string json = LeerContenido(File.ReadAllText(rutaArchivo));
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
            string json = JsonSerializer.Serialize(perfiles, opciones);
            File.WriteAllText(temporal, cifradoActivo ? protector!.Cifrar(json) : json);
            File.Move(temporal, rutaArchivo, true);
        }

        private string LeerContenido(string contenido)
        {
            if (!contenido.StartsWith("FDENC1:", StringComparison.Ordinal))
                return contenido;
            string rutaClave = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rutaArchivo))!, "clave_datos.bin");
            if (!File.Exists(rutaClave))
                throw new InvalidDataException("El perfil biométrico está cifrado, pero falta clave_datos.bin. " +
                    "Restaura ambos archivos desde la copia creada por la rama de protección.");
            try
            {
                protector ??= new ProtectorDatosLocal(rutaClave);
                cifradoActivo = true;
                return protector.Descifrar(contenido);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                throw new InvalidDataException("No se pudo abrir el perfil biométrico cifrado con la clave local de este usuario.", ex);
            }
        }
    }
}
