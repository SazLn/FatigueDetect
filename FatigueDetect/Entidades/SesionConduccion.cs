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
        private readonly List<EventoSesion> eventos = new List<EventoSesion>();
        private int cantidadAlarmas;

        public Guid Id { get; init; } = Guid.NewGuid();
        public Conductor Conductor { get; set; } = new Conductor();
        public Vehiculo Vehiculo { get; set; } = new Vehiculo();
        public DateTime Inicio { get; set; }
        public DateTime? Fin { get; set; }
        public CalidadSesion? Calidad { get; set; }
        public MetricasFatiga? MetricasFatiga { get; set; }
        public InformeAtencion? Atencion { get; set; }
        public ComparacionPersonal? ComparacionPersonal { get; set; }
        public MetricasPostura? Postura { get; set; }
        public string CamaraId { get; set; } = "AUTOMATICA";
        public CalibracionGuiada? Calibracion { get; set; }
        public UmbralesPersonalizados? UmbralesAplicados { get; set; }

        public double? OjosAbiertosObservados => MetricasFatiga?.Perclos is double perclos
            ? Math.Clamp(1 - perclos, 0, 1) : null;

        public double? CoberturaOcular => Calidad is { TiempoActivoMs: > 0 } calidad && MetricasFatiga is { } fatiga
            ? Math.Clamp((double)fatiga.TiempoOcularValidoMs / calidad.TiempoActivoMs, 0, 1) : null;

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

        public void RegistrarAlarma(DateTimeOffset inicio)
        {
            lock (candado)
            {
                cantidadAlarmas++;
                horasAlarmas.Add(inicio.LocalDateTime);
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

        public List<EventoSesion> Eventos
        {
            get { lock (candado) { return new List<EventoSesion>(eventos); } }
        }

        public void RegistrarEvento(EventoSesion evento)
        {
            lock (candado)
            {
                eventos.Add(evento);
            }
        }

        public void EstablecerEventos(IEnumerable<EventoSesion> nuevosEventos)
        {
            lock (candado)
            {
                eventos.Clear();
                eventos.AddRange(nuevosEventos);
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

        public TimeSpan Duracion => (Fin?.ToUniversalTime() ?? DateTime.UtcNow) - Inicio.ToUniversalTime();
    }
}
