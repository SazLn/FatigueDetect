using System.Diagnostics;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    internal sealed class MedicionCalidadSesion
    {
        private readonly Stopwatch reloj = new();
        private long fotogramas;
        private long analizables;
        private long tiempoRostroMs;
        private long? ultimoRostroValido;
        private long fallos;
        private bool lecturaFallida;

        public void Reanudar() => reloj.Start();

        public void Interrumpir()
        {
            reloj.Stop();
            ultimoRostroValido = null;
            lecturaFallida = false;
        }

        public void RegistrarFallo()
        {
            if (!lecturaFallida)
                fallos++;

            lecturaFallida = true;
            ultimoRostroValido = null;
        }

        public void RegistrarFotograma(bool rostroValido, bool analizable)
        {
            fotogramas++;
            if (analizable)
                analizables++;

            lecturaFallida = false;
            long instante = reloj.ElapsedMilliseconds;
            if (rostroValido && ultimoRostroValido is long anterior)
                tiempoRostroMs += instante - anterior;

            ultimoRostroValido = rostroValido ? instante : null;
        }

        public CalidadSesion Resumen() =>
            new(fotogramas, analizables, reloj.ElapsedMilliseconds, tiempoRostroMs, fallos);
    }
}
