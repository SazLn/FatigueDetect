using System.Security.Cryptography;
using System.Text;

namespace FatigueDetect.Datos
{
    /// <summary>Compatibilidad de lectura con perfiles cifrados por la rama de protección.</summary>
    public sealed class ProtectorDatosLocal
    {
        private const string Prefijo = "FDENC1:";
        private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("FatigueDetect-datos-v1");
        private readonly byte[] clave;

        public ProtectorDatosLocal(string rutaClave)
        {
            RutaClave = Path.GetFullPath(rutaClave);
            clave = CargarOCrear();
        }

        public string RutaClave { get; }

        public string Cifrar(string? texto)
        {
            if (string.IsNullOrEmpty(texto) || texto.StartsWith(Prefijo, StringComparison.Ordinal))
                return texto ?? string.Empty;
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] plano = Encoding.UTF8.GetBytes(texto);
            byte[] cifrado = new byte[plano.Length];
            byte[] etiqueta = new byte[16];
            using var aes = new AesGcm(clave, etiqueta.Length);
            aes.Encrypt(nonce, plano, cifrado, etiqueta);
            byte[] paquete = new byte[nonce.Length + etiqueta.Length + cifrado.Length];
            Buffer.BlockCopy(nonce, 0, paquete, 0, nonce.Length);
            Buffer.BlockCopy(etiqueta, 0, paquete, nonce.Length, etiqueta.Length);
            Buffer.BlockCopy(cifrado, 0, paquete, nonce.Length + etiqueta.Length, cifrado.Length);
            return Prefijo + Convert.ToBase64String(paquete);
        }

        public string Descifrar(string? texto)
        {
            if (string.IsNullOrEmpty(texto) || !EstaCifrado(texto))
                return texto ?? string.Empty;
            byte[] paquete = Convert.FromBase64String(texto[Prefijo.Length..]);
            if (paquete.Length < 28)
                throw new InvalidDataException("El perfil biométrico cifrado está incompleto.");
            byte[] plano = new byte[paquete.Length - 28];
            using var aes = new AesGcm(clave, 16);
            aes.Decrypt(paquete.AsSpan(0, 12), paquete.AsSpan(28), paquete.AsSpan(12, 16), plano);
            return Encoding.UTF8.GetString(plano);
        }

        public bool EstaCifrado(string? texto) => texto?.StartsWith(Prefijo, StringComparison.Ordinal) == true;

        private byte[] CargarOCrear()
        {
            if (File.Exists(RutaClave))
                return DesprotegerClave(File.ReadAllBytes(RutaClave));
            Directory.CreateDirectory(Path.GetDirectoryName(RutaClave)!);
            byte[] nueva = RandomNumberGenerator.GetBytes(32);
            string temporal = RutaClave + ".tmp";
            File.WriteAllBytes(temporal, ProtegerClave(nueva));
            File.Move(temporal, RutaClave, true);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(RutaClave, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return nueva;
        }

        private static byte[] ProtegerClave(byte[] valor) => OperatingSystem.IsWindows()
            ? ProtectedData.Protect(valor, Entropia, DataProtectionScope.CurrentUser)
            : [.. Encoding.ASCII.GetBytes("FDRAW1"), .. valor];

        private static byte[] DesprotegerClave(byte[] valor)
        {
            byte[] cabecera = Encoding.ASCII.GetBytes("FDRAW1");
            if (valor.AsSpan().StartsWith(cabecera))
                return valor[cabecera.Length..];
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("La clave fue protegida por Windows y solo puede abrirse con el mismo usuario.");
            return ProtectedData.Unprotect(valor, Entropia, DataProtectionScope.CurrentUser);
        }
    }
}
