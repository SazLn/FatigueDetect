using System.Text.Json.Serialization;

namespace FatigueDetect.Entidades
{
    public sealed record AjustesDeteccion(
        [property: JsonRequired] double SegundosSomnoliento,
        [property: JsonRequired] double SegundosAlarma,
        [property: JsonRequired] bool SonidoActivado)
    {
        public const int MaximoSomnolencia = 5;
        public const double MinimoSomnolencia = 0.5;
        public const int MinimoAlarma = 1;
        public const int MaximoAlarma = 10;

        public static AjustesDeteccion Predeterminados { get; } = new(1.5, 3.0, true);

        public bool EsValido() =>
            SegundosSomnoliento is >= MinimoSomnolencia and <= MaximoSomnolencia &&
            SegundosAlarma is >= MinimoAlarma and <= MaximoAlarma &&
            SegundosAlarma > SegundosSomnoliento;
    }
}
