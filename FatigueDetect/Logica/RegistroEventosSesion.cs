using System;
using System.Collections.Generic;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    internal sealed class RegistroEventosSesion
    {
        private static readonly TimeSpan UmbralEntorno = TimeSpan.FromSeconds(2);
        private readonly SesionConduccion sesion;
        private readonly Dictionary<TipoEventoSesion, Intervalo> activos = new();

        public RegistroEventosSesion(SesionConduccion sesion)
        {
            this.sesion = sesion;
        }

        public void Observar(DateTimeOffset instante, int rostros, bool ojosDetectados, EyeState estado, double brillo)
        {
            bool cierre = rostros > 0 && !ojosDetectados;
            Actualizar(TipoEventoSesion.CierreOcularProlongado, cierre, instante,
                estado is EyeState.SOMNOLIENTO or EyeState.ALARMA_ACTIVADA);
            Actualizar(TipoEventoSesion.Alarma, estado == EyeState.ALARMA_ACTIVADA, instante, true);
            Actualizar(TipoEventoSesion.RostroNoDetectado, rostros == 0, instante);
            Actualizar(TipoEventoSesion.VariosRostros, rostros > 1, instante);
            Actualizar(TipoEventoSesion.LuzInsuficiente, brillo < 38, instante);
            Actualizar(TipoEventoSesion.LuzExcesiva, brillo > 220, instante);
        }

        public void Interrumpir(DateTimeOffset instante)
        {
            foreach ((TipoEventoSesion tipo, Intervalo intervalo) in activos)
                Guardar(tipo, intervalo, instante);

            activos.Clear();
        }

        private void Actualizar(TipoEventoSesion tipo, bool presente, DateTimeOffset instante, bool? confirmado = null)
        {
            if (!presente)
            {
                if (activos.Remove(tipo, out Intervalo? anterior))
                    Guardar(tipo, anterior, instante);
                return;
            }

            if (!activos.TryGetValue(tipo, out Intervalo? intervalo))
            {
                intervalo = new Intervalo(instante);
                activos.Add(tipo, intervalo);
            }

            if (!intervalo.Confirmado && (confirmado ?? (instante - intervalo.Inicio >= UmbralEntorno)))
            {
                intervalo.Confirmado = true;
                if (tipo == TipoEventoSesion.Alarma)
                    sesion.RegistrarAlarma(intervalo.Inicio);
            }
        }

        private void Guardar(TipoEventoSesion tipo, Intervalo intervalo, DateTimeOffset fin)
        {
            if (intervalo.Confirmado)
                sesion.RegistrarEvento(new EventoSesion(tipo, intervalo.Inicio, fin < intervalo.Inicio ? intervalo.Inicio : fin));
        }

        private sealed class Intervalo(DateTimeOffset inicio)
        {
            public DateTimeOffset Inicio { get; } = inicio;
            public bool Confirmado { get; set; }
        }
    }
}
