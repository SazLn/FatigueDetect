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

        public (EstadoIluminacion? Iluminacion, TipoEventoSesion? Rostro) Observar(
            DateTimeOffset instante, int rostros,
            EyeState estado, EstadoIluminacion iluminacion, bool analisisDisponible = true)
        {
            Actualizar(TipoEventoSesion.Alarma, analisisDisponible && estado == EyeState.ALARMA_ACTIVADA, instante, true);
            bool rostroAusente = Actualizar(TipoEventoSesion.RostroNoDetectado, analisisDisponible && rostros == 0, instante);
            bool variosRostros = Actualizar(TipoEventoSesion.VariosRostros, analisisDisponible && rostros > 1, instante);
            bool pocaLuz = Actualizar(TipoEventoSesion.LuzInsuficiente, analisisDisponible && iluminacion == EstadoIluminacion.Insuficiente, instante);
            bool muchaLuz = Actualizar(TipoEventoSesion.LuzExcesiva, analisisDisponible && iluminacion == EstadoIluminacion.Excesiva, instante);

            TipoEventoSesion? avisoRostro = rostroAusente ? TipoEventoSesion.RostroNoDetectado
                : variosRostros ? TipoEventoSesion.VariosRostros : null;
            return (pocaLuz || muchaLuz ? iluminacion : null, avisoRostro);
        }

        public void ObservarCamara(DateTimeOffset instante, TipoEventoSesion? indicio, TipoEventoSesion? confirmado)
        {
            Actualizar(TipoEventoSesion.CamaraTapada, indicio == TipoEventoSesion.CamaraTapada,
                instante, confirmado == TipoEventoSesion.CamaraTapada);
            Actualizar(TipoEventoSesion.CamaraDesenfocada, indicio == TipoEventoSesion.CamaraDesenfocada,
                instante, confirmado == TipoEventoSesion.CamaraDesenfocada);
            Actualizar(TipoEventoSesion.CamaraCongelada, indicio == TipoEventoSesion.CamaraCongelada,
                instante, confirmado == TipoEventoSesion.CamaraCongelada);
        }

        public void Interrumpir(DateTimeOffset instante)
        {
            foreach ((TipoEventoSesion tipo, Intervalo intervalo) in activos)
                Guardar(tipo, intervalo, instante);

            activos.Clear();
        }

        private bool Actualizar(TipoEventoSesion tipo, bool presente, DateTimeOffset instante, bool? confirmado = null)
        {
            if (!presente)
            {
                if (activos.Remove(tipo, out Intervalo? anterior))
                    Guardar(tipo, anterior, instante);
                return false;
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
            return intervalo.Confirmado;
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
