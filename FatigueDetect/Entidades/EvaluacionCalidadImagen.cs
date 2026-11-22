using System;
using System.Collections.Generic;

namespace FatigueDetect.Entidades
{
    /// <summary>
    /// Evaluación orientativa de un fotograma. Los puntajes van de 0 a 100:
    /// cámara/entorno describe la señal y colocación describe el encuadre del rostro.
    /// </summary>
    public sealed record EvaluacionCalidadImagen(double Brillo, double Contraste, double Nitidez,
        double? PosicionFacial)
    {
        public double PuntajeCamaraEntorno => Math.Clamp(Brillo * 0.35 + Contraste * 0.25 + Nitidez * 0.40, 0, 100);
        public double? PuntajeColocacion => PosicionFacial is double posicion ? Math.Clamp(posicion, 0, 100) : null;

        public IReadOnlyList<string> FactoresReduccion()
        {
            var factores = new List<string>();
            if (Brillo < 70) factores.Add("brillo fuera del rango recomendado");
            if (Contraste < 70) factores.Add("contraste insuficiente");
            if (Nitidez < 70) factores.Add("imagen poco nítida");
            if (PosicionFacial is < 70) factores.Add("rostro descentrado, muy cerca, muy lejos o junto al borde");
            return factores;
        }

        public string? Advertencia => PuntajeCamaraEntorno < 55
            ? "Calidad de cámara/entorno baja: revisa luz, enfoque y lente."
            : PuntajeColocacion is < 55
                ? "Colocación deficiente: centra el rostro y ajusta la distancia a la cámara."
                : null;
    }
}
