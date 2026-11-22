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
        CamaraCongelada,
        Cabeceo,
        CierreOcularAnomalo,
        EventoExterno,
        GiroIzquierda, GiroDerecha, CabezaArriba, CabezaAbajo,
        FueraZonaFrontal, PosiblePosturaEncorvada, RostroParcial, PosibleBostezo
    }

    public sealed record EventoSesion(TipoEventoSesion Tipo, DateTimeOffset Inicio, DateTimeOffset? Fin)
    {
        // Conserva códigos nuevos de otros detectores aunque esta versión aún no los conozca.
        public string? CodigoTipo { get; init; }
        public EvaluacionConfiabilidadEvento? Confiabilidad { get; init; }
        public long? DuracionMilisegundos => Fin.HasValue
            ? Math.Max(0, Fin.Value.ToUnixTimeMilliseconds() - Inicio.ToUnixTimeMilliseconds())
            : null;
    }
}
