using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public static class AnalisisConfiabilidadEvento
    {
        private static bool EsPose(TipoEventoSesion tipo) => tipo is
            TipoEventoSesion.Cabeceo or TipoEventoSesion.GiroIzquierda or TipoEventoSesion.GiroDerecha
            or TipoEventoSesion.CabezaArriba or TipoEventoSesion.CabezaAbajo or TipoEventoSesion.FueraZonaFrontal
            or TipoEventoSesion.PosiblePosturaEncorvada or TipoEventoSesion.RostroParcial or TipoEventoSesion.PosibleBostezo;

        public const int VersionActual = 1;

        public static void Completar(SesionConduccion sesion)
        {
            sesion.EstablecerEventos(sesion.Eventos.Select(evento => evento.Confiabilidad is null
                ? evento with { Confiabilidad = Evaluar(evento, sesion) }
                : evento));
        }

        public static EvaluacionConfiabilidadEvento Evaluar(EventoSesion evento, SesionConduccion sesion)
        {
            if (evento.Tipo == TipoEventoSesion.EventoExterno)
                return SinMedicion("El detector externo no proporcionó una evaluación de confiabilidad.");
            if (sesion.Calidad is not CalidadSesion calidad)
                return SinMedicion("La sesión no contiene métricas de calidad compatibles.");

            double persistencia = Persistencia(evento);
            var factores = new List<FactorConfiabilidadEvento>();
            var advertencias = new List<string>();
            if (EsOcular(evento.Tipo))
            {
                Agregar(factores, "Calidad de sesión", Normalizar(calidad.Puntaje), 0.35);
                Agregar(factores, "Cobertura ocular", sesion.CoberturaOcular, 0.40);
                Agregar(factores, "Persistencia temporal", persistencia, 0.25);
            }
            else if (EsPose(evento.Tipo))
            {
                Agregar(factores, "Calidad de sesión", Normalizar(calidad.Puntaje), 0.40);
                Agregar(factores, "Cobertura facial", calidad.CoberturaRostro, 0.35);
                Agregar(factores, "Persistencia temporal", persistencia, 0.25);
            }
            else if (EsProblemaCamara(evento.Tipo))
            {
                Agregar(factores, "Persistencia temporal", persistencia, 0.70);
                Agregar(factores, "Estabilidad de FPS", EstabilidadFps(calidad), 0.15);
                Agregar(factores, "Continuidad de captura", Continuidad(calidad), 0.15);
            }
            else
            {
                Agregar(factores, "Persistencia temporal", persistencia, 0.55);
                Agregar(factores, "Estabilidad de FPS", EstabilidadFps(calidad), 0.25);
                Agregar(factores, "Continuidad de captura", Continuidad(calidad), 0.20);
            }

            if (factores.Count == 0)
                return SinMedicion("No hay suficientes componentes para evaluar este evento.");

            double peso = factores.Sum(f => f.Peso);
            double puntaje = 100 * factores.Sum(f => f.Valor * f.Peso) / peso;
            bool faltaCalidad = (EsOcular(evento.Tipo) || EsPose(evento.Tipo))
                && calidad.Puntaje is null;
            bool faltaCobertura = (EsOcular(evento.Tipo) && sesion.CoberturaOcular is null)
                || (EsPose(evento.Tipo) && calidad.CoberturaRostro is null);
            bool faltaFps = !EsOcular(evento.Tipo) && !EsPose(evento.Tipo)
                && EstabilidadFps(calidad) is null;
            if (factores.Any(f => f.Valor < 0.40))
                puntaje = Math.Min(puntaje, 59);
            else if (faltaCalidad || faltaCobertura || faltaFps
                || factores.Any(f => f.Valor < UmbralAdvertencia(f.Nombre)))
                puntaje = Math.Min(puntaje, 79);

            if (faltaCalidad)
                advertencias.Add("No se pudo calcular la calidad global de la sesión.");
            if (EsOcular(evento.Tipo) && sesion.CoberturaOcular is null)
                advertencias.Add("No se pudo calcular la cobertura ocular de la sesión.");
            if (EsPose(evento.Tipo) && calidad.CoberturaRostro is null)
                advertencias.Add("No se pudo calcular la cobertura facial de la sesión.");
            if (faltaFps)
                advertencias.Add("La sesión fue demasiado corta para evaluar la estabilidad de FPS.");
            foreach (FactorConfiabilidadEvento factor in factores)
            {
                if (factor.Valor >= UmbralAdvertencia(factor.Nombre))
                    continue;
                advertencias.Add(Advertencia(factor.Nombre));
            }
            if (calidad.FallosCamara > 0 && !advertencias.Any(a => a.Contains("captura", StringComparison.OrdinalIgnoreCase)))
                advertencias.Add($"La sesión registró {calidad.FallosCamara} fallo(s) de captura.");
            if (advertencias.Count == 0)
                advertencias.Add("Los datos disponibles son consistentes para esta estimación.");
            return new EvaluacionConfiabilidadEvento(VersionActual, Math.Clamp(puntaje, 0, 100),
                factores, advertencias);
        }

        public static string Resumir(SesionConduccion sesion)
        {
            EventoSesion[] eventos = sesion.Eventos.ToArray();
            if (eventos.Length == 0)
                return "Confiabilidad de eventos: no hubo eventos para evaluar.\n" + ExplicarMedicion(sesion);
            int alta = eventos.Count(e => e.Confiabilidad?.Nivel == "Alta");
            int media = eventos.Count(e => e.Confiabilidad?.Nivel == "Media");
            int baja = eventos.Count(e => e.Confiabilidad?.Nivel == "Baja");
            int ausente = eventos.Length - alta - media - baja;
            string sinMedicion = ausente > 0 ? $" · Sin medición: {ausente}" : string.Empty;
            return $"Confiabilidad de eventos — Alta: {alta} · Media: {media} · Baja: {baja}{sinMedicion}.\n" +
                ExplicarMedicion(sesion);
        }

        public static string ExplicarMedicion(SesionConduccion sesion)
        {
            if (sesion.Calidad is not CalidadSesion calidad)
                return "Datos poco confiables: la sesión no contiene métricas de calidad.";
            var razones = new List<string>();
            if (calidad.Puntaje is not double puntaje)
                razones.Add("no se pudo calcular el puntaje de calidad");
            else if (puntaje < CalidadSesion.UmbralCalidadBaja)
                razones.Add($"calidad global baja ({puntaje:0}/100)");
            if (calidad.ProporcionAnalizable is double analizables && analizables < 0.70)
                razones.Add($"solo {analizables:P0} de los fotogramas fueron analizables");
            if (calidad.CoberturaRostro is double rostro && rostro < 0.70)
                razones.Add($"cobertura facial de {rostro:P0}");
            if (sesion.CoberturaOcular is double ocular && ocular < 0.70)
                razones.Add($"cobertura ocular de {ocular:P0}");
            if (EstabilidadFps(calidad) is double fps && fps < 0.80)
                razones.Add($"FPS estables durante {fps:P0} del tiempo evaluado");
            if (calidad.FallosCamara > 0)
                razones.Add($"{calidad.FallosCamara} fallo(s) de captura");
            return razones.Count == 0
                ? "No se observaron limitaciones importantes en las métricas generales disponibles."
                : "Factores que reducen la confiabilidad: " + string.Join("; ", razones) + ".";
        }

        public static string Describir(EvaluacionConfiabilidadEvento? evaluacion)
        {
            if (evaluacion is null)
                return "Sin medición (sesión anterior).";
            string puntaje = evaluacion.Puntaje is double valor ? $"{valor:0}/100 · {evaluacion.Nivel}" : evaluacion.Nivel;
            return puntaje + " — " + string.Join(" ", evaluacion.Advertencias);
        }

        private static EvaluacionConfiabilidadEvento SinMedicion(string razon) =>
            new(VersionActual, null, Array.Empty<FactorConfiabilidadEvento>(), [razon]);

        private static void Agregar(List<FactorConfiabilidadEvento> factores, string nombre,
            double? valor, double peso)
        {
            if (valor is double v && double.IsFinite(v))
                factores.Add(new FactorConfiabilidadEvento(nombre, Math.Clamp(v, 0, 1), peso));
        }

        private static double? Normalizar(double? puntaje) => puntaje / 100;

        private static double? EstabilidadFps(CalidadSesion calidad) => calidad.TiempoEvaluadoFpsMs is > 0
            ? 1 - Math.Clamp((double)calidad.TiempoFpsBajoMs.GetValueOrDefault() / calidad.TiempoEvaluadoFpsMs.Value, 0, 1)
            : null;

        private static double Continuidad(CalidadSesion calidad) =>
            Math.Clamp(1 - calidad.FallosCamara * 0.20, 0.40, 1);

        private static double Persistencia(EventoSesion evento)
        {
            double objetivoMs = evento.Tipo switch
            {
                TipoEventoSesion.CierreOcularAnomalo => 800,
                TipoEventoSesion.Cabeceo => 750,
                TipoEventoSesion.CierreOcularProlongado => 3000,
                TipoEventoSesion.Alarma => 3000,
                _ => 5000
            };
            return evento.DuracionMilisegundos is long ms ? Math.Clamp(ms / objetivoMs, 0, 1) : 0;
        }

        private static bool EsOcular(TipoEventoSesion tipo) => tipo is TipoEventoSesion.CierreOcularProlongado
            or TipoEventoSesion.CierreOcularAnomalo or TipoEventoSesion.Alarma;

        private static bool EsProblemaCamara(TipoEventoSesion tipo) => tipo is TipoEventoSesion.CamaraTapada
            or TipoEventoSesion.CamaraDesenfocada or TipoEventoSesion.CamaraCongelada;

        private static double UmbralAdvertencia(string nombre) => nombre == "Estabilidad de FPS" ? 0.80 : 0.70;

        private static string Advertencia(string nombre) => nombre switch
        {
            "Calidad de sesión" => "La calidad global de la sesión fue inferior a 70/100.",
            "Cobertura ocular" => "Los ojos estuvieron medibles durante menos del 70 % de la sesión.",
            "Cobertura facial" => "El rostro estuvo disponible durante menos del 70 % de la sesión.",
            "Persistencia temporal" => "La evidencia temporal fue breve; conviene confirmar el evento visualmente.",
            "Estabilidad de FPS" => "Hubo poca estabilidad de FPS durante la sesión.",
            "Continuidad de captura" => "Los fallos de captura reducen la continuidad de la evidencia.",
            _ => $"El factor {nombre} tuvo una medición baja."
        };
    }
}
