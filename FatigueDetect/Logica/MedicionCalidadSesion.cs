using System.Diagnostics;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    internal sealed class MedicionCalidadSesion
    {
        private const long VentanaMs = 2000;
        private readonly Stopwatch reloj = new();
        private long fotogramas;
        private long analizables;
        private long tiempoRostroMs;
        private long? ultimoRostroValido;
        private long fallos;
        private bool lecturaFallida;
        private long inicioVentana;
        private long fotogramasVentana;
        private long tiempoEvaluado;
        private long tiempoFpsBajo;
        private long caidas;
        private double brilloTotal;
        private double contrasteTotal;
        private double nitidezTotal;
        private double posicionTotal;
        private long muestrasImagen;
        private long muestrasPosicion;
        public double? FpsRecientes { get; private set; }
        public bool FpsBajos => FpsRecientes < CalidadSesion.UmbralFps;

        public void Reanudar() => reloj.Start();

        public void Interrumpir()
        {
            reloj.Stop();
            ultimoRostroValido = null;
            lecturaFallida = false;
            inicioVentana = reloj.ElapsedMilliseconds;
            fotogramasVentana = 0;
            FpsRecientes = null;
        }

        public void RegistrarFallo()
        {
            if (!lecturaFallida)
                fallos++;

            lecturaFallida = true;
            ultimoRostroValido = null;
            EvaluarVentana(reloj.ElapsedMilliseconds);
        }

        public void RegistrarFotograma(bool rostroValido, bool analizable,
            EvaluacionCalidadImagen? evaluacion = null)
        {
            long instante = reloj.ElapsedMilliseconds;
            EvaluarVentana(instante);
            fotogramas++;
            fotogramasVentana++;
            if (analizable)
                analizables++;
            if (evaluacion is { } imagen)
            {
                brilloTotal += imagen.Brillo;
                contrasteTotal += imagen.Contraste;
                nitidezTotal += imagen.Nitidez;
                muestrasImagen++;
                if (imagen.PosicionFacial is double posicion)
                {
                    posicionTotal += posicion;
                    muestrasPosicion++;
                }
            }

            lecturaFallida = false;
            if (rostroValido && ultimoRostroValido is long anterior && instante - anterior <= 250)
                tiempoRostroMs += instante - anterior;

            ultimoRostroValido = rostroValido ? instante : null;
        }

        private void EvaluarVentana(long instante)
        {
            long intervalo = instante - inicioVentana;
            if (intervalo < VentanaMs)
                return;

            bool anteriorBajo = FpsBajos;
            FpsRecientes = fotogramasVentana * 1000.0 / intervalo;
            tiempoEvaluado += intervalo;
            if (FpsBajos)
            {
                tiempoFpsBajo += intervalo;
                if (!anteriorBajo)
                    caidas++;
            }

            inicioVentana = instante;
            fotogramasVentana = 0;
        }

        public CalidadSesion Resumen() => new(fotogramas, analizables, reloj.ElapsedMilliseconds,
            tiempoRostroMs, fallos, tiempoEvaluado > 0 ? caidas : null,
            tiempoEvaluado > 0 ? tiempoFpsBajo : null, tiempoEvaluado > 0 ? tiempoEvaluado : null,
            muestrasImagen > 0 ? brilloTotal / muestrasImagen : null,
            muestrasImagen > 0 ? contrasteTotal / muestrasImagen : null,
            muestrasImagen > 0 ? nitidezTotal / muestrasImagen : null,
            muestrasPosicion > 0 ? posicionTotal / muestrasPosicion : null,
            muestrasImagen, muestrasPosicion);
    }
}
