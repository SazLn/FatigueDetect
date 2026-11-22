using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    internal sealed class RastreadorFatiga
    {
        private const long MaxSeparacionMs = 250;
        private const long VentanaMs = 60_000;
        private const long ParpadeoMinimoMs = 60;
        private const long ParpadeoCortoMaximoMs = 200;
        private const long ParpadeoMaximoMs = 400;
        private const long IntervaloMuestraPerclosMs = 10_000;
        private readonly SesionConduccion sesion;
        private readonly RastreadorPostura postura;
        public MetricasPostura ResumenPostura => postura.Resumen;

        private readonly Queue<IntervaloOcular> ventana = new();
        private readonly List<(double Izquierda, double Derecha)> muestras = new();
        private readonly List<AngulosCabeza> muestrasCabeza = new();
        private readonly List<MuestraPerclos> evolucionPerclos = new();
        private DateTimeOffset? inicioCalibracion, inicioCalibracionCabeza, ultimoOjo, ultimoCabeza;
        private DateTimeOffset? inicioCierre, inicioCabeceo, ultimaMuestraPerclos;
        private (double Izquierda, double Derecha)? referencia;
        private AngulosCabeza? posturaNeutra;
        private bool ojoPreviamenteAbierto, cierreComenzoAbierto, anteriorCerradoPerclos, cabezaEnNeutro;
        private long validoMs, cerradoMs, duracionParpadeosMs, parpadeoMinMs = long.MaxValue;
        private long parpadeoMaxMs, cierreMasLargoMs, microsuenoMasLargoMs;
        private int parpadeos, parpadeosCortos, parpadeosProlongados, cierresAnomalos, cierres, cabeceos;
        private double diferenciaAperturaAnterior, diferenciaAperturaAcumuladaMs;

        public RastreadorFatiga(SesionConduccion sesion, CalibracionGuiada? calibracion = null)
        {
            this.sesion = sesion;
            postura = new RastreadorPostura(sesion);
            if (calibracion is not null)
            {
                referencia = (calibracion.EarIzquierdoAbierto, calibracion.EarDerechoAbierto);
                posturaNeutra = calibracion.PosturaFrontal;
            }
        }

        public MetricasFatiga Resumen => CrearResumen(incluirEvolucion: true);

        public FatigaEnVivo Observar(DateTimeOffset instante, MedicionFacial? medicion, double umbralProlongado,
            bool rostroParcial = false)
        {
            double? izquierda = null, derecha = null, diferencia = null;
            long cierreMs = 0;
            if (medicion is MedicionFacial cara)
            {
                if (ultimoOjo is DateTimeOffset anterior && SeparacionInvalida(anterior, instante))
                    InterrumpirOjos(umbralProlongado);

                CalibrarOjos(instante, cara);
                if (referencia is { } abierta)
                {
                    izquierda = Apertura(cara.EarIzquierdo, abierta.Izquierda);
                    derecha = Apertura(cara.EarDerecho, abierta.Derecha);
                    diferencia = Math.Abs(izquierda.Value - derecha.Value);
                    cierreMs = ObservarOjos(instante, Math.Max(izquierda.Value, derecha.Value),
                        diferencia.Value, umbralProlongado);
                }
            }
            else
            {
                InterrumpirOjos(umbralProlongado);
            }

            AngulosCabeza? angulos = ObservarCabeza(instante, medicion?.Angulos);
            PosturaEnVivo lecturaPostura = postura.Observar(instante, medicion, angulos, rostroParcial);
            double? perclos = PerclosReciente(instante.ToUnixTimeMilliseconds());
            RegistrarMuestraPerclos(instante, perclos, forzar: false);
            return new FatigaEnVivo(izquierda, derecha, diferencia, perclos, cierreMs,
                angulos, CrearResumen(incluirEvolucion: false), lecturaPostura)
            {
                ProporcionDistraccion = postura.DistraccionReciente
            };
        }

        public void Interrumpir(DateTimeOffset instante, double umbralProlongado)
        {
            postura.Interrumpir();
            RegistrarMuestraPerclos(instante, PerclosReciente(instante.ToUnixTimeMilliseconds()), forzar: true);
            InterrumpirOjos(umbralProlongado);
            InterrumpirCabeza();
        }

        private void CalibrarOjos(DateTimeOffset instante, MedicionFacial cara)
        {
            if (referencia.HasValue)
                return;

            if (cara.EarIzquierdo < 0.15 || cara.EarDerecho < 0.15)
            {
                inicioCalibracion = null;
                muestras.Clear();
                ultimoOjo = instante;
                return;
            }

            inicioCalibracion ??= instante;
            muestras.Add((cara.EarIzquierdo, cara.EarDerecho));
            if (muestras.Count >= 8 && (instante - inicioCalibracion.Value).TotalMilliseconds >= 1500)
            {
                referencia = (Percentil75(muestras.Select(m => m.Izquierda)),
                    Percentil75(muestras.Select(m => m.Derecha)));
                muestras.Clear();
                ultimoOjo = null;
                return;
            }
            ultimoOjo = instante;
        }

        private long ObservarOjos(DateTimeOffset instante, double apertura, double diferenciaApertura,
            double umbralProlongado)
        {
            if (ultimoOjo is DateTimeOffset anterior)
            {
                long inicio = anterior.ToUnixTimeMilliseconds(), fin = instante.ToUnixTimeMilliseconds();
                long intervalo = fin - inicio;
                if (intervalo > 0)
                {
                    ventana.Enqueue(new IntervaloOcular(inicio, fin, anteriorCerradoPerclos));
                    validoMs += intervalo;
                    diferenciaAperturaAcumuladaMs += diferenciaAperturaAnterior * intervalo;
                    if (anteriorCerradoPerclos)
                        cerradoMs += intervalo;
                }
            }
            ultimoOjo = instante;
            anteriorCerradoPerclos = apertura <= 0.20;
            diferenciaAperturaAnterior = diferenciaApertura;

            // La banda de histéresis evita partir un cierre por ruido en el umbral.
            if (apertura >= 0.55)
            {
                TerminarCierre(instante, umbralProlongado, reabierto: true);
                ojoPreviamenteAbierto = true;
                return 0;
            }
            if (apertura <= 0.40 && !inicioCierre.HasValue)
            {
                inicioCierre = instante;
                cierreComenzoAbierto = ojoPreviamenteAbierto;
                ojoPreviamenteAbierto = false;
            }
            return inicioCierre is DateTimeOffset inicioCierreActual
                ? (long)(instante - inicioCierreActual).TotalMilliseconds : 0;
        }

        private void TerminarCierre(DateTimeOffset fin, double umbralProlongado, bool reabierto)
        {
            if (inicioCierre is not DateTimeOffset inicio)
                return;

            long duracion = Math.Max(0, (long)(fin - inicio).TotalMilliseconds);
            cierreMasLargoMs = Math.Max(cierreMasLargoMs, duracion);
            if (duracion >= umbralProlongado * 1000)
            {
                cierres++;
                microsuenoMasLargoMs = Math.Max(microsuenoMasLargoMs, duracion);
                sesion.RegistrarEvento(new EventoSesion(TipoEventoSesion.CierreOcularProlongado, inicio, fin));
            }
            else if (reabierto && cierreComenzoAbierto && duracion > ParpadeoMaximoMs)
            {
                cierresAnomalos++;
                sesion.RegistrarEvento(new EventoSesion(TipoEventoSesion.CierreOcularAnomalo, inicio, fin));
            }
            else if (reabierto && cierreComenzoAbierto && duracion >= ParpadeoMinimoMs)
            {
                parpadeos++;
                duracionParpadeosMs += duracion;
                parpadeoMinMs = Math.Min(parpadeoMinMs, duracion);
                parpadeoMaxMs = Math.Max(parpadeoMaxMs, duracion);
                if (duracion <= ParpadeoCortoMaximoMs)
                    parpadeosCortos++;
                else
                    parpadeosProlongados++;
            }
            inicioCierre = null;
        }

        private void InterrumpirOjos(double umbralProlongado)
        {
            if (ultimoOjo is DateTimeOffset fin)
                TerminarCierre(fin, umbralProlongado, reabierto: false);
            ultimoOjo = null;
            ojoPreviamenteAbierto = false;
            anteriorCerradoPerclos = false;
            inicioCalibracion = null;
            muestras.Clear();
        }

        private AngulosCabeza? ObservarCabeza(DateTimeOffset instante, AngulosCabeza? angulos)
        {
            if (ultimoCabeza is DateTimeOffset anterior && SeparacionInvalida(anterior, instante))
                InterrumpirCabeza();
            if (angulos is not { } cabeza)
            {
                InterrumpirCabeza();
                return null;
            }
            ultimoCabeza = instante;

            if (posturaNeutra is not { } neutra)
            {
                if (Math.Abs(cabeza.Horizontal) > 30 || Math.Abs(cabeza.Lateral) > 25)
                {
                    InterrumpirCabeza();
                    return null;
                }
                inicioCalibracionCabeza ??= instante;
                muestrasCabeza.Add(cabeza);
                if (muestrasCabeza.Count >= 8 && (instante - inicioCalibracionCabeza.Value).TotalMilliseconds >= 1500)
                {
                    double[] verticales = muestrasCabeza.Select(m => m.Vertical).Order().ToArray();
                    double[] horizontales = muestrasCabeza.Select(m => m.Horizontal).Order().ToArray();
                    double[] laterales = muestrasCabeza.Select(m => m.Lateral).Order().ToArray();
                    if (verticales[^1] - verticales[0] <= 12 && horizontales[^1] - horizontales[0] <= 12
                        && laterales[^1] - laterales[0] <= 12)
                    {
                        int mitad = muestrasCabeza.Count / 2;
                        posturaNeutra = new AngulosCabeza(verticales[mitad], horizontales[mitad], laterales[mitad]);
                    }
                    inicioCalibracionCabeza = null;
                    muestrasCabeza.Clear();
                }
                return null;
            }

            var relativos = new AngulosCabeza(cabeza.Vertical - neutra.Vertical,
                cabeza.Horizontal - neutra.Horizontal, cabeza.Lateral - neutra.Lateral);
            if (Math.Abs(relativos.Horizontal) > 30 || Math.Abs(relativos.Lateral) > 25)
            {
                InterrumpirCabeza();
                return relativos;
            }

            double descenso = relativos.Vertical;
            if (Math.Abs(descenso) <= 6)
            {
                if (inicioCabeceo is DateTimeOffset inicio && (instante - inicio).TotalMilliseconds is >= 150 and <= 1500)
                {
                    cabeceos++;
                    sesion.RegistrarEvento(new EventoSesion(TipoEventoSesion.Cabeceo, inicio, instante));
                }
                inicioCabeceo = null;
                cabezaEnNeutro = true;
            }
            else if (cabezaEnNeutro && descenso >= 15)
            {
                inicioCabeceo = instante;
                cabezaEnNeutro = false;
            }
            else if (descenso < -6 || inicioCabeceo is DateTimeOffset caida && (instante - caida).TotalMilliseconds > 1500)
            {
                inicioCabeceo = null;
                cabezaEnNeutro = false;
            }
            return relativos;
        }

        private void InterrumpirCabeza()
        {
            ultimoCabeza = inicioCalibracionCabeza = inicioCabeceo = null;
            cabezaEnNeutro = false;
            muestrasCabeza.Clear();
        }

        private double? PerclosReciente(long instante)
        {
            long limite = instante - VentanaMs;
            while (ventana.TryPeek(out var primero) && primero.Fin <= limite)
                ventana.Dequeue();

            long valido = 0, cerrado = 0;
            foreach (var intervalo in ventana)
            {
                long duracion = intervalo.Fin - Math.Max(intervalo.Inicio, limite);
                valido += duracion;
                if (intervalo.Cerrado)
                    cerrado += duracion;
            }
            return valido >= 2000 ? (double)cerrado / valido : null;
        }

        private void RegistrarMuestraPerclos(DateTimeOffset instante, double? perclos, bool forzar)
        {
            if (perclos is not double valor)
                return;
            if (ultimaMuestraPerclos is DateTimeOffset anterior)
            {
                long separacion = (long)(instante - anterior).TotalMilliseconds;
                if (separacion <= 0)
                    return;
                if (forzar && separacion < 1000)
                {
                    evolucionPerclos[^1] = new MuestraPerclos(instante, valor);
                    ultimaMuestraPerclos = instante;
                    return;
                }
                if (!forzar && separacion < IntervaloMuestraPerclosMs)
                    return;
            }

            evolucionPerclos.Add(new MuestraPerclos(instante, valor));
            ultimaMuestraPerclos = instante;
        }

        private MetricasFatiga CrearResumen(bool incluirEvolucion)
        {
            double? diferenciaPromedio = validoMs >= 2000
                ? diferenciaAperturaAcumuladaMs / validoMs : null;
            return new MetricasFatiga(parpadeos, cierres, cabeceos, validoMs, cerradoMs,
                VersionMetricasOculares: 1,
                ParpadeosCortos: parpadeosCortos,
                ParpadeosProlongados: parpadeosProlongados,
                CierresAnomalos: cierresAnomalos,
                DuracionParpadeosTotalMs: duracionParpadeosMs,
                DuracionParpadeoMinMs: parpadeos > 0 ? parpadeoMinMs : null,
                DuracionParpadeoMaxMs: parpadeos > 0 ? parpadeoMaxMs : null,
                CierreMasLargoMs: cierreMasLargoMs,
                MicrosuenoMasLargoMs: microsuenoMasLargoMs,
                DiferenciaAperturaPromedio: diferenciaPromedio)
            {
                EvolucionPerclos = incluirEvolucion
                    ? evolucionPerclos.ToArray() : Array.Empty<MuestraPerclos>()
            };
        }

        private static bool SeparacionInvalida(DateTimeOffset anterior, DateTimeOffset actual) =>
            actual < anterior || (actual - anterior).TotalMilliseconds > MaxSeparacionMs;

        private static double Percentil75(IEnumerable<double> valores)
        {
            var ordenados = valores.Order().ToArray();
            return ordenados[(ordenados.Length - 1) * 3 / 4];
        }

        private static double Apertura(double ear, double referenciaAbierta)
        {
            // Referencia de cierre aproximada. EAR mide distancia entre párpados, no área pupilar.
            double referenciaCerrada = 0.35 * referenciaAbierta;
            return Math.Clamp((ear - referenciaCerrada) / (referenciaAbierta - referenciaCerrada), 0, 1);
        }

        private readonly record struct IntervaloOcular(long Inicio, long Fin, bool Cerrado);
    }
}
