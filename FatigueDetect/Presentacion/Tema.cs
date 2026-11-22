using System;
using System.Drawing;
using System.Windows.Forms;

namespace FatigueDetect.Presentacion
{
    /// <summary>Colores, fuentes y utilidades para construir las pantallas.</summary>
    public static class Tema
    {
        public static readonly Color Fondo = Color.FromArgb(20, 17, 31);
        public static readonly Color Lateral = Color.FromArgb(16, 13, 25);
        public static readonly Color Tarjeta = Color.FromArgb(29, 24, 48);
        public static readonly Color TarjetaClara = Color.FromArgb(39, 33, 56);
        public static readonly Color Borde = Color.FromArgb(53, 46, 77);
        public static readonly Color Texto = Color.FromArgb(240, 235, 252);
        public static readonly Color TextoSuave = Color.FromArgb(167, 158, 196);
        public static readonly Color Azul = Color.FromArgb(132, 102, 240);
        public static readonly Color AzulOscuro = Color.FromArgb(103, 76, 211);
        public static readonly Color Verde = Color.FromArgb(126, 224, 195);
        public static readonly Color Ambar = Color.FromArgb(255, 184, 107);
        public static readonly Color Rojo = Color.FromArgb(220, 75, 116);
        public static readonly Color TextoSobreAcento = Color.FromArgb(20, 15, 38);

        public static Font Fuente(float tamano, FontStyle estilo = FontStyle.Regular)
        {
            return new Font("Segoe UI", tamano, estilo);
        }

        public static Label Etiqueta(string texto, float tamano, Color color, Color fondo,
            FontStyle estilo = FontStyle.Regular,
            ContentAlignment alineacion = ContentAlignment.MiddleLeft)
        {
            return new Label
            {
                Text = texto,
                Font = Fuente(tamano, estilo),
                ForeColor = color,
                BackColor = fondo,
                TextAlign = alineacion,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Margin = new Padding(0)
            };
        }

        public static Button Boton(string texto, Color fondo, Color letra)
        {
            if (fondo.ToArgb() == Azul.ToArgb() || fondo.ToArgb() == Verde.ToArgb() ||
                fondo.ToArgb() == Ambar.ToArgb() || fondo.ToArgb() == Rojo.ToArgb())
            {
                letra = TextoSobreAcento;
            }

            var boton = new Button
            {
                Text = texto,
                BackColor = fondo,
                ForeColor = letra,
                FlatStyle = FlatStyle.Flat,
                Font = Fuente(10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Height = 40,
                Width = 180,
                Margin = new Padding(0, 0, 10, 0)
            };
            boton.FlatAppearance.BorderSize = 0;
            return boton;
        }

        /// <summary>
        /// Tabla de una columna con las alturas indicadas (en píxeles).
        /// Un valor 0 significa "ocupar el espacio que sobre".
        /// </summary>
        public static TableLayoutPanel TablaFilas(Color fondo, params int[] alturas)
        {
            var tabla = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = alturas.Length,
                BackColor = fondo,
                Margin = new Padding(0)
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            foreach (int altura in alturas)
            {
                tabla.RowStyles.Add(altura > 0
                    ? new RowStyle(SizeType.Absolute, altura)
                    : new RowStyle(SizeType.Percent, 100));
            }

            return tabla;
        }

        public static string FormatoDuracion(TimeSpan d)
        {
            return $"{(int)d.TotalHours:00}:{d.Minutes:00}:{d.Seconds:00}";
        }
    }
}
