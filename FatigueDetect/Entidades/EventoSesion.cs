using System;

namespace FatigueDetect.Entidades
{
    public enum TipoEventoSesion
    {
        CierreOcularProlongado,
        Alarma,
        RostroNoDetectado,
        VariosRostros,
        LuzInsuficiente,
        LuzExcesiva,
        CamaraTapada,
        CamaraDesenfocada,
        CamaraCongelada
    }

    public sealed record EventoSesion(TipoEventoSesion Tipo, DateTimeOffset Inicio, DateTimeOffset? Fin)
    {
        public long? DuracionMilisegundos => Fin.HasValue
            ? Math.Max(0, Fin.Value.ToUnixTimeMilliseconds() - Inicio.ToUnixTimeMilliseconds())
            : null;
    }
}
