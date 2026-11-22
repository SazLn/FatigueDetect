using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FatigueDetect.Entidades;
using Microsoft.Data.Sqlite;

namespace FatigueDetect.Datos
{
    public sealed class PoliticaProteccionRepository(string ruta)
    {
        public PoliticaProteccionDatos Leer()
        {
            if (!File.Exists(ruta)) return PoliticaProteccionDatos.Predeterminada;
            return JsonSerializer.Deserialize<PoliticaProteccionDatos>(File.ReadAllText(ruta))
                ?? PoliticaProteccionDatos.Predeterminada;
        }

        public void Guardar(PoliticaProteccionDatos politica)
        {
            if (!politica.EsValida()) throw new InvalidDataException("La política de retención no es válida.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
            File.WriteAllText(ruta, JsonSerializer.Serialize(politica, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public sealed class ProteccionDatosService
    {
        private static readonly byte[] Cabecera = Encoding.ASCII.GetBytes("FDBACKUP1");
        private static readonly string[] Archivos = ["historial.sqlite", "perfiles_biometricos.json",
            "tipos_carro.txt", "camara_preferida.txt", "configuracion.json", "politica_datos.json"];
        private readonly string carpeta;
        private readonly ProtectorDatosLocal protector;
        private readonly HistorialSqliteRepository historial;
        private readonly PerfilBiometricoRepository perfiles;

        public ProteccionDatosService(string carpeta, ProtectorDatosLocal protector,
            HistorialSqliteRepository historial, PerfilBiometricoRepository perfiles)
        {
            this.carpeta = Path.GetFullPath(carpeta);
            this.protector = protector;
            this.historial = historial;
            this.perfiles = perfiles;
        }

        public (int Sesiones, int Perfiles) AplicarRetencion(PoliticaProteccionDatos politica, DateTime ahora)
        {
            if (!politica.EliminacionAutomatica) return (0, 0);
            int sesiones = historial.EliminarAnteriores(ahora.AddDays(-politica.DiasHistorial));
            int biometricos = perfiles.EliminarAnteriores(ahora.ToUniversalTime().AddDays(-politica.DiasPerfilesBiometricos));
            return (sesiones, biometricos);
        }

        public void CrearRespaldo(string ruta, string contrasena)
        {
            ValidarContrasena(contrasena);
            byte[] zip;
            using (var memoria = new MemoryStream())
            {
                using (var archivo = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (string nombre in Archivos)
                    {
                        string origen = Path.Combine(carpeta, nombre);
                        if (File.Exists(origen)) archivo.CreateEntryFromFile(origen, nombre, CompressionLevel.Optimal);
                    }
                    ZipArchiveEntry clave = archivo.CreateEntry("clave_portable.bin");
                    using Stream destino = clave.Open();
                    destino.Write(protector.ExportarClaveParaRespaldo());
                }
                zip = memoria.ToArray();
            }
            byte[] salt = RandomNumberGenerator.GetBytes(16), nonce = RandomNumberGenerator.GetBytes(12);
            byte[] claveRespaldo = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, 200_000,
                HashAlgorithmName.SHA256, 32);
            byte[] cifrado = new byte[zip.Length], etiqueta = new byte[16];
            using (var aes = new AesGcm(claveRespaldo, 16)) aes.Encrypt(nonce, zip, cifrado, etiqueta, Cabecera);
            CryptographicOperations.ZeroMemory(claveRespaldo);
            using var salida = File.Create(ruta);
            salida.Write(Cabecera); salida.Write(salt); salida.Write(nonce); salida.Write(etiqueta); salida.Write(cifrado);
        }

        public void RestaurarRespaldo(string ruta, string contrasena)
        {
            ValidarContrasena(contrasena);
            byte[] paquete = File.ReadAllBytes(ruta);
            int minimo = Cabecera.Length + 16 + 12 + 16;
            if (paquete.Length <= minimo || !paquete.AsSpan(0, Cabecera.Length).SequenceEqual(Cabecera))
                throw new InvalidDataException("El archivo no es un respaldo FatigueDetect válido.");
            int p = Cabecera.Length;
            ReadOnlySpan<byte> salt = paquete.AsSpan(p, 16); p += 16;
            ReadOnlySpan<byte> nonce = paquete.AsSpan(p, 12); p += 12;
            ReadOnlySpan<byte> etiqueta = paquete.AsSpan(p, 16); p += 16;
            byte[] plano = new byte[paquete.Length - p];
            byte[] claveRespaldo = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, 200_000,
                HashAlgorithmName.SHA256, 32);
            try
            {
                using var aes = new AesGcm(claveRespaldo, 16);
                aes.Decrypt(nonce, paquete.AsSpan(p), etiqueta, plano, Cabecera);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidDataException("Contraseña incorrecta o respaldo alterado.", ex);
            }
            finally { CryptographicOperations.ZeroMemory(claveRespaldo); }

            string temporal = Path.Combine(Path.GetTempPath(), "fatiguedetect-restore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporal);
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(plano), ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entrada in zip.Entries)
                    {
                        if (entrada.Name != entrada.FullName ||
                            (!Archivos.Contains(entrada.Name) && entrada.Name != "clave_portable.bin"))
                            throw new InvalidDataException("El respaldo contiene una entrada no permitida.");
                        entrada.ExtractToFile(Path.Combine(temporal, entrada.Name), true);
                    }
                }
                string clave = Path.Combine(temporal, "clave_portable.bin");
                if (!File.Exists(clave)) throw new InvalidDataException("El respaldo no contiene su clave de datos.");
                ValidarSqlite(Path.Combine(temporal, "historial.sqlite"));
                string rollback = Path.Combine(temporal, "rollback");
                Directory.CreateDirectory(rollback);
                foreach (string nombre in Archivos.Append(Path.GetFileName(protector.RutaClave)))
                {
                    string actual = Path.Combine(carpeta, nombre);
                    if (File.Exists(actual)) File.Copy(actual, Path.Combine(rollback, nombre), true);
                }
                try
                {
                    protector.ImportarClaveDesdeRespaldo(File.ReadAllBytes(clave));
                    Directory.CreateDirectory(carpeta);
                    foreach (string nombre in Archivos)
                    {
                        string origen = Path.Combine(temporal, nombre);
                        if (File.Exists(origen)) File.Copy(origen, Path.Combine(carpeta, nombre), true);
                    }
                }
                catch
                {
                    foreach (string nombre in Archivos.Append(Path.GetFileName(protector.RutaClave)))
                    {
                        string anterior = Path.Combine(rollback, nombre);
                        if (File.Exists(anterior)) File.Copy(anterior, Path.Combine(carpeta, nombre), true);
                    }
                    throw;
                }
            }
            finally { Directory.Delete(temporal, true); }
        }

        private static void ValidarSqlite(string ruta)
        {
            if (!File.Exists(ruta)) return;
            using var conexion = new SqliteConnection($"Data Source={ruta};Mode=ReadOnly");
            conexion.Open();
            using SqliteCommand comando = conexion.CreateCommand();
            comando.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(comando.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("La base de datos del respaldo no superó la comprobación de integridad.");
        }

        private static void ValidarContrasena(string contrasena)
        {
            if (contrasena.Length < 10) throw new ArgumentException("Usa una contraseña de al menos 10 caracteres.");
        }
    }
}
