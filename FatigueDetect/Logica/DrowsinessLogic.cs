using System;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public class DrowsinessLogic
    {
        private readonly Configuracion config;

        private long eyesClosedSince = -1;
        private EyeState currentState = EyeState.DESPIERTO;

        public DrowsinessLogic(Configuracion? config = null)
        {
            this.config = config ?? new Configuracion();
        }

        public EyeState Evaluate(bool eyesDetected)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (eyesDetected)
            {
                eyesClosedSince = -1;
                currentState = EyeState.DESPIERTO;
            }
            else
            {
                if (eyesClosedSince == -1)
                {
                    eyesClosedSince = now;
                }

                long closed = now - eyesClosedSince;

                AjustesDeteccion ajustes = config.Deteccion;
                if (closed >= (long)(ajustes.SegundosAlarma * 1000))
                {
                    currentState = EyeState.ALARMA_ACTIVADA;
                }
                else if (closed >= (long)(ajustes.SegundosSomnoliento * 1000))
                {
                    currentState = EyeState.SOMNOLIENTO;
                }
                else
                {
                    currentState = EyeState.DESPIERTO;
                }
            }

            return currentState;
        }

        public EyeState EvaluateSinRostro()
        {
            // Si no hay rostro simplemente reseteamos todo.
            // No debe contar como ojos cerrados.
            eyesClosedSince = -1;
            currentState = EyeState.DESPIERTO;
            return currentState;
        }

        public bool ShouldActivateAlarm()
        {
            return currentState == EyeState.ALARMA_ACTIVADA;
        }

        public EyeState GetCurrentState()
        {
            return currentState;
        }

        public long GetClosedDurationMs()
        {
            if (eyesClosedSince == -1) return 0;
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - eyesClosedSince;
        }

        public void Reset()
        {
            eyesClosedSince = -1;
            currentState = EyeState.DESPIERTO;
        }
    }
}
