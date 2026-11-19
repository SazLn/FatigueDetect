using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal sealed class VentanaLineaTemporal : Form
    {
        public VentanaLineaTemporal(SesionConduccion sesion)
        {
            Text = "Línea temporal de la sesión";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(990, 650);
            MinimumSize = new Size(740, 500);
            Padding = new Padding(18);
            BackColor = Tema.Fondo;

            EventoSesion[] eventos = sesion.Eventos.OrderBy(e => e.Inicio).ToArray();
            int alarmasSinHora = ResumenHistorial.AlarmasSinHora(sesion, eventos);
            var contenido = Tema.TablaFilas(Tema.Fondo, 34, 28, 0, 0);
            contenido.RowStyles[2].Height = 62;
            contenido.RowStyles[3].Height = 38;
            contenido.Controls.Add(Tema.Etiqueta(sesion.Inicio.ToString("dd/MM/yyyy HH:mm:ss") +
                " · " + sesion.Conductor.NombreCompleto, 14, Tema.Texto, Tema.Fondo, FontStyle.Bold), 0, 0);
            string aviso = alarmasSinHora > 0 ? $" · Alarmas sin hora: {alarmasSinHora}" : "";
            contenido.Controls.Add(Tema.Etiqueta("Barras: duración · Puntos: duración desconocida" + aviso,
                10, Tema.TextoSuave, Tema.Fondo), 0, 1);
            if (eventos.Length == 0)
                contenido.Controls.Add(Tema.Etiqueta("Sin eventos con hora para graficar", 12, Tema.TextoSuave, Tema.Tarjeta), 0, 2);
            else
                contenido.Controls.Add(new LineaTemporal(sesion, eventos), 0, 2);

            var tabla = DashboardHistorial.CrearTabla("Evento", "Gravedad", "Inicio", "Fin", "Duración (s)", "Desde alarma anterior (s)");
            var intervalos = ResumenHistorial.IntervalosEntreAlertas(sesion, eventos)
                .ToDictionary<IntervaloAlerta, EventoSesion, double>(i => i.Alarma, i => i.Segundos, ReferenceEqualityComparer.Instance);
            foreach (EventoSesion evento in eventos)
                tabla.Rows.Add(PaginaHistorial.NombreEvento(evento.Tipo), ResumenHistorial.Gravedad(evento.Tipo),
                    evento.Inicio.LocalDateTime.ToString("HH:mm:ss.fff"), evento.Fin?.LocalDateTime.ToString("HH:mm:ss.fff"),
                    evento.DuracionMilisegundos / 1000.0,
                    intervalos.TryGetValue(evento, out double intervalo) ? intervalo : null);
            contenido.Controls.Add(tabla, 0, 3);
            Controls.Add(contenido);
        }

        private sealed class LineaTemporal : Control
        {
            private readonly IGrouping<TipoEventoSesion, EventoSesion>[] pistas;
            private readonly DateTimeOffset inicio;
            private readonly double segundos;

            public LineaTemporal(SesionConduccion sesion, EventoSesion[] eventos)
            {
                pistas = eventos.GroupBy(e => e.Tipo).OrderBy(g => g.Key).ToArray();
                var inicioSesion = new DateTimeOffset(sesion.Inicio);
                inicio = eventos[0].Inicio < inicioSesion ? eventos[0].Inicio : inicioSesion;
                DateTimeOffset fin = eventos.Max(e => e.Fin ?? e.Inicio);
                if (sesion.Fin is DateTime termina && new DateTimeOffset(termina) > fin)
                    fin = new DateTimeOffset(termina);
                segundos = Math.Max(0.001, (fin - inicio).TotalSeconds);
                Dock = DockStyle.Fill;
                BackColor = Tema.Tarjeta;
                DoubleBuffered = true;
                ResizeRedraw = true;
                AccessibleName = "Línea temporal de eventos. Los tiempos exactos se muestran en la tabla.";
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                const int margen = 210;
                const int final = 24;
                if (Width <= margen + final || Height <= 52)
                    return;

                var g = e.Graphics;
                float ancho = Width - margen - final;
                float altura = (Height - 33f) / pistas.Length;
                using var fuente = Tema.Fuente(8);
                using var tinta = new SolidBrush(Tema.Texto);
                using var guia = new Pen(Tema.TarjetaClara);
                for (int i = 0; i <= 4; i++)
                {
                    float x = margen + i * ancho / 4;
                    g.DrawLine(guia, x, 0, x, Height - 29);
                    string marca = inicio.AddSeconds(i * segundos / 4).LocalDateTime.ToString(segundos < 8 ? "HH:mm:ss.fff" : "HH:mm:ss");
                    g.DrawString(marca, fuente, tinta, x - (i == 4 ? g.MeasureString(marca, fuente).Width : 0), Height - 23);
                }

                for (int i = 0; i < pistas.Length; i++)
                {
                    var pista = pistas[i];
                    float y = (i + 0.5f) * altura;
                    g.DrawString(PaginaHistorial.NombreEvento(pista.Key), fuente, tinta, 9, y - 6);
                    using var color = new SolidBrush(ResumenHistorial.Gravedad(pista.Key) switch
                    {
                        GravedadEvento.Alta => Tema.Rojo,
                        GravedadEvento.Media => Tema.Ambar,
                        _ => Tema.Azul
                    });
                    foreach (EventoSesion evento in pista)
                    {
                        float x = margen + (float)((evento.Inicio - inicio).TotalSeconds / segundos) * ancho;
                        if (evento.Fin is DateTimeOffset fin)
                        {
                            float largo = (float)((fin - evento.Inicio).TotalSeconds / segundos) * ancho;
                            g.FillRectangle(color, x, y - 4, Math.Max(2, largo), 8);
                        }
                        else
                            g.FillEllipse(color, x - 4, y - 4, 8, 8);
                    }
                }
            }
        }
    }
}
