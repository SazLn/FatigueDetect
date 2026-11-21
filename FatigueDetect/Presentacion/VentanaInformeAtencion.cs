using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal sealed class VentanaInformeAtencion : Form
    {
        public VentanaInformeAtencion(SesionConduccion sesion, string? detalle = null)
        {
            Text = "Atención y resumen de la sesión";
            Size = new Size(1000, 720);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;
            var contenido = Tema.TablaFilas(Tema.Fondo, 0, 42);
            var pestanas = new TabControl { Dock = DockStyle.Fill };
            var resumen = new TabPage("Resumen");
            resumen.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical, Font = Tema.Fuente(11),
                Text = (detalle ?? (AnalisisAtencion.Resumir(sesion.Atencion) + "\n\n" +
                    PersonalizacionConductor.Resumir(sesion.ComparacionPersonal))).Replace("\n", Environment.NewLine) });
            pestanas.TabPages.Add(resumen);
            var evolucion = new TabPage("Evolución y factores");
            var panel = Tema.TablaFilas(Tema.Fondo, 180, 0);
            var muestras = sesion.Atencion?.Muestras ?? Array.Empty<MuestraAtencion>();
            panel.Controls.Add(new GraficaAtencion(muestras), 0, 0);
            var tabla = DashboardHistorial.CrearTabla("Hora", "Tiempo activo (s)", "Atención /100", "Calidad /100", "Distracción medida", "Descuentos (puntos)");
            foreach (var m in muestras)
                tabla.Rows.Add(m.Instante.LocalDateTime.ToString("HH:mm:ss.fff"), m.ActivoMs / 1000.0,
                    m.Puntaje?.ToString("0.0") ?? "—", m.Calidad?.ToString("0.0") ?? "—",
                    m.DistraccionDisponible ? "Sí" : "No", string.Join("; ", m.Factores.Select(f => $"{f.Nombre}: −{f.Descuento:0.0}")));
            panel.Controls.Add(tabla, 0, 1);
            evolucion.Controls.Add(panel);
            pestanas.TabPages.Add(evolucion);
            var respuesta = new TabPage("Respuesta a alarmas");
            var respuestas = DashboardHistorial.CrearTabla("Alarma", "Respuesta ocular (s)", "Recuperación confirmada (s)", "Resultado");
            foreach (var r in sesion.Atencion?.Respuestas ?? Array.Empty<RespuestaAlarma>())
                respuestas.Rows.Add(r.Inicio.LocalDateTime.ToString("HH:mm:ss.fff"), r.RespuestaMs / 1000.0,
                    r.RecuperacionMs / 1000.0, r.Estado);
            respuesta.Controls.Add(respuestas);
            pestanas.TabPages.Add(respuesta);
            contenido.Controls.Add(pestanas, 0, 0);
            var linea = Tema.Boton("Ver todos los eventos", Tema.TarjetaClara, Tema.Texto);
            linea.Click += (_, _) => { using var ventana = new VentanaLineaTemporal(sesion); ventana.ShowDialog(this); };
            contenido.Controls.Add(linea, 0, 1);
            Controls.Add(contenido);
        }

        private sealed class GraficaAtencion(IReadOnlyList<MuestraAtencion> muestras) : Control
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Width < 80 || Height < 60) return;
                using var fuente = Tema.Fuente(9);
                using var texto = new SolidBrush(Tema.Texto);
                using var guia = new Pen(Tema.Borde);
                using var linea = new Pen(Tema.Azul, 2);
                for (int i = 0; i <= 4; i++)
                {
                    float y = 10 + (Height - 40) * i / 4f;
                    e.Graphics.DrawString((100 - i * 25).ToString(), fuente, texto, 0, y);
                    e.Graphics.DrawLine(guia, 35, y, Width - 10, y);
                }
                if (muestras.Count == 0) return;
                DateTimeOffset inicio = muestras[0].Instante;
                double total = Math.Max(1, (muestras[^1].Instante - inicio).TotalMilliseconds);
                PointF? anterior = null;
                DateTimeOffset? horaAnterior = null;
                foreach (var m in muestras)
                {
                    if (m.Puntaje is not double valor) { anterior = null; continue; }
                    var punto = new PointF(35 + (float)((m.Instante - inicio).TotalMilliseconds / total * (Width - 45)),
                        10 + (float)((100 - valor) / 100 * (Height - 40)));
                    if (anterior is PointF p && horaAnterior.HasValue && (m.Instante - horaAnterior.Value).TotalMilliseconds <= 1500)
                        e.Graphics.DrawLine(linea, p, punto);
                    anterior = punto;
                    horaAnterior = m.Instante;
                }
                e.Graphics.DrawString($"{inicio.LocalDateTime:HH:mm:ss} — {muestras[^1].Instante.LocalDateTime:HH:mm:ss} · Huecos: sin medición o pausa",
                    fuente, texto, 35, Height - 23);
            }

            protected override void OnCreateControl()
            {
                base.OnCreateControl();
                Dock = DockStyle.Fill;
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }
    }
}
