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

string carpeta = Path.Combine(Path.GetTempPath(), "fatiguedetect-atencion-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpeta);
try
{
    string ruta = Path.Combine(carpeta, "historial.sqlite");
    var repo = new HistorialSqliteRepository(ruta, Path.Combine(carpeta, "ausente.csv"));
    // Simula una base previa a esta entrega: sin tabla AtencionSesiones.
    repo.LeerTodas();
    using (var conexion = new SqliteConnection($"Data Source={ruta}"))
    {
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DROP TABLE AtencionSesiones;";
        comando.ExecuteNonQuery();
    }
    repo = new HistorialSqliteRepository(ruta, Path.Combine(carpeta, "ausente.csv"));
    var antigua = new SesionConduccion { Inicio = instante.LocalDateTime, Fin = instante.AddMinutes(1).LocalDateTime };
    repo.Guardar(antigua);
    sesion.Atencion = informe;
    sesion.RegistrarEvento(new EventoSesion(TipoEventoSesion.EventoExterno, instante, instante.AddSeconds(2)) { CodigoTipo = "DistraccionFutura" });
    repo.Guardar(sesion);
    var leidas = repo.LeerTodas();
    Verificar(leidas.Single(s => s.Id == antigua.Id).Atencion is null, "Sesión antigua conserva ausencia de medición");
    var leida = leidas.Single(s => s.Id == sesion.Id);
    Verificar(leida.Atencion?.Muestras.Count == informe.Muestras.Count && leida.Atencion.Respuestas[0] == informe.Respuestas[0], "Persistencia de muestras y respuestas");
    Verificar(leida.Eventos[0].CodigoTipo == "DistraccionFutura", "Eventos futuros preservados");
    string excel = Path.Combine(carpeta, "informe.xlsx");
    ExportadorHistorialExcel.Guardar(excel, leidas);
    using (var libro = new XLWorkbook(excel))
    {
        Verificar(libro.Worksheet("Atención").LastRowUsed()!.RowNumber() == 3, "Excel incluye sesiones nuevas y antiguas");
        Verificar(libro.Worksheet("Evolución atención").LastRowUsed()!.RowNumber() == informe.Muestras.Count + 1, "Excel incluye muestras");
        Verificar(libro.Worksheet("Respuesta a alarmas").Cell(2, 3).GetDouble() == 1.1, "Excel conserva tiempos de respuesta");
        Verificar(libro.Worksheet("Eventos").Cell(2, 2).GetString() == "DistraccionFutura", "Excel conserva nombres futuros");
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
Console.WriteLine($"{comprobaciones} comprobaciones de atención, personalización, SQLite y Excel correctas.");
