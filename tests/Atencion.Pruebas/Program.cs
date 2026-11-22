using FatigueDetect.Entidades;
using FatigueDetect.Logica;
using FatigueDetect.Datos;
using Microsoft.Data.Sqlite;
using ClosedXML.Excel;
using FatigueDetect.Presentacion;

int comprobaciones = 0;
void Verificar(bool condicion, string nombre)
{
    if (!condicion) throw new Exception(nombre);
    comprobaciones++;
}
var sano = AnalisisAtencion.Calcular(0, 0, 0, true, null, 90, 1);
Verificar(sano.Puntaje == 100 && sano.Factores.All(f => f.Descuento == 0), "Atención sana");
var riesgo = AnalisisAtencion.Calcular(0.4, 3, 3, true, 0.3, 90, 1);
Verificar(riesgo.Puntaje == 0 && Math.Abs(riesgo.Factores.Sum(f => f.Descuento) - 100) < .001, "Riesgo máximo y descuentos");
Verificar(AnalisisAtencion.Calcular(0, 0, 0, true, null, 49, 1).Puntaje is null, "Calidad insuficiente");
Verificar(AnalisisAtencion.Calcular(0, 0, 0, true, null, 90, .49).Puntaje is null, "Cobertura insuficiente");
Verificar(AnalisisAtencion.Calcular(null, 0, 0, true, null, 90, 1).Puntaje is null, "Ojos sin medición");
Verificar(AnalisisAtencion.Calcular(0, 0, 0, false, null, 90, 1).Factores.Count == 2, "Cabeza y distracción desconocidas no se inventan");
Verificar(AnalisisAtencion.Calcular(0, 0, 0, true, .3, 90, 1).Puntaje == 85, "Contrato de distracción aporta descuento");

var instante = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
var sesion = new SesionConduccion { Inicio = instante.LocalDateTime, Fin = instante.AddSeconds(30).LocalDateTime };
var rastreador = new RastreadorAtencion();
void Observar(int ms, double apertura, EyeState estado = EyeState.DESPIERTO, long cierre = 0, int cabeceos = 0)
{
    var fatiga = new MetricasFatiga(0, 0, cabeceos, ms, apertura < .4 ? ms / 2 : 0);
    rastreador.Observar(instante.AddMilliseconds(ms), new FatigaEnVivo(apertura, apertura, 0,
        ms >= 2000 ? (apertura < .4 ? .4 : 0) : null, cierre, new AngulosCabeza(0, 0, 0), fatiga), estado,
        new CalidadSesion(ms / 50, ms / 50, ms, ms, 0, 0, 0, ms), sesion, 1.5);
}
for (int ms = 0; ms <= 5000; ms += 100) Observar(ms, 1);
for (int ms = 5100; ms <= 8000; ms += 100)
    Observar(ms, 0, ms >= 7000 ? EyeState.ALARMA_ACTIVADA : EyeState.SOMNOLIENTO, ms - 5000);
for (int ms = 8100; ms <= 11000; ms += 100) Observar(ms, 1);
rastreador.Interrumpir(instante.AddSeconds(11));
var informe = rastreador.Resumen();
Verificar(informe.PrimerSintomaActivoMs == 5000, "Inicio real del cierre, no instante de confirmación");
Verificar(informe.Respuestas.Count == 1 && informe.Respuestas[0].RespuestaMs == 1100, "Respuesta desde inicio de alarma");
Verificar(informe.Respuestas[0].RecuperacionMs == 3100, "Recuperación tras 2 s estables");
Verificar(informe.Muestras.All(m => m.Puntaje is null or >= 0 and <= 100), "Puntaje acotado");
Verificar(informe.Muestras.Sum(m => m.PesoMs) == 11000, "Sin duplicar intervalos");

