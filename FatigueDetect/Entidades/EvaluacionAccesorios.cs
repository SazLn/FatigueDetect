namespace FatigueDetect.Entidades
{
    public sealed record EvaluacionAccesorios(bool PosiblesGafasOscuras, bool PosibleMascarilla,
        bool PosibleGorra, double VisibilidadOjos, double VisibilidadBoca,
        double ConfiabilidadMedicion, bool Confiable, string Explicacion)
    {
        public static EvaluacionAccesorios SinRostro { get; } = new(false, false, false,
            0, 0, 0, false, "Rostro no disponible para evaluar accesorios.");
    }
}
