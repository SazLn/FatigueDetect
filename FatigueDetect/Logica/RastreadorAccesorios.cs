using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    internal sealed class RastreadorAccesorios(SesionConduccion sesion)
    {
        private static readonly TimeSpan Confirmacion = TimeSpan.FromSeconds(2);
        private readonly Dictionary<TipoEventoSesion, Intervalo> activos = new();

        public EvaluacionAccesorios Observar(DateTimeOffset instante, EvaluacionAccesorios evaluacion)
        {
            bool gafas = Actualizar(TipoEventoSesion.PosiblesGafasOscuras,
                evaluacion.PosiblesGafasOscuras, instante);
            bool mascarilla = Actualizar(TipoEventoSesion.PosibleMascarilla,
                evaluacion.PosibleMascarilla, instante);
            bool gorra = Actualizar(TipoEventoSesion.PosibleGorra,
                evaluacion.PosibleGorra, instante);
            bool noConfiable = Actualizar(TipoEventoSesion.MedicionFacialNoConfiable,
                !evaluacion.Confiable, instante);
            return evaluacion with
            {
                PosiblesGafasOscuras = gafas,
                PosibleMascarilla = mascarilla,
                PosibleGorra = gorra,
                Confiable = !noConfiable && evaluacion.Confiable
            };
        }

        public void Interrumpir(DateTimeOffset instante)
        {
            foreach ((TipoEventoSesion tipo, Intervalo intervalo) in activos)
                Guardar(tipo, intervalo, instante);
            activos.Clear();
        }

        private bool Actualizar(TipoEventoSesion tipo, bool presente, DateTimeOffset instante)
        {
            if (!presente)
            {
                if (activos.Remove(tipo, out Intervalo? anterior)) Guardar(tipo, anterior, instante);
                return false;
            }
            if (!activos.TryGetValue(tipo, out Intervalo? intervalo))
                activos[tipo] = intervalo = new Intervalo(instante);
            if (!intervalo.Confirmado && instante - intervalo.Inicio >= Confirmacion)
                intervalo.Confirmado = true;
            return intervalo.Confirmado;
        }

        private void Guardar(TipoEventoSesion tipo, Intervalo intervalo, DateTimeOffset fin)
        {
            if (intervalo.Confirmado)
                sesion.RegistrarEvento(new EventoSesion(tipo, intervalo.Inicio,
                    fin < intervalo.Inicio ? intervalo.Inicio : fin));
        }

        private sealed class Intervalo(DateTimeOffset inicio)
        { public DateTimeOffset Inicio { get; } = inicio; public bool Confirmado { get; set; } }
    }
}
