using FatigueDetect.Entidades;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    /// <summary>Indicador heurístico de obstrucciones; no clasifica accesorios con certeza.</summary>
    public sealed class DetectorAccesorios
    {
        public EvaluacionAccesorios Evaluar(Mat imagen, Rect rostro, MedicionFacial? medicion)
        {
            Rect recorte = Limitar(rostro, imagen.Width, imagen.Height);
            if (recorte.Width < 80 || recorte.Height < 80)
                return EvaluacionAccesorios.SinRostro;
            using var gris = new Mat();
            using var cara = new Mat(imagen, recorte);
            Cv2.CvtColor(cara, gris, ColorConversionCodes.BGR2GRAY);
            (double media, double desviacion) = Estadistica(gris, 0, 0, 1, 1);
            (double ojosMedia, double ojosDesv) = Estadistica(gris, .08, .25, .84, .28);
            (double frenteMedia, double frenteDesv) = Estadistica(gris, .18, .02, .64, .20);
            (double bocaMedia, double bocaDesv) = Estadistica(gris, .16, .62, .68, .28);

            bool gafas = ojosMedia < media * .58 && ojosDesv < Math.Max(28, desviacion * .9);
            bool mascarilla = bocaDesv < 11 && desviacion > 17 && Math.Abs(bocaMedia - media) > 12;
            bool gorra = frenteMedia < media * .55 && frenteDesv < Math.Max(24, desviacion * .85);
            double ojos = medicion.HasValue ? 1 : .25;
            double boca = medicion?.AperturaBoca.HasValue == true ? 1 : .30;
            if (gafas) ojos *= .35;
            if (mascarilla) boca *= .25;
            if (gorra) ojos *= .85;
            double confianza = Math.Clamp(.65 * ojos + .25 * boca + .10 * (medicion?.Angulos.HasValue == true ? 1 : .2), 0, 1);
            bool confiable = confianza >= .60 && !gafas && medicion.HasValue;
            var causas = new List<string>();
            if (gafas) causas.Add("posible obstrucción oscura sobre los ojos");
            if (mascarilla) causas.Add("posible cobertura uniforme de la boca");
            if (gorra) causas.Add("posible obstrucción oscura en la frente");
            if (!medicion.HasValue) causas.Add("puntos faciales no disponibles");
            string explicacion = causas.Count == 0
                ? "Ojos, boca y postura facial visibles para la medición."
                : string.Join("; ", causas) + ". Verifica el encuadre y la iluminación.";
            return new(gafas, mascarilla, gorra, ojos, boca, confianza, confiable, explicacion);
        }

        private static (double Media, double Desviacion) Estadistica(Mat imagen,
            double x, double y, double ancho, double alto)
        {
            var region = new Rect((int)(imagen.Width * x), (int)(imagen.Height * y),
                Math.Max(1, (int)(imagen.Width * ancho)), Math.Max(1, (int)(imagen.Height * alto)));
            region = Limitar(region, imagen.Width, imagen.Height);
            using var roi = new Mat(imagen, region);
            Cv2.MeanStdDev(roi, out Scalar media, out Scalar desviacion);
            return (media.Val0, desviacion.Val0);
        }

        private static Rect Limitar(Rect r, int ancho, int alto)
        {
            int x = Math.Clamp(r.X, 0, Math.Max(0, ancho - 1));
            int y = Math.Clamp(r.Y, 0, Math.Max(0, alto - 1));
            return new Rect(x, y, Math.Max(1, Math.Min(r.Width, ancho - x)),
                Math.Max(1, Math.Min(r.Height, alto - y)));
        }
    }
}
