using System;
using System.Collections.Generic;

namespace FatigueDetect.Entidades
{
    /// <summary>
    /// Una sesión = desde que el conductor inicia el monitoreo hasta que lo detiene.
    /// </summary>
    public class SesionConduccion
    {
        private readonly object candado = new object();
        private readonly List<DateTime> horasAlarmas = new List<DateTime>();
        private int cantidadAlarmas;

        public Conductor Conductor { get; set; } = new Conductor();
        public Vehiculo Vehiculo { get; set; } = new Vehiculo();
        public DateTime Inicio { get; set; }
        public DateTime? Fin { get; set; }

        // Las alarmas se registran desde el hilo de detección, por eso se protegen con lock.
        public int CantidadAlarmas
        {
            get { lock (candado) { return cantidadAlarmas; } }
            set { lock (candado) { cantidadAlarmas = value; } }
        }

        /// <summary>Copia de las horas en que sonó cada alarma.</summary>
        public List<DateTime> HorasAlarmas
        {
            get { lock (candado) { return new List<DateTime>(horasAlarmas); } }
        }

        public void RegistrarAlarma()
        {
            lock (candado)
            {
                cantidadAlarmas++;
                horasAlarmas.Add(DateTime.Now);
            }
        }

        public void EstablecerHorasAlarmas(IEnumerable<DateTime> horas)
        {
            lock (candado)
            {
                horasAlarmas.Clear();
                horasAlarmas.AddRange(horas);
            }
        }

        /// <summary>Cuántas alarmas sonaron desde la hora indicada.</summary>
        public int AlarmasDesde(DateTime desde)
        {
            lock (candado)
            {
                int total = 0;
                foreach (DateTime hora in horasAlarmas)
                {
                    if (hora >= desde) total++;
                }
                return total;
            }
        }

        public TimeSpan Duracion => (Fin ?? DateTime.Now) - Inicio;
    }
}
