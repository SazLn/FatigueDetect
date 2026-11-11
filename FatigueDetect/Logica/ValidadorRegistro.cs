using System.Text.RegularExpressions;

namespace FatigueDetect.Logica
{
    public static class ValidadorRegistro
    {
        public static readonly string[] TiposDeCarro =
        {
            "Automóvil", "Camioneta", "Campero / SUV", "Taxi", "Bus", "Camión"
        };

        private static readonly Regex PatronCedula = new Regex("^[0-9]{6,10}$");

        /// <summary>Devuelve null si todo está bien, o el mensaje del primer error.</summary>
        public static string? Validar(string cedula, string nombre, string apellidos,
                                      string tipoCarro, string placa)
        {
            if (!PatronCedula.IsMatch((cedula ?? "").Trim()))
                return "La cédula debe tener entre 6 y 10 dígitos (solo números).";

            if (string.IsNullOrWhiteSpace(nombre))
                return "Escribe el nombre del conductor.";

            if (string.IsNullOrWhiteSpace(apellidos))
                return "Escribe los apellidos del conductor.";

            if (string.IsNullOrWhiteSpace(tipoCarro))
                return "Selecciona o escribe el tipo de carro.";

            // La placa puede tener cualquier formato; solo es obligatoria.
            if (string.IsNullOrWhiteSpace(placa))
                return "Escribe la placa del vehículo.";

            return null;
        }

        public static string NormalizarPlaca(string placa)
        {
            return (placa ?? "").Trim().ToUpperInvariant();
        }
    }
}
