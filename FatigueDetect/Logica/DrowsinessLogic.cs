using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed class DrowsinessLogic(Configuracion config)
    {
        public double UmbralCierreProlongado => config.Deteccion.SegundosSomnoliento;

        public EyeState Evaluar(long cierreMs)
        {
            AjustesDeteccion ajustes = config.Deteccion;
            if (cierreMs >= ajustes.SegundosAlarma * 1000)
                return EyeState.ALARMA_ACTIVADA;
            if (cierreMs >= ajustes.SegundosSomnoliento * 1000)
                return EyeState.SOMNOLIENTO;
            return EyeState.DESPIERTO;
        }
    }
}
