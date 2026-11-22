namespace FatigueDetect.Entidades
{
    public sealed record PoliticaProteccionDatos(bool EliminacionAutomatica,
        int DiasHistorial, int DiasPerfilesBiometricos)
    {
        public static PoliticaProteccionDatos Predeterminada { get; } = new(false, 365, 365);
        public bool EsValida() => DiasHistorial is >= 30 and <= 3650 &&
            DiasPerfilesBiometricos is >= 30 and <= 3650;
    }
}
