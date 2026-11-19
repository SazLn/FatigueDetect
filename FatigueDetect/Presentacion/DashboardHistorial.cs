using FatigueDetect.Logica;

namespace FatigueDetect.Presentacion
{
    internal sealed class DashboardHistorial : Form
    {
        public DashboardHistorial(ResumenHistorial resumen)
        {
            Text = "Estadísticas del historial";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 520);
            MinimumSize = new Size(520, 360);
            BackColor = Tema.Fondo;
            Padding = new Padding(16);

            var contenido = Tema.TablaFilas(Tema.Fondo, 48, 0);
            string promedio = resumen.PromedioSegundos is double segundos
                ? Tema.FormatoDuracion(TimeSpan.FromSeconds(segundos)) : "—";
            var encabezado = Tema.Etiqueta(
                $"Sesiones: {resumen.TotalSesiones}   ·   Duración promedio: {promedio}",
                13, Tema.Texto, Tema.Fondo, FontStyle.Bold);
            contenido.Controls.Add(encabezado, 0, 0);

            var pestañas = new TabControl { Dock = DockStyle.Fill };
            Agregar(pestañas, "Por día", "Fecha", resumen.PorDia());
            Agregar(pestañas, "Por semana", "Lunes de inicio", resumen.PorSemana());
            Agregar(pestañas, "Por mes", "Mes", resumen.PorMes());
            Agregar(pestañas, "Horas de mayor uso", "Hora de inicio", resumen.PorHora());
            contenido.Controls.Add(pestañas, 0, 1);
            Controls.Add(contenido);
        }

        private static void Agregar(TabControl pestañas, string nombre, string periodo,
            IEnumerable<ResumenPeriodo> filas)
        {
            var pagina = new TabPage(nombre) { BackColor = Tema.Tarjeta };
            var tabla = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Tema.Tarjeta,
                BorderStyle = BorderStyle.None
            };
            tabla.Columns.Add("periodo", periodo);
            tabla.Columns.Add("sesiones", "Sesiones");
            tabla.Columns.Add("promedio", "Duración promedio");
            foreach (ResumenPeriodo fila in filas)
            {
                tabla.Rows.Add(fila.Periodo, fila.Sesiones,
                    fila.PromedioSegundos is double segundos
                        ? Tema.FormatoDuracion(TimeSpan.FromSeconds(segundos)) : "—");
            }

            if (tabla.Rows.Count == 0)
                pagina.Controls.Add(Tema.Etiqueta("Sin sesiones registradas", 12, Tema.TextoSuave, Tema.Tarjeta));
            else
                pagina.Controls.Add(tabla);

            pestañas.TabPages.Add(pagina);
        }
    }
}