// Una interrupción no se convierte en una recuperación ni rellena el hueco con datos sanos.
for (int ms = 12000; ms <= 14000; ms += 100) Observar(ms, 0, EyeState.ALARMA_ACTIVADA, 2000);
rastreador.Interrumpir(instante.AddSeconds(14));
Verificar(rastreador.Resumen().Respuestas[^1].RecuperacionMs is null, "Alarma interrumpida sin recuperación ficticia");
long pesoAntes = rastreador.Resumen().Muestras.Sum(m => m.PesoMs);
Observar(30000, 1);
Verificar(rastreador.Resumen().Muestras.Sum(m => m.PesoMs) == pesoAntes, "Pérdida de imagen excluida");
var pausa = new RastreadorAtencion();
var calidadPausa = new CalidadSesion(100, 100, 5000, 5000, 0, 0, 0, 5000);
var lecturaPausa = new FatigaEnVivo(1, 1, 0, 0, 0, new AngulosCabeza(0, 0, 0), new MetricasFatiga(0, 0, 0, 5000, 0));
pausa.Observar(instante, lecturaPausa, EyeState.DESPIERTO, calidadPausa, sesion, 1.5);
pausa.Interrumpir(instante);
pausa.Observar(instante.AddMinutes(5), lecturaPausa, EyeState.DESPIERTO, calidadPausa, sesion, 1.5);
Verificar(pausa.Resumen().Muestras[^1].ActivoMs == 5000 && pausa.Resumen().Muestras[^1].PesoMs == 0, "Pausa excluye tiempo activo y peso");
var perdida = new RastreadorAtencion();
perdida.Observar(instante, lecturaPausa with { AperturaIzquierda = 0, AperturaDerecha = 0 },
    EyeState.ALARMA_ACTIVADA, calidadPausa, sesion, 1.5);
perdida.Observar(instante.AddMilliseconds(100), lecturaPausa with { AperturaIzquierda = null, AperturaDerecha = null },
    EyeState.DESPIERTO, calidadPausa, sesion, 1.5);
for (int ms = 200; ms <= 3000; ms += 100)
    perdida.Observar(instante.AddMilliseconds(ms), lecturaPausa, EyeState.DESPIERTO, calidadPausa, sesion, 1.5);
Verificar(perdida.Resumen().Respuestas[0].RecuperacionMs is null && perdida.Resumen().Respuestas[0].RespuestaMs is null,
    "Pérdida de rostro interrumpe respuesta pendiente aunque después vuelva al encuadre");

var muestras = Enumerable.Range(1, 120).Select(i => new MuestraAtencion(instante.AddSeconds(i), i * 1000, 1000,
    i <= 60 ? 90 : 30, 90, false, Array.Empty<FactorAtencion>())).ToArray();
var tendencia = new InformeAtencion(1, null, muestras, Array.Empty<RespuestaAlarma>());
Verificar(AnalisisAtencion.PeorMinuto(tendencia)?.Minuto == 2, "Minuto de mayor fatiga");
Verificar(AnalisisAtencion.Tendencia(tendencia).StartsWith("Empeora"), "Tendencia temporal");
Verificar(AnalisisAtencion.Tendencia(informe).StartsWith("Sin datos"), "Sesión corta sin tendencia inventada");
Verificar(AnalisisAtencion.Promedio(muestras) == 60, "Promedio ponderado");
Verificar(AnalisisAtencion.Resumir(null).Contains("sin medición"), "Historial antiguo");

var sesionConfiable = new SesionConduccion
{
    Calidad = new CalidadSesion(1200, 1200, 60_000, 60_000, 0, 0, 0, 60_000),
    MetricasFatiga = new MetricasFatiga(12, 1, 0, 60_000, 6_000, VersionMetricasOculares: 1)
};
sesionConfiable.RegistrarEvento(new EventoSesion(TipoEventoSesion.CierreOcularProlongado,
    instante, instante.AddSeconds(3)));
AnalisisConfiabilidadEvento.Completar(sesionConfiable);
var confianzaAlta = sesionConfiable.Eventos.Single().Confiabilidad!;
Verificar(confianzaAlta is { Nivel: "Alta", Puntaje: >= 80 }, "Evento con datos consistentes obtiene confianza alta");
Verificar(confianzaAlta.Factores.Count == 3 && confianzaAlta.Advertencias.Single().StartsWith("Los datos"),
    "Confianza conserva factores y explicación favorable");

var sesionDebil = new SesionConduccion
{
    Calidad = new CalidadSesion(100, 20, 60_000, 12_000, 2, 1, 48_000, 60_000),
    MetricasFatiga = new MetricasFatiga(2, 0, 0, 12_000, 2_000, VersionMetricasOculares: 1)
};
sesionDebil.RegistrarEvento(new EventoSesion(TipoEventoSesion.CierreOcularAnomalo,
    instante, instante.AddMilliseconds(500)));
