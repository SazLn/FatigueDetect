namespace FatigueDetect.Entidades
{
    public sealed record FactorConfiabilidadEvento(string Nombre, double Valor, double Peso);

    /// <summary>
    /// Explica la calidad de los datos que respaldan un evento. No representa una
    /// probabilidad médica de que el evento haya ocurrido.
    /// </summary>
    public sealed record EvaluacionConfiabilidadEvento(int Version, double? Puntaje,
        IReadOnlyList<FactorConfiabilidadEvento> Factores, IReadOnlyList<string> Advertencias)
    {
        public string Nivel => Puntaje switch
        {
            >= 80 => "Alta",
            >= 60 => "Media",
            >= 0 => "Baja",
            _ => "Sin medición"
        };
    }
}
