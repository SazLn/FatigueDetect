using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public sealed class CalibradorGuiado(string? camaraId, string? vehiculoClave)
    {
        private const int MuestrasMinimas = 15;
        private readonly List<MedicionFacial> muestras = new();
        private DateTimeOffset? inicio;
        private (double Izquierda, double Derecha)? referencia;
        private bool ojosCerrados;

        public CalibracionGuiada? Resultado { get; private set; }

        public EstadoCalibracion Observar(DateTimeOffset instante, MedicionFacial? medicion)
        {
            if (Resultado is not null)
                return new("Calibración terminada. Puedes iniciar el monitoreo.", 1,
                    Resultado.MuestrasValidas, Resultado.ParpadeosConfirmados, true);

            if (medicion is not { Angulos: { } angulos } cara ||
                (!referencia.HasValue && (cara.EarIzquierdo < 0.15 || cara.EarDerecho < 0.15)) ||
                Math.Abs(angulos.Horizontal) > 25 || Math.Abs(angulos.Lateral) > 20)
            {
                inicio = null;
                if (!referencia.HasValue)
                    muestras.Clear();
                return new("Mira al frente, mantén ambos ojos abiertos y mejora la iluminación.",
                    referencia.HasValue ? 0.55 : 0, muestras.Count, 0, false);
            }

            if (!referencia.HasValue)
            {
                inicio ??= instante;
                muestras.Add(cara);
                double progresoTiempo = Math.Clamp((instante - inicio.Value).TotalSeconds / 2.0, 0, 1);
                double progreso = Math.Min(progresoTiempo, muestras.Count / (double)MuestrasMinimas);
                if (muestras.Count >= MuestrasMinimas && progresoTiempo >= 1)
                {
                    referencia = (Percentil(muestras.Select(m => m.EarIzquierdo), .75),
                        Percentil(muestras.Select(m => m.EarDerecho), .75));
                    return new("Referencia frontal lista. Parpadea dos veces de forma natural.", .55,
                        muestras.Count, 0, false);
                }
                return new("Paso 1 de 2: mira al frente y mantén los ojos abiertos.",
                    progreso * .55, muestras.Count, 0, false);
            }

            (double izquierda, double derecha) = referencia.Value;
            bool cerrados = cara.EarIzquierdo <= izquierda * .55 && cara.EarDerecho <= derecha * .55;
            bool abiertos = cara.EarIzquierdo >= izquierda * .80 && cara.EarDerecho >= derecha * .80;
            int parpadeos = Resultado?.ParpadeosConfirmados ?? parpadeosTemporales;
            if (cerrados)
                ojosCerrados = true;
            else if (ojosCerrados && abiertos)
            {
                ojosCerrados = false;
                parpadeosTemporales++;
                parpadeos = parpadeosTemporales;
            }

            if (parpadeos >= 2)
            {
                Resultado = new(1, instante, Normalizar(camaraId, "AUTOMATICA"),
                    Normalizar(vehiculoClave, "SIN-VEHICULO"), izquierda, derecha,
                    Mediana(muestras.Select(m => m.Angulos!.Value)), muestras.Count, parpadeos);
                return new("Calibración terminada. Puedes iniciar el monitoreo.", 1,
                    muestras.Count, parpadeos, true);
            }

            return new($"Paso 2 de 2: parpadea dos veces ({parpadeos}/2).",
                .55 + parpadeos * .225, muestras.Count, parpadeos, false);
        }

        private int parpadeosTemporales;

        private static double Percentil(IEnumerable<double> valores, double percentil)
        {
            double[] ordenados = valores.Order().ToArray();
            int indice = (int)Math.Round((ordenados.Length - 1) * percentil);
            return ordenados[Math.Clamp(indice, 0, ordenados.Length - 1)];
        }

        private static AngulosCabeza Mediana(IEnumerable<AngulosCabeza> valores)
        {
            AngulosCabeza[] lista = valores.ToArray();
            return new(Mediana(lista.Select(v => v.Vertical)), Mediana(lista.Select(v => v.Horizontal)),
                Mediana(lista.Select(v => v.Lateral)));
        }

        private static double Mediana(IEnumerable<double> valores)
        {
            double[] ordenados = valores.Order().ToArray();
            return ordenados[ordenados.Length / 2];
        }

        private static string Normalizar(string? valor, string respaldo) =>
            string.IsNullOrWhiteSpace(valor) ? respaldo : valor.Trim().ToUpperInvariant();
    }
}
