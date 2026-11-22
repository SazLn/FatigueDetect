namespace FatigueDetect.Entidades
{
    public enum NivelAlertaProgresiva
    {
        Normal,
        Preventiva,
        Alta,
        Critica
    }

    public sealed record EstadoAlertaProgresiva(NivelAlertaProgresiva Nivel,
        bool EmitirSonido, bool RecuperacionPendiente, long? RecuperacionRestanteMs,
        string Explicacion);
}
