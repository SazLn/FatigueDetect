namespace FatigueDetect.Entidades
{
    public sealed record FormatoCamara(int Ancho = 0, int Alto = 0, int Fps = 0)
    {
        public static FormatoCamara Automatico { get; } = new();

        public bool EsValido() =>
            ((Ancho == 0 && Alto == 0) || (Ancho is >= 160 and <= 3840 && Alto is >= 120 and <= 2160))
            && (Fps == 0 || Fps is >= 5 and <= 60);
    }
}