AnalisisConfiabilidadEvento.Completar(sesionDebil);
var confianzaBaja = sesionDebil.Eventos.Single().Confiabilidad!;
Verificar(confianzaBaja is { Nivel: "Baja", Puntaje: < 60 }, "Evento con poca cobertura obtiene confianza baja");
Verificar(confianzaBaja.Advertencias.Any(a => a.Contains("ojos")) &&
    AnalisisConfiabilidadEvento.ExplicarMedicion(sesionDebil).Contains("Factores que reducen"),
    "Se explican cobertura y limitaciones generales");
var confianzaPropia = new EvaluacionConfiabilidadEvento(9, 99,
    Array.Empty<FactorConfiabilidadEvento>(), ["Detector futuro"]);
var externo = new SesionConduccion();
externo.RegistrarEvento(new EventoSesion(TipoEventoSesion.EventoExterno, instante, instante.AddSeconds(1))
    { CodigoTipo = "EventoFuturo", Confiabilidad = confianzaPropia });
AnalisisConfiabilidadEvento.Completar(externo);
Verificar(externo.Eventos.Single().Confiabilidad == confianzaPropia,
    "No se reemplaza la confianza aportada por otro detector");

string carpeta = Path.Combine(Path.GetTempPath(), "fatiguedetect-atencion-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpeta);
try
{
    string ruta = Path.Combine(carpeta, "historial.sqlite");
    var repo = new HistorialSqliteRepository(ruta, Path.Combine(carpeta, "ausente.csv"));
    // Simula una base previa a estas entregas: sin atención ni confianza por evento.
    repo.LeerTodas();
    using (var conexion = new SqliteConnection($"Data Source={ruta}"))
    {
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DROP TABLE AtencionSesiones; ALTER TABLE Eventos DROP COLUMN Confiabilidad;";
        comando.ExecuteNonQuery();
    }
    repo = new HistorialSqliteRepository(ruta, Path.Combine(carpeta, "ausente.csv"));
    var antigua = new SesionConduccion { Inicio = instante.LocalDateTime, Fin = instante.AddMinutes(1).LocalDateTime };
    repo.Guardar(antigua);
    sesion.Atencion = informe;
    sesion.RegistrarEvento(new EventoSesion(TipoEventoSesion.EventoExterno, instante, instante.AddSeconds(2))
        { CodigoTipo = "DistraccionFutura", Confiabilidad = confianzaPropia });
    repo.Guardar(sesion);
    var leidas = repo.LeerTodas();
    Verificar(leidas.Single(s => s.Id == antigua.Id).Atencion is null, "Sesión antigua conserva ausencia de medición");
    var leida = leidas.Single(s => s.Id == sesion.Id);
    Verificar(leida.Atencion?.Muestras.Count == informe.Muestras.Count && leida.Atencion.Respuestas[0] == informe.Respuestas[0], "Persistencia de muestras y respuestas");
    Verificar(leida.Eventos[0].CodigoTipo == "DistraccionFutura", "Eventos futuros preservados");
    Verificar(leida.Eventos[0].Confiabilidad is { Version: 9, Puntaje: 99 } confianzaLeida &&
        confianzaLeida.Advertencias.Single() == "Detector futuro",
        "Persistencia de confianza y explicación del evento");
    string excel = Path.Combine(carpeta, "informe.xlsx");
    ExportadorHistorialExcel.Guardar(excel, leidas);
    using (var libro = new XLWorkbook(excel))
    {
        Verificar(libro.Worksheet("Atención").LastRowUsed()!.RowNumber() == 3, "Excel incluye sesiones nuevas y antiguas");
        Verificar(libro.Worksheet("Evolución atención").LastRowUsed()!.RowNumber() == informe.Muestras.Count + 1, "Excel incluye muestras");
        Verificar(libro.Worksheet("Respuesta a alarmas").Cell(2, 3).GetDouble() == 1.1, "Excel conserva tiempos de respuesta");
        Verificar(libro.Worksheet("Eventos").Cell(2, 2).GetString() == "DistraccionFutura", "Excel conserva nombres futuros");
        Verificar(libro.Worksheet("Eventos").Cell(2, 8).GetDouble() == 99 &&
            libro.Worksheet("Eventos").Cell(2, 10).GetString() == "Detector futuro",
            "Excel exporta confianza y explicación");
        Verificar(libro.Worksheet("Comparación personal").LastRowUsed()!.RowNumber() == 3,
            "Excel representa sesiones anteriores sin inventar comparación personal");
    }
    ExportadorHistorialExcel.Guardar(excel, Array.Empty<SesionConduccion>());
    using (var libro = new XLWorkbook(excel))
        Verificar(libro.Worksheet("Atención").LastRowUsed()!.RowNumber() == 1, "Exportación vacía válida");
    repo.Borrar();
    using var comprobacion = new SqliteConnection($"Data Source={ruta}");
    comprobacion.Open();
    using var consulta = comprobacion.CreateCommand();
    consulta.CommandText = "SELECT COUNT(*) FROM AtencionSesiones;";
    Verificar(Convert.ToInt32(consulta.ExecuteScalar()) == 0, "Borrado en cascada");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(carpeta, true);
}

string carpetaPersonal = Path.Combine(Path.GetTempPath(), "fatiguedetect-personal-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpetaPersonal);
try
{
    var repo = new HistorialSqliteRepository(Path.Combine(carpetaPersonal, "historial.sqlite"),
        Path.Combine(carpetaPersonal, "ausente.csv"));
    PerfilPersonalConductor? perfil = null;
    SesionConduccion CrearReferencia(int parpadeos, long cerradoMs, double atencion)
    {
        var muestra = new MuestraAtencion(instante.AddMinutes(parpadeos), 60_000, 60_000,
            atencion, 100, false, Array.Empty<FactorAtencion>());
        return new SesionConduccion
        {
            Conductor = new Conductor { Cedula = "  cc-123  ", Nombre = "Ana" },
            Inicio = instante.LocalDateTime,
            Fin = instante.AddMinutes(1).LocalDateTime,
            Calidad = new CalidadSesion(1200, 1200, 60_000, 60_000, 0, 0, 0, 60_000),
            MetricasFatiga = new MetricasFatiga(parpadeos, 0, 0, 60_000, cerradoMs, VersionMetricasOculares: 1),
            Atencion = new InformeAtencion(1, null, [muestra], Array.Empty<RespuestaAlarma>())
        };
    }
    void ProcesarYGuardar(SesionConduccion actual)
    {
        var resultado = PersonalizacionConductor.Procesar(perfil, actual, instante);
        actual.ComparacionPersonal = resultado.Comparacion;
        perfil = resultado.PerfilActualizado;
        repo.Guardar(actual, perfil);
    }

    var primera = CrearReferencia(12, 6_000, 90);
    var segunda = CrearReferencia(13, 6_600, 91);
    var tercera = CrearReferencia(11, 5_400, 89);
    ProcesarYGuardar(primera);
    ProcesarYGuardar(segunda);
    ProcesarYGuardar(tercera);
    Verificar(perfil is { Calibrado: true, SesionesValidas: 3 }, "Perfil calibrado después de tres referencias válidas");
    Verificar(tercera.ComparacionPersonal is { PerfilCalibradoAntes: false, PerfilCalibradoDespues: true },
        "La tercera sesión completa la calibración sin compararse retroactivamente");

    var normal = CrearReferencia(12, 6_300, 90);
    ProcesarYGuardar(normal);
    Verificar(normal.ComparacionPersonal is { PerfilCalibradoAntes: true, CambioAnormal: false, IncorporadaAlPerfil: true },
        "Sesión normal comparada e incorporada");
    Verificar(perfil?.SesionesValidas == 4, "La referencia normal amplía el perfil");

    var anomala = CrearReferencia(12, 21_000, 60);
    ProcesarYGuardar(anomala);
    Verificar(anomala.ComparacionPersonal is { PerclosAnormal: true, AtencionAnormal: true, IncorporadaAlPerfil: false },
        "PERCLOS y atención anormales detectados");
    Verificar(perfil?.SesionesValidas == 4, "Una sesión anómala no desplaza la línea base");

    PerfilPersonalConductor? persistido = repo.LeerPerfilPersonal("CC-123");
    Verificar(persistido is { Calibrado: true, SesionesValidas: 4 }, "Perfil persistente normalizado por cédula");
    var leidas = repo.LeerTodas();
    Verificar(leidas.Single(s => s.Id == anomala.Id).ComparacionPersonal?.PerclosAnormal == true,
        "Comparación personal persiste con la sesión");
    string excel = Path.Combine(carpetaPersonal, "personal.xlsx");
    ExportadorHistorialExcel.Guardar(excel, leidas);
    using (var libro = new XLWorkbook(excel))
    {
        var hoja = libro.Worksheet("Comparación personal");
        Verificar(hoja.LastRowUsed()!.RowNumber() == leidas.Count + 1, "Excel incluye todas las comparaciones personales");
        Verificar(hoja.RowsUsed().Any(f => f.Cell(19).GetString() == "Sí"), "Excel identifica PERCLOS anormal");
    }
    repo.Borrar();
    Verificar(repo.LeerPerfilPersonal("CC-123") is null, "Borrar historial elimina la línea base derivada");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(carpetaPersonal, true);
}
var sesionPostura = new SesionConduccion { Inicio = instante.LocalDateTime, Fin = instante.AddSeconds(15).LocalDateTime };
var postura = new RastreadorFatiga(sesionPostura);
FatigaEnVivo Cara(int ms, double vertical = 0, double horizontal = 0, double boca = .15,
    double altura = 0, bool parcial = false) =>
    postura.Observar(instante.AddMilliseconds(ms), new(.27, .27, new(vertical, horizontal, 0),
        boca, 62, altura), 1.5, parcial);

for (int ms = 0; ms <= 2200; ms += 100) Cara(ms);
for (int ms = 2300; ms <= 2600; ms += 100) Cara(ms, vertical: 16);
Cara(2700);
Verificar(sesionPostura.Eventos.Single(e => e.Tipo == TipoEventoSesion.Cabeceo).DuracionMilisegundos == 400,
    "Duración individual de cada cabeceo");
for (int ms = 2800; ms <= 3700; ms += 100) Cara(ms, horizontal: 26);
for (int ms = 3800; ms <= 4700; ms += 100) Cara(ms, horizontal: -26);
var deslizada = Cara(4800);
Verificar(Math.Abs(deslizada.ProporcionDistraccion.GetValueOrDefault() - 0.625) < 0.001 && postura.ResumenPostura is
    { TiempoFueraFrenteMs: 2000, DistraccionMaximaMs: 2000 }, "Cambio de giro sin partir la distracción");
Verificar(sesionPostura.Eventos.Any(e => e.Tipo == TipoEventoSesion.GiroDerecha) &&
    sesionPostura.Eventos.Any(e => e.Tipo == TipoEventoSesion.GiroIzquierda), "Giros por dirección");
for (int ms = 4900; ms <= 7000; ms += 100) Cara(ms, vertical: 25, boca: .53, altura: .2, parcial: true);
Cara(7100);
Verificar(postura.ResumenPostura is { Bostezos: 1, DistanciaMediaCm: 62 } &&
    sesionPostura.Eventos.Any(e => e.Tipo == TipoEventoSesion.PosiblePosturaEncorvada) &&
    sesionPostura.Eventos.Any(e => e.Tipo == TipoEventoSesion.RostroParcial), "Postura, borde, distancia y bostezo");
postura.Interrumpir(instante.AddMilliseconds(7100), 1.5);
Cara(13500, boca: .53);
postura.Interrumpir(instante.AddMilliseconds(14000), 1.5);
Verificar(postura.ResumenPostura is { TiempoPoseMs: 5500, Bostezos: 1 },
    "Las pausas no suman cobertura ni completan un bostezo");
sesionPostura.Postura = postura.ResumenPostura;
sesionPostura.Calidad = new(100, 100, 7000, 7000, 0, 0, 0, 7000);
string carpetaPostura = Path.Combine(Path.GetTempPath(), "fatiga-postura-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpetaPostura);
try
{
    var repoPostura = new HistorialSqliteRepository(Path.Combine(carpetaPostura, "historial.sqlite"),
        Path.Combine(carpetaPostura, "historial.csv"));
    repoPostura.Guardar(sesionPostura);
    var guardadas = repoPostura.LeerTodas();
    Verificar(guardadas.Single().Postura == sesionPostura.Postura &&
        guardadas.Single().Eventos.Any(e => e.Tipo == TipoEventoSesion.PosibleBostezo),
        "Métricas y eventos persisten en SQLite");
    string excelPostura = Path.Combine(carpetaPostura, "postura.xlsx");
    ExportadorHistorialExcel.Guardar(excelPostura, guardadas);
    using var libroPostura = new XLWorkbook(excelPostura);
    Verificar(libroPostura.Worksheet("Sesiones").Cell(2, 39).GetDouble() == 62 &&
        libroPostura.Worksheet("Sesiones").Cell(2, 40).GetDouble() == 1, "Distancia y bostezos en Excel");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(carpetaPostura, recursive: true);
}

var historia = new[]
{
    new SesionConduccion { Inicio = new DateTime(2026, 11, 16, 8, 0, 0), Fin = new DateTime(2026, 11, 16, 9, 0, 0),
        Conductor = new Conductor { Cedula = "1" }, Vehiculo = new Vehiculo { Placa = "ABC123" },
        CantidadAlarmas = 2, Calidad = new CalidadSesion(100, 100, 3_600_000, 3_600_000, 0, 0, 0, 3_600_000),
        MetricasFatiga = new MetricasFatiga(10, 0, 0, 60_000, 6_000) },
    new SesionConduccion { Inicio = new DateTime(2026, 11, 17, 8, 0, 0), Fin = new DateTime(2026, 11, 17, 8, 30, 0),
        Conductor = new Conductor { Cedula = "1" }, Vehiculo = new Vehiculo { Placa = "XYZ789" },
        Calidad = new CalidadSesion(100, 100, 1_800_000, 1_800_000, 0, 0, 0, 1_800_000),
        MetricasFatiga = new MetricasFatiga(20, 0, 0, 120_000, 24_000) }
};
var conjunto = AnalisisHistoricoAvanzado.Calcular(historia);
Verificar(conjunto.Sesiones == 2 && Math.Abs(conjunto.AlarmasPorHora!.Value - 4.0 / 3) < .001,
    "Alarmas normalizadas por horas activas");
Verificar(Math.Abs(conjunto.Perclos!.Value - .1666666667) < .001 &&
    Math.Abs(conjunto.ParpadeosPorMinuto!.Value - 10) < .001,
    "PERCLOS y frecuencia ponderados por observación ocular");
Verificar(AnalisisHistoricoAvanzado.PorVehiculo(historia).Count == 2 &&
    AnalisisHistoricoAvanzado.PorHora(historia).Single().Indicadores.Sesiones == 2 &&
    AnalisisHistoricoAvanzado.PorDiaSemana(historia).Count == 2,
    "Comparaciones por vehículo, hora y día");
Verificar(AnalisisHistoricoAvanzado.MapaCalor(historia)[0, 8] == 1 &&
    AnalisisHistoricoAvanzado.MapaCalor(historia)[1, 8] == 1,
    "Mapa de calor en hora local de inicio");
Verificar(AnalisisHistoricoAvanzado.TendenciaPersonal(historia, "1").Count == 2 &&
    AnalisisHistoricoAvanzado.Riesgo(historia[0]) is null,
    "Tendencia personal conserva ausencias de atención sin inventar riesgo");

var sesionAlertas = new SesionConduccion { Inicio = instante.LocalDateTime };
var gestorAlertas = new GestorAlertasProgresivas(sesionAlertas);
var calidadAlertas = new CalidadSesion(100, 100, 10_000, 10_000, 0, 0, 0, 10_000);
FatigaEnVivo LecturaAlerta(double apertura = 1, double? perclos = 0, double? distraccion = 0) =>
    new FatigaEnVivo(apertura, apertura, 0, perclos, apertura >= .55 ? 0 : 2000,
        new AngulosCabeza(0, 0, 0), new MetricasFatiga(0, 0, 0, 10_000, 0))
        { ProporcionDistraccion = distraccion };
MuestraAtencion AtencionAlerta(int segundo, double puntaje) => new(instante.AddSeconds(segundo),
    segundo * 1000, 1000, puntaje, 100, true, Array.Empty<FactorAtencion>());

EstadoAlertaProgresiva preventiva = gestorAlertas.Observar(instante, EyeState.DESPIERTO,
    LecturaAlerta(perclos: .16), AtencionAlerta(0, 70), calidadAlertas);
Verificar(preventiva is { Nivel: NivelAlertaProgresiva.Preventiva, EmitirSonido: true },
    "Riesgo temprano activa alerta preventiva");
Verificar(!gestorAlertas.Observar(instante.AddSeconds(1), EyeState.DESPIERTO,
    LecturaAlerta(perclos: .16), AtencionAlerta(1, 70), calidadAlertas).EmitirSonido,
    "Enfriamiento evita repetir inmediatamente el aviso preventivo");
Verificar(gestorAlertas.Observar(instante.AddSeconds(2), EyeState.SOMNOLIENTO,
    LecturaAlerta(.3), AtencionAlerta(2, 45), calidadAlertas).Nivel == NivelAlertaProgresiva.Alta,
    "Somnolencia escala inmediatamente a alerta alta");
Verificar(gestorAlertas.Observar(instante.AddSeconds(3), EyeState.ALARMA_ACTIVADA,
    LecturaAlerta(0), AtencionAlerta(3, 20), calidadAlertas).Nivel == NivelAlertaProgresiva.Critica,
    "Cierre crítico escala inmediatamente");
var invalida = LecturaAlerta() with { AperturaIzquierda = null, AperturaDerecha = null, Angulos = null };
Verificar(gestorAlertas.Observar(instante.AddSeconds(10), EyeState.DESPIERTO, invalida,
    AtencionAlerta(10, 90), calidadAlertas).Nivel == NivelAlertaProgresiva.Critica,
    "Pérdida de medición no confirma recuperación");
gestorAlertas.Observar(instante.AddSeconds(11), EyeState.DESPIERTO, LecturaAlerta(), AtencionAlerta(11, 90), calidadAlertas);
EstadoAlertaProgresiva trasCritica = gestorAlertas.Observar(instante.AddSeconds(14), EyeState.DESPIERTO,
    LecturaAlerta(), AtencionAlerta(14, 90), calidadAlertas);
EstadoAlertaProgresiva trasAlta = gestorAlertas.Observar(instante.AddSeconds(19), EyeState.DESPIERTO,
    LecturaAlerta(), AtencionAlerta(19, 90), calidadAlertas);
EstadoAlertaProgresiva recuperada = gestorAlertas.Observar(instante.AddSeconds(27), EyeState.DESPIERTO,
    LecturaAlerta(), AtencionAlerta(27, 90), calidadAlertas);
Verificar(trasCritica.Nivel == NivelAlertaProgresiva.Alta &&
    trasAlta.Nivel == NivelAlertaProgresiva.Preventiva && recuperada.Nivel == NivelAlertaProgresiva.Normal,
    "La alerta baja por etapas tras 3, 5 y 8 segundos de recuperación estable");
Verificar(sesionAlertas.Eventos.Select(e => e.Tipo).SequenceEqual(new[]
    { TipoEventoSesion.AlertaPreventiva, TipoEventoSesion.AlertaAlta, TipoEventoSesion.AlertaCritica,
      TipoEventoSesion.AlertaAlta, TipoEventoSesion.AlertaPreventiva }),
    "La línea temporal conserva cada nivel de alerta");

string carpetaProtegida = Path.Combine(Path.GetTempPath(), "fatiga-protegida-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpetaProtegida);
try
{
    var protector = new ProtectorDatosLocal(Path.Combine(carpetaProtegida, "clave_datos.bin"));
    var repoSeguro = new HistorialSqliteRepository(Path.Combine(carpetaProtegida, "historial.sqlite"),
        Path.Combine(carpetaProtegida, "ausente.csv"), protector);
    var perfilesSeguros = new PerfilBiometricoRepository(Path.Combine(carpetaProtegida, "perfiles_biometricos.json"), protector);
    perfilesSeguros.Guardar(new PerfilBiometrico { Cedula = "987654", Nombre = "Dato secreto",
        DescriptorFacial = [1, 2, 3], ActualizadoUtc = DateTime.UtcNow });
    string archivoPerfil = File.ReadAllText(perfilesSeguros.RutaArchivo);
    Verificar(protector.EstaCifrado(archivoPerfil) && !archivoPerfil.Contains("987654"),
        "Perfil biométrico cifrado sin identificadores en texto claro");
    var privada = new SesionConduccion { Inicio = instante.LocalDateTime,
        Conductor = new Conductor { Cedula = "987654", Nombre = "Dato secreto" },
        Vehiculo = new Vehiculo { Placa = "SEC123", TipoCarro = "Prueba" } };
    repoSeguro.Guardar(privada);
    using (var conexionSegura = new SqliteConnection($"Data Source={repoSeguro.RutaArchivo}"))
    {
        conexionSegura.Open();
        using var consultaSegura = conexionSegura.CreateCommand();
        consultaSegura.CommandText = "SELECT Cedula || Nombre || Placa FROM Sesiones LIMIT 1;";
        string almacenado = (string)consultaSegura.ExecuteScalar()!;
        Verificar(!almacenado.Contains("987654") && !almacenado.Contains("Dato secreto") &&
            almacenado.Contains("FDENC1:"), "Identidad del historial cifrada por campo");
    }
    Verificar(repoSeguro.LeerTodas().Single().Conductor.Cedula == "987654",
        "Historial cifrado se descifra transparentemente");
    var servicioSeguro = new ProteccionDatosService(carpetaProtegida, protector, repoSeguro, perfilesSeguros);
    string respaldo = Path.Combine(carpetaProtegida, "respaldo.fdbak");
    using (var conexionAbierta = new SqliteConnection($"Data Source={repoSeguro.RutaArchivo}"))
    {
        conexionAbierta.Open();
        servicioSeguro.CrearRespaldo(respaldo, "clave-segura-123");
    }
    Verificar(File.ReadAllBytes(respaldo).AsSpan(0, 9).SequenceEqual("FDBACKUP1"u8),
        "Respaldo protegido usa instantánea aunque SQLite esté abierto");
    bool rechazada = false;
    try { servicioSeguro.RestaurarRespaldo(respaldo, "clave-erronea-123"); }
    catch (InvalidDataException) { rechazada = true; }
    Verificar(rechazada, "Contraseña incorrecta rechaza la restauración");
    perfilesSeguros.Eliminar("987654");
    servicioSeguro.RestaurarRespaldo(respaldo, "clave-segura-123");
    Verificar(new PerfilBiometricoRepository(perfilesSeguros.RutaArchivo,
        new ProtectorDatosLocal(Path.Combine(carpetaProtegida, "clave_datos.bin"))).Existe("987654"),
        "Restauración recupera perfiles y clave portátil");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(carpetaProtegida, true);
}

var guia = new CalibradorGuiado("cam-1", "abc123");
for (int ms = 0; ms <= 2000; ms += 100)
    guia.Observar(instante.AddMilliseconds(ms), new MedicionFacial(.30, .29, new AngulosCabeza(1, 2, 0)));
guia.Observar(instante.AddMilliseconds(2100), new MedicionFacial(.10, .10, new AngulosCabeza(1, 2, 0)));
guia.Observar(instante.AddMilliseconds(2200), new MedicionFacial(.30, .29, new AngulosCabeza(1, 2, 0)));
guia.Observar(instante.AddMilliseconds(2300), new MedicionFacial(.10, .10, new AngulosCabeza(1, 2, 0)));
EstadoCalibracion finGuia = guia.Observar(instante.AddMilliseconds(2400),
    new MedicionFacial(.30, .29, new AngulosCabeza(1, 2, 0)));
Verificar(finGuia.Completa && guia.Resultado is { ParpadeosConfirmados: 2, CamaraId: "CAM-1", VehiculoClave: "ABC123" },
    "Calibración guiada exige referencia frontal y dos parpadeos");

var perfilRiesgo = new PerfilPersonalConductor(2, "1", instante, 3, 180_000, 36_000, 36,
    12, 0, .20, 0, 3, 65, 0, "CAM-1", "ABC123", guia.Resultado);
UmbralesPersonalizados adaptados = PersonalizacionConductor.CalcularUmbrales(perfilRiesgo,
    AjustesDeteccion.Predeterminados);
Verificar(adaptados.Personalizados && adaptados.SegundosSomnoliento < 1.5 &&
    adaptados.SegundosAlarma < 3 && adaptados.SegundosAlarma >= AjustesDeteccion.MinimoAlarma,
    "Umbrales contextuales solo anticipan los límites configurados");

string carpetaContexto = Path.Combine(Path.GetTempPath(), "fatiga-contexto-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpetaContexto);
try
{
    var repoContexto = new HistorialSqliteRepository(Path.Combine(carpetaContexto, "historial.sqlite"),
        Path.Combine(carpetaContexto, "ausente.csv"));
    var sesionContexto = new SesionConduccion { Inicio = instante.LocalDateTime,
        Conductor = new Conductor { Cedula = "1" }, Vehiculo = new Vehiculo { Placa = "ABC123" } };
    repoContexto.Guardar(sesionContexto, perfilRiesgo);
    Verificar(repoContexto.LeerPerfilPersonal("1", "CAM-1", "ABC123")?.Calibracion?.ParpadeosConfirmados == 2,
        "Perfil persistente separado por cámara y vehículo");
    Verificar(repoContexto.LeerPerfilPersonal("1", "CAM-1", "OTRO") is null,
        "Otro vehículo no reutiliza una línea base contextual");
    repoContexto.Borrar();
    Verificar(repoContexto.LeerPerfilPersonal("1", "CAM-1", "ABC123") is null,
        "Borrar historial elimina perfiles contextuales");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(carpetaContexto, true);
}
Console.WriteLine($"{comprobaciones} comprobaciones de atención, postura, historial, SQLite y Excel correctas.");
