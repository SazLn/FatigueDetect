using System.Runtime.InteropServices;
using FatigueDetect.Entidades;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace FatigueDetect.Datos
{
    public sealed class DetectorPuntosFaciales : IDisposable
    {
        private const int Tamano = 192;
        private static readonly string[] Salidas = ["conv2d_21", "conv2d_31"];
        private static readonly long[] Dimension = [1, Tamano, Tamano, 3];
        private static readonly int[] OjoIzquierdo = [362, 385, 387, 263, 373, 380];
        private static readonly int[] OjoDerecho = [33, 160, 158, 133, 153, 144];
        private static readonly int[] PuntosPose = [1, 152, 33, 263, 61, 291];
        // Vértices del rostro canónico de MediaPipe en ejes de OpenCV: +X derecha, +Y abajo, +Z fondo.
        private static readonly Point3f[] RostroCanonico =
        [
            new(0, 1.126865f, -7.475604f), new(0, 9.403378f, -4.264492f),
            new(-4.445859f, -2.663991f, -3.173422f), new(4.445859f, -2.663991f, -3.173422f),
            new(-2.456206f, 4.342621f, -4.283884f), new(2.456206f, 4.342621f, -4.283884f)
        ];

        private readonly InferenceSession sesion;
        private readonly RunOptions opcionesEjecucion;
        private readonly Mat escalada;
        private readonly byte[] bgr = new byte[Tamano * Tamano * 3];
        private readonly float[] rgb = new float[Tamano * Tamano * 3];

        public DetectorPuntosFaciales(string ruta)
        {
            if (!File.Exists(ruta))
                throw new FileNotFoundException("Falta el modelo facial en Resources/modelos/face-landmark.onnx.", ruta);

            using var opciones = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1 };
            sesion = new InferenceSession(ruta, opciones);
            opcionesEjecucion = new RunOptions();
            escalada = new Mat();
        }

        public MedicionFacial? Medir(Mat imagen, Rect rostro)
        {
            int lado = Math.Min((int)Math.Ceiling(Math.Max(rostro.Width, rostro.Height) * 1.35),
                Math.Min(imagen.Width, imagen.Height));
            if (rostro.Width < 60 || lado < Math.Max(rostro.Width, rostro.Height))
                return null;

            int x = Math.Clamp(rostro.X + (rostro.Width - lado) / 2, 0, imagen.Width - lado);
            int y = Math.Clamp(rostro.Y + (rostro.Height - lado) / 2, 0, imagen.Height - lado);
            using var recorte = new Mat(imagen, new Rect(x, y, lado, lado));
            Cv2.Resize(recorte, escalada, new Size(Tamano, Tamano), interpolation: InterpolationFlags.Area);
            Marshal.Copy(escalada.Data, bgr, 0, bgr.Length);
            for (int i = 0; i < bgr.Length; i += 3)
            {
                rgb[i] = bgr[i + 2] / 255f;
                rgb[i + 1] = bgr[i + 1] / 255f;
                rgb[i + 2] = bgr[i] / 255f;
            }

            using var entrada = OrtValue.CreateTensorValueFromMemory(rgb, Dimension);
            using var salida = sesion.Run(opcionesEjecucion,
                new Dictionary<string, OrtValue> { ["input_1"] = entrada }, Salidas);
            float presencia = salida[1].GetTensorDataAsSpan<float>()[0];
            // El modelo devuelve un logit. Cero equivale a probabilidad 0,5.
            if (!float.IsFinite(presencia) || presencia <= 0)
                return null;

            ReadOnlySpan<float> coordenadas = salida[0].GetTensorDataAsSpan<float>();
            Span<Point2f> puntos = stackalloc Point2f[468];
            float escala = (float)lado / Tamano;
            for (int i = 0; i < puntos.Length; i++)
                puntos[i] = new Point2f(x + coordenadas[3 * i] * escala, y + coordenadas[3 * i + 1] * escala);

            double? izquierdo = CalcularEar(puntos, OjoIzquierdo, imagen.Width, imagen.Height);
            double? derecho = CalcularEar(puntos, OjoDerecho, imagen.Width, imagen.Height);
            if (izquierdo is not double earIzquierdo || derecho is not double earDerecho)
                return null;

            return new MedicionFacial(earIzquierdo, earDerecho,
                EstimarPose(puntos, imagen.Width, imagen.Height, rostro.Width));
        }

        private static double? CalcularEar(ReadOnlySpan<Point2f> puntos, int[] indices, int ancho, int alto)
        {
            foreach (int indice in indices)
            {
                Point2f p = puntos[indice];
                if (!DentroDeImagen(p, ancho, alto))
                    return null;
            }

            double anchoOjo = Distancia(puntos[indices[0]], puntos[indices[3]]);
            if (anchoOjo < 8)
                return null;
            double ear = (Distancia(puntos[indices[1]], puntos[indices[5]]) +
                Distancia(puntos[indices[2]], puntos[indices[4]])) / (2 * anchoOjo);
            return ear <= 0.65 ? ear : null;
        }

        private static AngulosCabeza? EstimarPose(ReadOnlySpan<Point2f> puntos, int ancho, int alto, int anchoRostro)
        {
            var observados = new Point2f[PuntosPose.Length];
            for (int i = 0; i < observados.Length; i++)
            {
                observados[i] = puntos[PuntosPose[i]];
                if (!DentroDeImagen(observados[i], ancho, alto))
                    return null;
            }

            double focal = ancho, centroX = ancho / 2.0, centroY = alto / 2.0;
            double[,] intrinsecos = { { focal, 0, centroX }, { 0, focal, centroY }, { 0, 0, 1 } };
            double[] rotacion = new double[3], traslacion = new double[3];
            Cv2.SolvePnP(RostroCanonico, observados, intrinsecos, new double[4], ref rotacion, ref traslacion);
            if (!rotacion.All(double.IsFinite) || !traslacion.All(double.IsFinite) || traslacion[2] <= 0)
                return null;

            Cv2.Rodrigues(rotacion, out double[,] r, out _);
            double error = 0;
            for (int i = 0; i < RostroCanonico.Length; i++)
            {
                Point3f p = RostroCanonico[i];
                double x = r[0, 0] * p.X + r[0, 1] * p.Y + r[0, 2] * p.Z + traslacion[0];
                double y = r[1, 0] * p.X + r[1, 1] * p.Y + r[1, 2] * p.Z + traslacion[1];
                double z = r[2, 0] * p.X + r[2, 1] * p.Y + r[2, 2] * p.Z + traslacion[2];
                if (z <= 0)
                    return null;
                double dx = focal * x / z + centroX - observados[i].X;
                double dy = focal * y / z + centroY - observados[i].Y;
                error += dx * dx + dy * dy;
            }
            if (Math.Sqrt(error / observados.Length) > anchoRostro * 0.08)
                return null;

            const double grados = 180 / Math.PI;
            var angulos = new AngulosCabeza(
                grados * Math.Atan2(r[2, 1], r[2, 2]),
                grados * Math.Asin(Math.Clamp(-r[2, 0], -1, 1)),
                grados * Math.Atan2(r[1, 0], r[0, 0]));
            return Math.Abs(angulos.Vertical) <= 65 && Math.Abs(angulos.Horizontal) <= 65 &&
                Math.Abs(angulos.Lateral) <= 65 ? angulos : null;
        }

        private static bool DentroDeImagen(Point2f p, int ancho, int alto) =>
            p.X >= 0 && p.X < ancho && p.Y >= 0 && p.Y < alto;

        private static double Distancia(Point2f a, Point2f b)
        {
            double x = a.X - b.X, y = a.Y - b.Y;
            return Math.Sqrt(x * x + y * y);
        }

        public void Dispose()
        {
            escalada.Dispose();
            opcionesEjecucion.Dispose();
            sesion.Dispose();
        }
    }
}
