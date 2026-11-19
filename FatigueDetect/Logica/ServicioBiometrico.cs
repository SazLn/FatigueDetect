using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;
using OpenCvSharp;

namespace FatigueDetect.Logica
{
    public sealed class ProgresoCapturaBiometrica
    {
        public Bitmap Imagen { get; init; } = null!;
        public string Mensaje { get; init; } = "";
        public int Capturas { get; init; }
        public int Requeridas { get; init; }
    }

    public sealed class ResultadoIdentificacion
    {
        public PerfilBiometrico? Perfil { get; init; }
        public double Coincidencia { get; init; }
        public string Mensaje { get; init; } = "";
        public bool Exitoso => Perfil != null;
    }

    /// <summary>Orquesta captura, enrolamiento e identificación facial local.</summary>
    public class ServicioBiometrico
    {
        private const int CapturasRegistro = 5;
        private const int CapturasIdentificacion = 3;
        private const double UmbralCoincidencia = 0.58;
        private const double SeparacionMinima = 0.025;

        private readonly PerfilBiometricoRepository repositorio;
        private readonly string rutaCascada;
        private readonly Configuracion config;
        private readonly ExtractorDescriptorFacial extractor = new ExtractorDescriptorFacial();
        private readonly SemaphoreSlim operacion = new SemaphoreSlim(1, 1);

        public ServicioBiometrico(PerfilBiometricoRepository repositorio, string rutaCascada, Configuracion config)
        {
            this.repositorio = repositorio;
            this.rutaCascada = rutaCascada;
            this.config = config;
        }

        public bool TienePerfil(string cedula) => repositorio.Existe(cedula);

        public bool EliminarPerfil(string cedula) => repositorio.Eliminar(cedula);

        public async Task RegistrarAsync(PerfilBiometrico perfil,
            IProgress<ProgresoCapturaBiometrica> progreso, CancellationToken cancelacion)
        {
            float[] descriptor = await CapturarDescriptorAsync(
                CapturasRegistro, progreso, cancelacion).ConfigureAwait(false);

            perfil.Cedula = perfil.Cedula.Trim();
            perfil.Nombre = perfil.Nombre.Trim();
            perfil.Apellidos = perfil.Apellidos.Trim();
            perfil.TipoCarro = perfil.TipoCarro.Trim();
            perfil.Placa = ValidadorRegistro.NormalizarPlaca(perfil.Placa);
            perfil.DescriptorFacial = descriptor;
            perfil.ActualizadoUtc = DateTime.UtcNow;
            repositorio.Guardar(perfil);
        }

        public async Task<ResultadoIdentificacion> IdentificarAsync(
            IProgress<ProgresoCapturaBiometrica> progreso, CancellationToken cancelacion)
        {
            List<PerfilBiometrico> perfiles = repositorio.LeerTodos()
                .Where(perfil => perfil.DescriptorFacial.Length > 0)
                .ToList();
            if (perfiles.Count == 0)
            {
                return new ResultadoIdentificacion
                {
                    Mensaje = "Todavía no hay rostros registrados. Completa tus datos y registra el primero."
                };
            }

            float[] descriptor = await CapturarDescriptorAsync(
                CapturasIdentificacion, progreso, cancelacion).ConfigureAwait(false);

            var coincidencias = perfiles
                .Select(perfil => new
                {
                    Perfil = perfil,
                    Valor = extractor.Comparar(descriptor, perfil.DescriptorFacial)
                })
                .OrderByDescending(resultado => resultado.Valor)
                .ToList();

            var mejor = coincidencias[0];
            double segunda = coincidencias.Count > 1 ? coincidencias[1].Valor : 0;
            if (mejor.Valor < UmbralCoincidencia)
            {
                return new ResultadoIdentificacion
                {
                    Coincidencia = mejor.Valor,
                    Mensaje = "No se encontró una coincidencia confiable. Intenta otra vez con luz frontal."
                };
            }
            if (coincidencias.Count > 1 && mejor.Valor - segunda < SeparacionMinima)
            {
                return new ResultadoIdentificacion
                {
                    Coincidencia = mejor.Valor,
                    Mensaje = "El resultado es ambiguo. Identifícate manualmente o vuelve a capturar el rostro."
                };
            }

            return new ResultadoIdentificacion
            {
                Perfil = mejor.Perfil,
                Coincidencia = mejor.Valor,
                Mensaje = $"Identidad confirmada: {mejor.Perfil.NombreCompleto}."
            };
        }

