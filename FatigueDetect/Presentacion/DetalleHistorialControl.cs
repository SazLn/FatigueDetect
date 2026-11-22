using System.Drawing;
using System.Windows.Forms;
using FatigueDetect.Entidades;
using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    /// <summary>Detalle desplazable de una sesión con cabecera y acciones fijas.</summary>
    internal sealed class DetalleHistorialControl : UserControl
    {
        private readonly Label contexto;
        private readonly FlowLayoutPanel cuerpo;
        private readonly BotonConfiguracion lineaTemporal = new("Ver línea temporal");
        private readonly BotonConfiguracion informeAtencion = new("Atención y resumen", primario: true);
        private SesionConduccion? sesion;

        public DetalleHistorialControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Tema.Tarjeta;
            Margin = Padding.Empty;

            var raiz = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                BackColor = Tema.Tarjeta
            };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

            var cabecera = Tema.TablaFilas(Tema.Tarjeta, 30, 22);
            cabecera.Padding = new Padding(6, 0, 6, 0);
            cabecera.Controls.Add(Tema.Etiqueta("Detalle de la sesión", 13, Tema.Texto, Tema.Tarjeta,
                FontStyle.Bold), 0, 0);
            contexto = Tema.Etiqueta("Selecciona una sesión de la tabla.", 9, Tema.TextoSuave, Tema.Tarjeta);
            cabecera.Controls.Add(contexto, 0, 1);
            raiz.Controls.Add(cabecera, 0, 0);

            cuerpo = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Tema.Tarjeta,
                Padding = new Padding(6, 4, 2, 10),
                Margin = Padding.Empty
            };
            cuerpo.SizeChanged += (_, _) => AjustarAnchos();
            raiz.Controls.Add(cuerpo, 0, 1);

            var pie = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(6, 10, 6, 0),
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(22, 18, 34)
            };
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            lineaTemporal.Dock = DockStyle.Fill;
            lineaTemporal.Margin = new Padding(0, 0, 5, 0);
            informeAtencion.Dock = DockStyle.Fill;
            informeAtencion.Margin = new Padding(5, 0, 0, 0);
            lineaTemporal.Click += (_, _) => AbrirLineaTemporal();
            informeAtencion.Click += (_, _) => AbrirInforme();
            pie.Controls.Add(lineaTemporal, 0, 0);
            pie.Controls.Add(informeAtencion, 1, 0);
            raiz.Controls.Add(pie, 0, 2);
            Controls.Add(raiz);

            Mostrar(null);
        }

        public void Mostrar(SesionConduccion? nueva)
        {
            sesion = nueva;
            lineaTemporal.Enabled = informeAtencion.Enabled = nueva is not null;
            cuerpo.SuspendLayout();
            cuerpo.Controls.Clear();

            if (nueva is null)
            {
                contexto.Text = "Selecciona una sesión de la tabla.";
                cuerpo.Controls.Add(CrearNota("No hay una sesión seleccionada."));
                cuerpo.ResumeLayout(true);
                AjustarAnchos();
                return;
            }

            contexto.Text = $"{nueva.Inicio:dd/MM/yyyy} · {nueva.Conductor.NombreCompleto}";
            cuerpo.Controls.Add(CrearCuadricula(new[]
            {
                ("Conductor", nueva.Conductor.NombreCompleto),
                ("Cédula", nueva.Conductor.Cedula),
                ("Vehículo", nueva.Vehiculo.TipoCarro),
                ("Placa", nueva.Vehiculo.Placa),
                ("Inicio", nueva.Inicio.ToString("dd/MM/yyyy HH:mm:ss")),
                ("Fin", nueva.Fin?.ToString("dd/MM/yyyy HH:mm:ss") ?? "—"),
                ("Duración", nueva.Fin.HasValue ? Tema.FormatoDuracion(nueva.Duracion) : "—"),
                ("Alarmas activadas", nueva.CantidadAlarmas.ToString())
            }));

            cuerpo.Controls.Add(CrearSeccion("Calidad de la captura"));
            AgregarCalidad(nueva);
            cuerpo.Controls.Add(CrearSeccion("Ojos y parpadeos"));
            AgregarOjos(nueva);
            cuerpo.Controls.Add(CrearSeccion("Resumen de eventos"));
            AgregarResumen(nueva);
            cuerpo.Controls.Add(CrearSeccion("Eventos de la sesión"));
            AgregarEventos(nueva);

            cuerpo.ResumeLayout(true);
            cuerpo.AutoScrollPosition = Point.Empty;
            AjustarAnchos();
        }

        private void AgregarCalidad(SesionConduccion s)
        {
            if (s.Calidad is not CalidadSesion c)
            {
                cuerpo.Controls.Add(CrearNota("Calidad: sin medición"));
                return;
            }

            cuerpo.Controls.Add(CrearCuadricula(new[]
            {
                ("Calidad general", Numero(c.Puntaje, "/100")),
                ("FPS medios", Numero(c.Fps)),
                ("Analizables", Porcentaje(c.ProporcionAnalizable)),
                ("Cobertura facial", Porcentaje(c.CoberturaRostro)),
                ("Caídas de FPS", c.CaidasFps?.ToString() ?? "—"),
                ("Fallos de cámara", c.FallosCamara.ToString()),
                ("Cámara/entorno", Numero(c.PuntajeCamaraEntorno, "/100")),
                ("Colocación", Numero(c.PuntajeColocacion, "/100")),
                ("Brillo", Numero(c.BrilloPromedio, decimales: 0)),
                ("Contraste", Numero(c.ContrastePromedio, decimales: 0)),
                ("Nitidez", Numero(c.NitidezPromedio, decimales: 0)),
                ("Posición", Numero(c.PosicionFacialPromedio, decimales: 0))
            }));
            string explicacion = c.ExplicarReducciones();
            cuerpo.Controls.Add(CrearNota(string.IsNullOrWhiteSpace(explicacion)
                ? "Sin factores de calidad destacados." : explicacion));
        }

        private void AgregarOjos(SesionConduccion s)
        {
            if (s.MetricasFatiga is not MetricasFatiga f)
            {
                cuerpo.Controls.Add(CrearNota("Métricas faciales: sin medición"));
                return;
            }

            var valores = new List<(string, string)>
            {
                ("Ojos abiertos observados", Porcentaje(s.OjosAbiertosObservados)),
                ("Cobertura ocular", Porcentaje(s.CoberturaOcular)),
                ("Parpadeos", f.Parpadeos.ToString()),
                ("Microsueños", f.Microsuenos.ToString()),
                ("Cabeceos", f.Cabeceos.ToString()),
                ("PERCLOS de sesión", Porcentaje(f.Perclos)),
                ("Tiempo cerrado", $"{f.TiempoOcularCerradoMs / 1000.0:0.0} s"),
                ("Tiempo ocular válido", $"{f.TiempoOcularValidoMs / 1000.0:0.0} s")
            };
            if (f.VersionMetricasOculares >= 1)
            {
                valores.Add(("Frecuencia", $"{f.ParpadeosPorMinuto?.ToString("0.0") ?? "—"}/min"));
                valores.Add(("Cierres anómalos", f.CierresAnomalos.ToString()));
                valores.Add(("Cortos · Prolongados", $"{f.ParpadeosCortos} · {f.ParpadeosProlongados}"));
                valores.Add(("Cierre más largo", $"{f.CierreMasLargoMs / 1000.0:0.000} s"));
                valores.Add(("Parpadeo mín. / prom. / máx.",
                    $"{Ms(f.DuracionParpadeoMinMs)} / {Ms(f.DuracionParpadeoPromedioMs)} / {Ms(f.DuracionParpadeoMaxMs)}"));
                valores.Add(("Microsueño más largo", $"{f.MicrosuenoMasLargoMs / 1000.0:0.000} s"));
            }
            cuerpo.Controls.Add(CrearCuadricula(valores));
        }

        private void AgregarResumen(SesionConduccion s)
        {
            bool alguno = false;
            foreach (ResumenEvento resumen in ResumenHistorial.PorTipo(s, s.Eventos))
            {
                string tiempo = resumen.TotalMs.HasValue
                    ? $"{resumen.Cantidad} eventos · total {resumen.TotalMs / 1000.0:0.0} s · máximo {resumen.MaximoMs / 1000.0:0.0} s"
                    : $"{resumen.Cantidad} eventos";
                if (resumen.SinDuracion > 0)
                    tiempo += $" · {resumen.SinDuracion} sin duración";
                cuerpo.Controls.Add(CrearEvento(PaginaHistorial.NombreEvento(resumen.Tipo), tiempo, null));
                alguno = true;
            }
            if (s.Postura is { } postura)
            {
                foreach (string linea in MainForm.FormatearPostura(s, postura).Split('\n'))
                {
                    cuerpo.Controls.Add(CrearNota(linea));
                    alguno = true;
                }
            }
            if (!alguno)
                cuerpo.Controls.Add(CrearNota("Sin eventos registrados"));
        }

        private void AgregarEventos(SesionConduccion s)
        {
            bool alguno = false;
            foreach (EventoSesion evento in s.Eventos.OrderBy(e => e.Inicio))
            {
                string hora = evento.Inicio.LocalDateTime.ToString("dd/MM HH:mm:ss.fff");
                string detalle = evento.DuracionMilisegundos is long ms ? $"{ms / 1000.0:0.000} s" : "Duración desconocida";
                if (evento.Confiabilidad?.Puntaje is double confianza)
                    detalle += $" · confianza {confianza:0}/100 ({evento.Confiabilidad.Nivel.ToLowerInvariant()})";
                cuerpo.Controls.Add(CrearEvento(PaginaHistorial.NombreEvento(evento), detalle, hora));
                alguno = true;
            }
            int alarmasSinHora = ResumenHistorial.AlarmasSinHora(s, s.Eventos);
            if (alarmasSinHora > 0)
            {
                cuerpo.Controls.Add(CrearEvento("Alarmas sin hora registrada",
                    $"{alarmasSinHora} eventos · hora y duración desconocidas", null));
                alguno = true;
            }
            if (!alguno)
                cuerpo.Controls.Add(CrearNota("Sin eventos registrados"));
        }

        private static Control CrearCuadricula(IEnumerable<(string Clave, string Valor)> datos)
        {
            var elementos = datos.ToArray();
            int filas = (elementos.Length + 1) / 2;
            var tabla = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = filas,
                Height = Math.Max(1, filas) * 45,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Tema.Tarjeta
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < filas; i++)
                tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            for (int i = 0; i < elementos.Length; i++)
                tabla.Controls.Add(CrearDato(elementos[i].Clave, elementos[i].Valor), i % 2, i / 2);
            return tabla;
        }

        private static Control CrearDato(string clave, string valor)
        {
            var dato = Tema.TablaFilas(Tema.Tarjeta, 18, 24);
            dato.Margin = new Padding(0, 0, 8, 0);
            dato.Controls.Add(Tema.Etiqueta(clave, 8.5f, Tema.TextoSuave, Tema.Tarjeta), 0, 0);
            Label contenido = Tema.Etiqueta(valor, 9.5f, Tema.Texto, Tema.Tarjeta, FontStyle.Bold);
            contenido.AutoEllipsis = true;
            dato.Controls.Add(contenido, 0, 1);
            return dato;
        }

        private static Control CrearSeccion(string texto)
        {
            var panel = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                Height = 37,
                Margin = new Padding(0, 10, 0, 5),
                BackColor = Tema.Tarjeta
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
            panel.Controls.Add(Tema.Etiqueta(texto, 9, Tema.Azul, Tema.Tarjeta, FontStyle.Bold), 0, 0);
            panel.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Tema.Borde }, 0, 1);
            return panel;
        }

        private static Control CrearEvento(string titulo, string detalle, string? hora)
        {
            int alto = hora is null ? 58 : 72;
            var panel = new PanelRedondeado
            {
                Height = alto,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Tema.Tarjeta,
                ColorTarjeta = Tema.TarjetaClara,
                ColorBorde = Tema.TarjetaClara,
                Radio = 10,
                Padding = new Padding(14, 8, 8, 7)
            };
            var contenido = hora is null
                ? Tema.TablaFilas(Tema.TarjetaClara, 24, 22)
                : Tema.TablaFilas(Tema.TarjetaClara, 18, 24, 22);
            if (hora is not null)
                contenido.Controls.Add(Tema.Etiqueta(hora, 8.5f, Tema.TextoSuave, Tema.TarjetaClara), 0, 0);
            contenido.Controls.Add(Tema.Etiqueta(titulo, 9.5f, Tema.Texto, Tema.TarjetaClara,
                FontStyle.Bold), 0, hora is null ? 0 : 1);
            contenido.Controls.Add(Tema.Etiqueta(detalle, 8.5f, Tema.TextoSuave, Tema.TarjetaClara),
                0, hora is null ? 1 : 2);
            panel.Controls.Add(contenido);
            var acento = new Panel { Dock = DockStyle.Left, Width = 3,
                BackColor = Color.FromArgb(244, 165, 192) };
            panel.Controls.Add(acento);
            acento.BringToFront();
            return panel;
        }

        private static Control CrearNota(string texto)
        {
            Label nota = Tema.Etiqueta(texto, 9, Tema.TextoSuave, Tema.Tarjeta);
            nota.Height = Math.Max(34, ((texto.Length / 48) + 1) * 20);
            nota.Margin = new Padding(0, 0, 0, 5);
            return nota;
        }

        private void AjustarAnchos()
        {
            int ancho = Math.Max(120, cuerpo.ClientSize.Width - cuerpo.Padding.Horizontal -
                (cuerpo.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
            foreach (Control control in cuerpo.Controls)
                control.Width = ancho;
        }

        private void AbrirLineaTemporal()
        {
            if (sesion is null) return;
            using var ventana = new VentanaLineaTemporal(sesion);
            ventana.ShowDialog(this);
        }

        private void AbrirInforme()
        {
            if (sesion is null) return;
            using var ventana = new VentanaInformeAtencion(sesion);
            ventana.ShowDialog(this);
        }

        private static string Numero(double? valor, string sufijo = "", int decimales = 1) =>
            valor.HasValue ? valor.Value.ToString(decimales == 0 ? "0" : "0.0") + sufijo : "—";

        private static string Porcentaje(double? valor) => valor.HasValue ? valor.Value.ToString("P1") : "—";

        private static string Ms(double? valor) => valor.HasValue ? $"{valor:0} ms" : "—";
    }
}
