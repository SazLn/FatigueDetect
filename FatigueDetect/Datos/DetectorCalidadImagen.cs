using System;
using FatigueDetect.Entidades;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public sealed class DetectorCalidadImagen : IDisposable
    {
        private readonly Mat gris = new();
        private readonly Mat muestra = new();
        private readonly Mat anterior = new();
        private readonly Mat laplaciano = new();
        private DateTimeOffset? inicioImagenIgual;
        private DateTimeOffset inicioDefecto;
        private TipoEventoSesion? candidato;

        public TipoEventoSesion? Observar(Mat imagen, DateTimeOffset instante, out TipoEventoSesion? indicio)
        {
            if (imagen.Channels() == 1)
                imagen.CopyTo(gris);
            else
                Cv2.CvtColor(imagen, gris, ColorConversionCodes.BGR2GRAY);
            Cv2.Resize(gris, muestra, new Size(160, 90), interpolation: InterpolationFlags.Area);

            Cv2.MeanStdDev(muestra, out Scalar media, out Scalar contraste);
            bool tapada = media.Val0 < 22 && contraste.Val0 < 6;
            bool repetida = inicioImagenIgual.HasValue && Cv2.Norm(muestra, anterior, NormTypes.INF) == 0;
            if (!repetida)
                inicioImagenIgual = instante;
            muestra.CopyTo(anterior);

            Cv2.Laplacian(muestra, laplaciano, MatType.CV_32F);
            Cv2.MeanStdDev(laplaciano, out _, out Scalar bordes);
            bool desenfocada = media.Val0 is >= 38 and <= 220 && bordes.Val0 * bordes.Val0 < 12;
            bool congelada = inicioImagenIgual is DateTimeOffset inicio
                && instante - inicio >= TimeSpan.FromSeconds(3);

            indicio = tapada ? TipoEventoSesion.CamaraTapada
                : congelada ? TipoEventoSesion.CamaraCongelada
                : desenfocada ? TipoEventoSesion.CamaraDesenfocada : null;

            if (candidato != indicio)
            {
                candidato = indicio;
                inicioDefecto = instante;
            }

            if (indicio == TipoEventoSesion.CamaraCongelada || instante - inicioDefecto >= TimeSpan.FromSeconds(2))
                return indicio;

            return null;
        }

        public void Reiniciar()
        {
            candidato = null;
            inicioImagenIgual = null;
        }

        public void Dispose()
        {
            gris.Dispose();
            muestra.Dispose();
            anterior.Dispose();
            laplaciano.Dispose();
        }
    }
}
