using System.Runtime.InteropServices;
using FatigueDetect.Entidades;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FatigueDetect.Datos
{
    public sealed record SalidaAudio(string? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    public sealed class AlarmService(Configuracion? config = null)
    {
        private static readonly string RutaAlarma =
            Path.Combine(AppContext.BaseDirectory, "Resources", "sounds", "alarma.wav");
        private readonly Configuracion configuracion = config ?? new();
        private Reproduccion? reproductor;
        private string? salidaActual;
        private DateTimeOffset siguienteIntento;
        private DateTimeOffset finReproduccion;
        private NivelAlertaProgresiva nivelActual;

        public static IReadOnlyList<SalidaAudio> ListarSalidas()
        {
            using var catalogo = new MMDeviceEnumerator();
            var salidas = new List<SalidaAudio> { new(null, "Predeterminada de Windows") };
            foreach (MMDevice dispositivo in catalogo.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (dispositivo)
                    salidas.Add(new(dispositivo.ID, dispositivo.FriendlyName));
            }
            return salidas;
        }

        public static async Task ProbarAsync(string? id)
        {
            using var prueba = new Reproduccion(id);
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (prueba.Salida.PlaybackState != PlaybackState.Playing)
                throw new InvalidOperationException("La reproducción se interrumpió antes de terminar la prueba.");
        }

        public void Activar() => Actualizar(NivelAlertaProgresiva.Critica, emitir: true);

        public void Actualizar(NivelAlertaProgresiva nivel, bool emitir)
        {
            AjustesDeteccion ajustes = configuracion.Deteccion;
            if (!ajustes.SonidoActivado)
            {
                Detener();
                return;
            }

            string? id = ajustes.SalidaAudioId;
            if (salidaActual != id)
            {
                Detener();
                salidaActual = id;
            }
            DateTimeOffset ahora = DateTimeOffset.UtcNow;
            if (nivel == NivelAlertaProgresiva.Normal)
            {
                Detener();
                return;
            }
            if (IsPlaying() && nivel != nivelActual)
            {
                bool escalando = nivel > nivelActual;
                Detener();
                if (escalando)
                    siguienteIntento = DateTimeOffset.MinValue;
            }
            if (IsPlaying() && ahora >= finReproduccion)
                Detener();
            if (!emitir || IsPlaying() || ahora < siguienteIntento)
                return;

            Detener();
            nivelActual = nivel;
            double volumen = nivel switch
            {
                NivelAlertaProgresiva.Preventiva => .35,
                NivelAlertaProgresiva.Alta => .65,
                _ => 1
            };
            finReproduccion = ahora.AddMilliseconds(nivel switch
            {
                NivelAlertaProgresiva.Preventiva => 700,
                NivelAlertaProgresiva.Alta => 1500,
                _ => 4000
            });
            siguienteIntento = ahora.AddSeconds(2);
            try
            {
                reproductor = new Reproduccion(id, volumen);
            }
            catch (Exception ex) when (EsErrorDeAudio(ex))
            {
                System.Diagnostics.Trace.TraceWarning("Salida de alarma: " + ex.Message);
                if (id is null)
                    return;
                try
                {
                    reproductor = new Reproduccion(null, volumen);
                }
                catch (Exception error) when (EsErrorDeAudio(error))
                {
                    System.Diagnostics.Trace.TraceError("Alarma sin audio: " + error.Message);
                }
            }
        }

        public static bool EsErrorDeAudio(Exception error) =>
            error is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException;

        public void Detener()
        {
            reproductor?.Dispose();
            reproductor = null;
            nivelActual = NivelAlertaProgresiva.Normal;
        }

        public bool IsPlaying() => reproductor?.Salida.PlaybackState == PlaybackState.Playing;

        private sealed class Reproduccion : IDisposable
        {
            private readonly MMDevice dispositivo;
            private readonly WaveFileReader archivo;
            public WasapiOut Salida { get; }

            public Reproduccion(string? id, double volumen = 1)
            {
                using var catalogo = new MMDeviceEnumerator();
                dispositivo = id is null
                    ? catalogo.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                    : catalogo.GetDevice(id);
                try
                {
                    archivo = new WaveFileReader(RutaAlarma);
                    try
                    {
                        Salida = new WasapiOut(dispositivo, AudioClientShareMode.Shared, false, 100);
                        try
                        {
                            Salida.Init(new VolumeWaveProvider16(new BucleWav(archivo))
                            {
                                Volume = (float)Math.Clamp(volumen, 0, 1)
                            });
                            Salida.Play();
                        }
                        catch
                        {
                            Salida.Dispose();
                            throw;
                        }
                    }
                    catch
                    {
                        archivo.Dispose();
                        throw;
                    }
                }
                catch
                {
                    dispositivo.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                try
                {
                    Salida.Dispose();
                }
                finally
                {
                    archivo.Dispose();
                    dispositivo.Dispose();
                }
            }
        }

        private sealed class BucleWav(WaveFileReader archivo) : IWaveProvider
        {
            public WaveFormat WaveFormat => archivo.WaveFormat;

            public int Read(byte[] buffer, int offset, int count)
            {
                int leidos = 0;
                while (leidos < count && archivo.Length > 0)
                {
                    int cantidad = archivo.Read(buffer, offset + leidos, count - leidos);
                    if (cantidad == 0)
                    {
                        if (archivo.Position == 0)
                            break;
                        archivo.Position = 0;
                    }
                    leidos += cantidad;
                }
                return leidos;
            }
        }
    }
}
