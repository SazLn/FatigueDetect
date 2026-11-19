using System;
using System.IO;
using System.Media;
using FatigueDetect.Entidades;

namespace FatigueDetect.Datos
{
    public class AlarmService
    {
        private readonly Configuracion config;
        private SoundPlayer? player;
        private bool playing = false;

        public AlarmService(Configuracion? config = null)
        {
            this.config = config ?? new Configuracion();
        }

        public void Activar()
        {
            if (!config.Deteccion.SonidoActivado)
            {
                Detener();
                return;
            }
            if (playing) return;

            try
            {
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Resources", "sounds", "alarma.wav");

                if (!File.Exists(soundPath))
                {
                    Console.WriteLine("No se encontró el archivo alarma.wav");
                    return;
                }

                player = new SoundPlayer(soundPath);
                player.Load();
                player.PlayLooping();
                playing = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error al reproducir alarma: " + e.Message);
            }
        }

        public void Detener()
        {
            if (player != null && playing)
            {
                player.Stop();
                player.Dispose();
                player = null;
                playing = false;
            }
        }

        public bool IsPlaying()
        {
            return playing;
        }
    }
}
