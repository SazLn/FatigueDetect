using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public static class PersonalizacionConductor
    {
        public const int VersionActual = 2;
        private const long MinimoComparacionMs = 30_000;
        private const long MinimoReferenciaMs = 60_000;

        public static (ComparacionPersonal Comparacion, PerfilPersonalConductor? PerfilActualizado) Procesar(
            PerfilPersonalConductor? perfil, SesionConduccion sesion, DateTimeOffset ahora)
        {
            string cedula = NormalizarCedula(sesion.Conductor.Cedula);
            if (perfil is not null && !string.Equals(perfil.Cedula, cedula, StringComparison.OrdinalIgnoreCase))
                perfil = null;
            string camara = NormalizarContexto(sesion.CamaraId, "AUTOMATICA");
            string vehiculo = NormalizarContexto(sesion.Vehiculo.Placa, "SIN-VEHICULO");
            if (perfil is not null && perfil.CamaraId.Length > 0 &&
                (!string.Equals(perfil.CamaraId, camara, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(perfil.VehiculoClave, vehiculo, StringComparison.OrdinalIgnoreCase)))
                perfil = null;

            MetricasFatiga? fatiga = sesion.MetricasFatiga;
            double? parpadeos = fatiga?.ParpadeosPorMinuto;
            double? perclos = fatiga?.Perclos;
            double? atencion = sesion.Atencion is { } informe ? AnalisisAtencion.Promedio(informe.Muestras) : null;
            bool datosComparables = !string.IsNullOrWhiteSpace(cedula)
                && fatiga is { VersionMetricasOculares: >= 1, TiempoOcularValidoMs: >= MinimoComparacionMs }
                && parpadeos.HasValue && perclos.HasValue
                && sesion.Calidad?.Puntaje is >= 50 && sesion.CoberturaOcular is >= 0.5;

            bool calibradoAntes = perfil?.Calibrado == true;
            double? baseParpadeos = calibradoAntes ? perfil!.ParpadeosPorMinuto : null;
            double? basePerclos = calibradoAntes ? perfil!.Perclos : null;
            double? baseAtencion = calibradoAntes && perfil!.SesionesAtencion > 0 ? perfil.MediaAtencion : null;
            double? diferenciaParpadeos = datosComparables && baseParpadeos.HasValue ? parpadeos - baseParpadeos : null;
            double? diferenciaPerclos = datosComparables && basePerclos.HasValue ? perclos - basePerclos : null;
            double? diferenciaAtencion = datosComparables && baseAtencion.HasValue && atencion.HasValue
                ? atencion - baseAtencion : null;
            double umbralParpadeos = Math.Max(3, 2 * (perfil?.DesviacionParpadeos ?? 0));
            double umbralPerclos = Math.Max(0.05, 2 * (perfil?.DesviacionPerclos ?? 0));
            double umbralAtencion = Math.Max(10, 2 * (perfil?.DesviacionAtencion ?? 0));
            bool cambioParpadeos = diferenciaParpadeos is double dp && Math.Abs(dp) > umbralParpadeos;
            bool cambioPerclos = diferenciaPerclos is double dpc && dpc > umbralPerclos;
            bool cambioAtencion = diferenciaAtencion is double da && da < -umbralAtencion;
            bool anormal = cambioParpadeos || cambioPerclos || cambioAtencion;

            string exclusion = MotivoExclusion(sesion, cedula);
            bool referencia = exclusion.Length == 0;
            bool incorporar = referencia && !(calibradoAntes && anormal);
            PerfilPersonalConductor? actualizado = ActualizarCalibracion(perfil, cedula, camara, vehiculo,
                sesion.Calibracion, ahora);
            if (incorporar && fatiga is not null && parpadeos.HasValue && perclos.HasValue)
                actualizado = Incorporar(actualizado, cedula, camara, vehiculo, ahora,
                    fatiga, parpadeos.Value, atencion);

            string estado = datosComparables
                ? calibradoAntes
                    ? anormal ? "Cambio anormal respecto a la línea base; la sesión no modificó el perfil."
                              : referencia ? "Dentro de la variación personal; sesión incorporada al perfil."
                                           : $"Comparada, pero no incorporada al perfil: {exclusion}"
                    : incorporar ? $"Calibración en progreso ({actualizado!.SesionesValidas}/3 sesiones válidas)."
                                  : $"Sin línea base calibrada. {exclusion}"
                : $"Sin comparación personal: {(exclusion.Length > 0 ? exclusion : "datos insuficientes")}";

            var comparacion = new ComparacionPersonal(VersionActual, perfil?.SesionesValidas ?? 0,
                actualizado?.SesionesValidas ?? perfil?.SesionesValidas ?? 0, calibradoAntes,
                actualizado?.Calibrado == true, datosComparables ? parpadeos : null, baseParpadeos,
                diferenciaParpadeos, datosComparables ? perclos : null, basePerclos, diferenciaPerclos,
                datosComparables ? atencion : null, baseAtencion, diferenciaAtencion,
                umbralParpadeos, umbralPerclos, umbralAtencion, cambioParpadeos, cambioPerclos,
                cambioAtencion, incorporar, estado, camara, vehiculo,
                sesion.UmbralesAplicados?.SegundosSomnoliento,
                sesion.UmbralesAplicados?.SegundosAlarma);
            return (comparacion, actualizado);
        }

        public static UmbralesPersonalizados CalcularUmbrales(PerfilPersonalConductor? perfil,
            AjustesDeteccion configurados)
        {
            if (perfil?.Calibrado != true)
                return new(configurados.SegundosSomnoliento, configurados.SegundosAlarma, false,
                    "Se usan los umbrales configurados mientras se completa la línea base contextual.");

            double reduccionPerclos = perfil.Perclos is double perclos
                ? Math.Clamp((perclos - .10) * 2.0, 0, .30) : 0;
            double reduccionAtencion = perfil.SesionesAtencion > 0
                ? Math.Clamp((80 - perfil.MediaAtencion) / 100.0, 0, .20) : 0;
            double reduccion = Math.Max(reduccionPerclos, reduccionAtencion);
            if (reduccion < .05)
                return new(configurados.SegundosSomnoliento, configurados.SegundosAlarma, true,
                    "La línea base es estable; se conservan los umbrales configurados.");

            double alarma = Math.Max(AjustesDeteccion.MinimoAlarma,
                configurados.SegundosAlarma - reduccion);
            double somnolencia = Math.Max(AjustesDeteccion.MinimoSomnolencia,
                Math.Min(configurados.SegundosSomnoliento - reduccion, alarma - .25));
            return new(somnolencia, alarma, true,
                $"La línea base contextual anticipa las alertas {reduccion:0.00} s; nunca las retrasa.");
        }

        public static string Resumir(ComparacionPersonal? comparacion)
        {
            if (comparacion is null)
                return "Comparación personal: no disponible en esta sesión anterior.";
            string progreso = comparacion.PerfilCalibradoDespues
                ? $"Perfil calibrado con {comparacion.SesionesBaseDespues} sesiones válidas."
                : $"Calibración: {comparacion.SesionesBaseDespues}/3 sesiones válidas.";
            string contexto = comparacion.CamaraId.Length > 0
                ? $" Contexto: cámara {comparacion.CamaraId}, vehículo {comparacion.VehiculoClave}." : "";
            string umbrales = comparacion.UmbralSomnoliento.HasValue && comparacion.UmbralAlarma.HasValue
                ? $" Umbrales aplicados: somnolencia {comparacion.UmbralSomnoliento:0.00} s, alarma {comparacion.UmbralAlarma:0.00} s."
                : "";
            if (!comparacion.PerfilCalibradoAntes)
                return $"Comparación personal: aún sin línea base. {progreso}{contexto}{umbrales}\n{comparacion.Estado}";

            string ocular = $"Parpadeos/min: {Valor(comparacion.ParpadeosActual)} frente a {Valor(comparacion.ParpadeosBase)} " +
                $"({Delta(comparacion.DiferenciaParpadeos)}). PERCLOS: {Porcentaje(comparacion.PerclosActual)} frente a " +
                $"{Porcentaje(comparacion.PerclosBase)} ({DeltaPorcentaje(comparacion.DiferenciaPerclos)}).";
            string atencion = comparacion.AtencionActual.HasValue && comparacion.AtencionBase.HasValue
                ? $" Atención: {Valor(comparacion.AtencionActual)}/100 frente a {Valor(comparacion.AtencionBase)}/100 ({Delta(comparacion.DiferenciaAtencion)} puntos)."
                : " Atención personal: sin datos suficientes.";
            return $"Comparación personal experimental. {progreso}{contexto}{umbrales}\n{ocular}{atencion}\n{comparacion.Estado}";
        }

        private static PerfilPersonalConductor Incorporar(PerfilPersonalConductor? perfil, string cedula,
            string camara, string vehiculo, DateTimeOffset ahora, MetricasFatiga fatiga,
            double parpadeos, double? atencion)
        {
            perfil ??= new PerfilPersonalConductor(VersionActual, cedula, ahora, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0);
            int n = perfil.SesionesValidas + 1;
            double delta = parpadeos - perfil.MediaParpadeosSesion;
            double media = perfil.MediaParpadeosSesion + delta / n;
            double m2 = perfil.M2ParpadeosSesion + delta * (parpadeos - media);
            double perclos = fatiga.Perclos!.Value;
            double deltaPerclos = perclos - perfil.MediaPerclosSesion;
            double mediaPerclos = perfil.MediaPerclosSesion + deltaPerclos / n;
            double m2Perclos = perfil.M2PerclosSesion + deltaPerclos * (perclos - mediaPerclos);
            int nAtencion = perfil.SesionesAtencion;
            double mediaAtencion = perfil.MediaAtencion;
            double m2Atencion = perfil.M2Atencion;
            if (atencion is double a)
            {
                nAtencion++;
                double deltaAtencion = a - mediaAtencion;
                mediaAtencion += deltaAtencion / nAtencion;
                m2Atencion += deltaAtencion * (a - mediaAtencion);
            }
            return perfil with
            {
                Version = VersionActual,
                Cedula = cedula,
                Actualizado = ahora,
                SesionesValidas = n,
                TiempoOcularValidoMs = perfil.TiempoOcularValidoMs + fatiga.TiempoOcularValidoMs,
                TiempoOcularCerradoMs = perfil.TiempoOcularCerradoMs + fatiga.TiempoOcularCerradoMs,
                Parpadeos = perfil.Parpadeos + fatiga.Parpadeos,
                MediaParpadeosSesion = media,
                M2ParpadeosSesion = m2,
                MediaPerclosSesion = mediaPerclos,
                M2PerclosSesion = m2Perclos,
                SesionesAtencion = nAtencion,
                MediaAtencion = mediaAtencion,
                M2Atencion = m2Atencion,
                CamaraId = camara,
                VehiculoClave = vehiculo
            };
        }

        private static PerfilPersonalConductor? ActualizarCalibracion(PerfilPersonalConductor? perfil,
            string cedula, string camara, string vehiculo, CalibracionGuiada? calibracion,
            DateTimeOffset ahora)
        {
            if (calibracion is null)
                return perfil;
            perfil ??= new PerfilPersonalConductor(VersionActual, cedula, ahora, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, camara, vehiculo, calibracion);
            return perfil with
            {
                Version = VersionActual,
                Cedula = cedula,
                Actualizado = ahora,
                CamaraId = camara,
                VehiculoClave = vehiculo,
                Calibracion = calibracion
            };
        }

        private static string MotivoExclusion(SesionConduccion sesion, string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula)) return "falta la identificación del conductor.";
            if (sesion.MetricasFatiga is not { VersionMetricasOculares: >= 1 } fatiga)
                return "la medición ocular avanzada no está disponible.";
            if (fatiga.TiempoOcularValidoMs < MinimoReferenciaMs)
                return "se requieren al menos 60 s de medición ocular válida.";
            if (sesion.Calidad?.Puntaje is not >= 70)
                return "la calidad debe ser al menos 70/100.";
            if (sesion.CoberturaOcular is not >= 0.6)
                return "la cobertura ocular debe ser al menos 60 %.";
            if (sesion.CantidadAlarmas > 0 || fatiga.Microsuenos > 0 || fatiga.CierresAnomalos > 0)
                return "hubo alarmas, microsueños o cierres anómalos.";
            return string.Empty;
        }

        private static string NormalizarCedula(string? cedula) => (cedula ?? string.Empty).Trim().ToUpperInvariant();
        public static string NormalizarContexto(string? valor, string respaldo) =>
            string.IsNullOrWhiteSpace(valor) ? respaldo : valor.Trim().ToUpperInvariant();
        private static string Valor(double? valor) => valor?.ToString("0.0") ?? "sin medición";
        private static string Porcentaje(double? valor) => valor.HasValue ? $"{valor:P1}" : "sin medición";
        private static string Delta(double? valor) => valor.HasValue ? valor.Value.ToString("+0.0;-0.0;0.0") : "sin diferencia";
        private static string DeltaPorcentaje(double? valor) => valor.HasValue ? valor.Value.ToString("+0.0%;-0.0%;0.0%") : "sin diferencia";
    }
}