        private async Task<float[]> CapturarDescriptorAsync(int requeridas,
            IProgress<ProgresoCapturaBiometrica> progreso, CancellationToken cancelacion)
        {
            bool adquirido = await operacion.WaitAsync(0, cancelacion).ConfigureAwait(false);
            if (!adquirido)
            {
                throw new InvalidOperationException("Ya hay una captura biométrica en curso.");
            }

            try
            {
                string? camaraId = config.CamaraId;
                return await HiloCapturaCamara.EjecutarAsync(() =>
                    CapturarDescriptor(camaraId, requeridas, progreso, cancelacion)).ConfigureAwait(false);
            }
            finally
            {
                operacion.Release();
            }
        }

        private float[] CapturarDescriptor(string? camaraId, int requeridas,
            IProgress<ProgresoCapturaBiometrica> progreso, CancellationToken cancelacion)
        {
            using var camara = new CameraService(camaraId, cancelacion, config.FormatoCamara);
            if (!camara.IsOpened())
            {
                throw new InvalidOperationException(
                    "No se pudo abrir la cámara. Cierra otras aplicaciones que puedan estar utilizándola.");
            }

            var detector = new FaceDetector(rutaCascada);
            var descriptores = new List<float[]>();
            DateTime limite = DateTime.UtcNow.AddSeconds(25);
            DateTime siguienteCaptura = DateTime.MinValue;
            string mensaje = "Mira de frente y mueve suavemente la cabeza.";

            while (descriptores.Count < requeridas && DateTime.UtcNow < limite)
            {
                cancelacion.ThrowIfCancellationRequested();
                using Mat frame = camara.GetFrame();
                if (frame.Empty())
                {
                    Thread.Sleep(40);
                    continue;
                }

                Cv2.Flip(frame, frame, FlipMode.Y);
                Rect[] rostros = detector.Detect(frame);
                Rect? principal = null;
                if (rostros.Length == 0)
                {
                    mensaje = "Coloca tu rostro dentro de la cámara.";
                }
                else if (rostros.Length > 1)
                {
                    mensaje = "Debe aparecer una sola persona en la imagen.";
                }
                else
                {
                    principal = rostros[0];
                    if (DateTime.UtcNow >= siguienteCaptura)
                    {
                        if (extractor.IntentarExtraer(frame, rostros[0], out float[] descriptor,
                            out string estado))
                        {
                            descriptores.Add(descriptor);
                            siguienteCaptura = DateTime.UtcNow.AddMilliseconds(650);
                            mensaje = descriptores.Count < requeridas
                                ? "Captura válida. Gira ligeramente la cabeza."
                                : "Captura completada.";
                        }
                        else
                        {
                            mensaje = estado;
                        }
                    }
                    Cv2.Rectangle(frame, rostros[0], new Scalar(44, 190, 92), 3);
                }

                Bitmap? imagen = ImageUtils.MatToBitmap(frame);
                if (imagen != null)
                {
                    progreso.Report(new ProgresoCapturaBiometrica
                    {
                        Imagen = imagen,
                        Mensaje = mensaje,
                        Capturas = descriptores.Count,
                        Requeridas = requeridas
                    });
                }

                Thread.Sleep(principal.HasValue ? 60 : 90);
            }

            cancelacion.ThrowIfCancellationRequested();
            if (descriptores.Count < requeridas)
            {
                throw new TimeoutException(
                    $"No se lograron obtener {requeridas} capturas válidas. Revisa la iluminación e inténtalo de nuevo.");
            }

            return extractor.Promediar(descriptores);
        }
    }
}
