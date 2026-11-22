using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed class DrowsinessLogic(Configuracion config, UmbralesPersonalizados? personalizados = null)
    {
        public double UmbralCierreProlongado => personalizados?.SegundosSomnoliento
            ?? config.Deteccion.SegundosSomnoliento;

        public double UmbralAlarma => personalizados?.SegundosAlarma
            ?? config.Deteccion.SegundosAlarma;

        public EyeState Evaluar(long cierreMs)
        {
            if (cierreMs >= UmbralAlarma * 1000)
                return EyeState.ALARMA_ACTIVADA;
            if (cierreMs >= UmbralCierreProlongado * 1000)
                return EyeState.SOMNOLIENTO;
            return EyeState.DESPIERTO;
        }
    }
}
