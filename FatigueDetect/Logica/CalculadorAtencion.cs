using System;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public static class CalculadorAtencion
    {
        /// <summary>
        /// BAJO: la alarma está sonando. MEDIO: somnolencia, o hubo alarmas en los últimos 10 minutos.
        /// ALTO: despierto y sin alarmas recientes.
        /// </summary>
        public static NivelAtencion Calcular(EyeState estado, SesionConduccion sesion)
        {
            if (estado == EyeState.ALARMA_ACTIVADA)
            {
                return NivelAtencion.BAJO;
            }

            if (estado == EyeState.SOMNOLIENTO)
            {
                return NivelAtencion.MEDIO;
            }

            if (sesion.AlarmasDesde(DateTime.Now.AddMinutes(-10)) > 0)
            {
                return NivelAtencion.MEDIO;
            }

            return NivelAtencion.ALTO;
        }
    }
}
