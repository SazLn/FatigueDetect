using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FatigueDetect.Entidades;
using Microsoft.Data.Sqlite;

namespace FatigueDetect.Datos
{
    public sealed class HistorialSqliteRepository
    {
        private readonly string rutaCsv;
        private readonly string cadenaConexion;
        private readonly object candado = new object();
        private bool inicializado;

        public string RutaArchivo { get; }

        public HistorialSqliteRepository(string rutaArchivo, string rutaCsv)
        {
            RutaArchivo = Path.GetFullPath(rutaArchivo);
            this.rutaCsv = rutaCsv;
            cadenaConexion = new SqliteConnectionStringBuilder
            {
                DataSource = RutaArchivo,
                ForeignKeys = true
            }.ToString();
        }

        public void Guardar(SesionConduccion sesion)
        {
            using SqliteConnection conexion = Abrir();
            using SqliteTransaction transaccion = conexion.BeginTransaction();
            InsertarSesion(conexion, transaccion, sesion);
            transaccion.Commit();
        }

        public List<SesionConduccion> LeerTodas()
        {
            using SqliteConnection conexion = Abrir();
            using SqliteTransaction transaccion = conexion.BeginTransaction(deferred: true);
            var sesiones = new Dictionary<string, SesionConduccion>();

            using (SqliteCommand consulta = CrearComando(conexion, transaccion, """
                SELECT Id, Cedula, Nombre, Apellidos, TipoCarro, Placa, InicioUtcMs, FinUtcMs, CantidadAlarmas,
                       Fotogramas, Analizables, TiempoActivoMs, TiempoRostroValidoMs, FallosCamara,
                       Parpadeos, CierresProlongados, Cabeceos, TiempoOcularValidoMs, TiempoOcularCerradoMs,
                       VersionOcular, ParpadeosCortos, ParpadeosProlongados, CierresAnomalos,
                       DuracionParpadeosTotalMs, DuracionParpadeoMinMs, DuracionParpadeoMaxMs,
                       CierreMasLargoMs, MicrosuenoMasLargoMs, DiferenciaAperturaPromedio,
                       CaidasFps, TiempoFpsBajoMs, TiempoEvaluadoFpsMs
                FROM Sesiones LEFT JOIN CalidadSesiones ON CalidadSesiones.SesionId = Sesiones.Id
                LEFT JOIN MetricasFatigaSesiones ON MetricasFatigaSesiones.SesionId = Sesiones.Id
                ORDER BY InicioUtcMs DESC, Id;
                """))
            using (SqliteDataReader lector = consulta.ExecuteReader())
            {
                while (lector.Read())
                {
                    string id = lector.GetString(0);
                    sesiones.Add(id, new SesionConduccion
                    {
                        Id = Guid.Parse(id),
                        Conductor = new Conductor
                        {
                            Cedula = lector.GetString(1), Nombre = lector.GetString(2), Apellidos = lector.GetString(3)
                        },
                        Vehiculo = new Vehiculo { TipoCarro = lector.GetString(4), Placa = lector.GetString(5) },
                        Inicio = LeerInstante(lector, 6).LocalDateTime,
                        Fin = lector.IsDBNull(7) ? null : LeerInstante(lector, 7).LocalDateTime,
                        CantidadAlarmas = lector.GetInt32(8),
                        Calidad = lector.IsDBNull(9) ? null : new CalidadSesion(lector.GetInt64(9),
                            lector.GetInt64(10), lector.GetInt64(11), lector.GetInt64(12), lector.GetInt64(13),
                            LeerEnteroOpcional(lector, 29), LeerEnteroOpcional(lector, 30), LeerEnteroOpcional(lector, 31)),
                        MetricasFatiga = lector.IsDBNull(14) ? null : new MetricasFatiga(lector.GetInt32(14),
                            lector.GetInt32(15), lector.GetInt32(16), lector.GetInt64(17), lector.GetInt64(18),
                            lector.GetInt32(19), lector.GetInt32(20), lector.GetInt32(21), lector.GetInt32(22),
                            lector.GetInt64(23), lector.IsDBNull(24) ? null : lector.GetInt64(24),
                            lector.IsDBNull(25) ? null : lector.GetInt64(25), lector.GetInt64(26),
                            lector.GetInt64(27), lector.IsDBNull(28) ? null : lector.GetDouble(28))
                    });
                }
            }

            using (SqliteCommand consulta = CrearComando(conexion, transaccion,
                "SELECT SesionId, Contenido FROM AtencionSesiones;"))
            using (SqliteDataReader lector = consulta.ExecuteReader())
            {
                while (lector.Read())
                {
                    if (sesiones.TryGetValue(lector.GetString(0), out SesionConduccion? sesion))
                        sesion.Atencion = JsonSerializer.Deserialize<InformeAtencion>(lector.GetString(1));
                }
            }

            var evolucionPerclos = new Dictionary<string, List<MuestraPerclos>>();
            using (SqliteCommand consulta = CrearComando(conexion, transaccion, """
                SELECT SesionId, InstanteUtcMs, Valor
                FROM EvolucionPerclosSesiones ORDER BY SesionId, InstanteUtcMs;
                """))
            using (SqliteDataReader lector = consulta.ExecuteReader())
            {
                while (lector.Read())
                {
                    string id = lector.GetString(0);
                    if (!sesiones.ContainsKey(id))
                        continue;
                    if (!evolucionPerclos.TryGetValue(id, out List<MuestraPerclos>? muestras))
                    {
                        muestras = new List<MuestraPerclos>();
                        evolucionPerclos.Add(id, muestras);
                    }
                    muestras.Add(new MuestraPerclos(LeerInstante(lector, 1), lector.GetDouble(2)));
                }
            }
            foreach ((string id, List<MuestraPerclos> muestras) in evolucionPerclos)
            {
                if (sesiones[id].MetricasFatiga is MetricasFatiga fatiga)
                    sesiones[id].MetricasFatiga = fatiga with { EvolucionPerclos = muestras };
            }

            using (SqliteCommand consulta = CrearComando(conexion, transaccion, """
                SELECT SesionId, Tipo, InicioUtcMs, FinUtcMs
                FROM Eventos ORDER BY SesionId, InicioUtcMs, Id;
                """))
            using (SqliteDataReader lector = consulta.ExecuteReader())
            {
                while (lector.Read())
                {
                    string codigo = lector.GetString(1);
                    if (!Enum.TryParse(codigo, out TipoEventoSesion tipo) || !Enum.IsDefined(tipo))
                        tipo = TipoEventoSesion.EventoExterno;

                    sesiones[lector.GetString(0)].RegistrarEvento(new EventoSesion(tipo, LeerInstante(lector, 2),
                        lector.IsDBNull(3) ? null : LeerInstante(lector, 3)) { CodigoTipo = codigo });
                }
            }

            foreach (SesionConduccion sesion in sesiones.Values)
            {
                sesion.EstablecerHorasAlarmas(sesion.Eventos.Where(e => e.Tipo == TipoEventoSesion.Alarma)
                    .Select(e => e.Inicio.LocalDateTime));
            }
            transaccion.Commit();
            return sesiones.Values.ToList();
        }

        public void Borrar()
        {
            using SqliteConnection conexion = Abrir();
            using SqliteCommand comando = conexion.CreateCommand();
            comando.CommandText = "DELETE FROM Sesiones;";
            comando.ExecuteNonQuery();
        }

        private SqliteConnection Abrir()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            var conexion = new SqliteConnection(cadenaConexion);
            try
            {
                conexion.Open();
                lock (candado)
                {
                    if (!inicializado)
                    {
                        Inicializar(conexion);
                        inicializado = true;
                    }
                }
                return conexion;
            }
            catch
            {
                conexion.Dispose();
                throw;
            }
        }

        private void Inicializar(SqliteConnection conexion)
        {
            using SqliteTransaction transaccion = conexion.BeginTransaction();
            using SqliteCommand esquema = CrearComando(conexion, transaccion, """
                CREATE TABLE IF NOT EXISTS Sesiones (
                    Id TEXT PRIMARY KEY NOT NULL,
                    Cedula TEXT NOT NULL, Nombre TEXT NOT NULL, Apellidos TEXT NOT NULL,
                    TipoCarro TEXT NOT NULL, Placa TEXT NOT NULL,
                    InicioUtcMs INTEGER NOT NULL, FinUtcMs INTEGER,
                    CantidadAlarmas INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Eventos (
                    Id INTEGER PRIMARY KEY,
                    SesionId TEXT NOT NULL REFERENCES Sesiones(Id) ON DELETE CASCADE,
                    Tipo TEXT NOT NULL, InicioUtcMs INTEGER NOT NULL, FinUtcMs INTEGER,
                    DuracionMs INTEGER
                );
                CREATE TABLE IF NOT EXISTS CalidadSesiones (
                    SesionId TEXT PRIMARY KEY NOT NULL REFERENCES Sesiones(Id) ON DELETE CASCADE,
                    Fotogramas INTEGER NOT NULL, Analizables INTEGER NOT NULL,
                    TiempoActivoMs INTEGER NOT NULL, TiempoRostroValidoMs INTEGER NOT NULL,
                    FallosCamara INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS MetricasFatigaSesiones (
                    SesionId TEXT PRIMARY KEY NOT NULL REFERENCES Sesiones(Id) ON DELETE CASCADE,
                    Parpadeos INTEGER NOT NULL, CierresProlongados INTEGER NOT NULL, Cabeceos INTEGER NOT NULL,
                    TiempoOcularValidoMs INTEGER NOT NULL, TiempoOcularCerradoMs INTEGER NOT NULL,
                    VersionOcular INTEGER NOT NULL DEFAULT 0,
                    ParpadeosCortos INTEGER NOT NULL DEFAULT 0,
                    ParpadeosProlongados INTEGER NOT NULL DEFAULT 0,
                    CierresAnomalos INTEGER NOT NULL DEFAULT 0,
                    DuracionParpadeosTotalMs INTEGER NOT NULL DEFAULT 0,
                    DuracionParpadeoMinMs INTEGER,
                    DuracionParpadeoMaxMs INTEGER,
                    CierreMasLargoMs INTEGER NOT NULL DEFAULT 0,
                    MicrosuenoMasLargoMs INTEGER NOT NULL DEFAULT 0,
                    DiferenciaAperturaPromedio REAL
                );
                CREATE TABLE IF NOT EXISTS EvolucionPerclosSesiones (
                    SesionId TEXT NOT NULL REFERENCES Sesiones(Id) ON DELETE CASCADE,
                    InstanteUtcMs INTEGER NOT NULL, Valor REAL NOT NULL,
                    PRIMARY KEY (SesionId, InstanteUtcMs)
                );
                CREATE INDEX IF NOT EXISTS IX_Sesiones_Inicio ON Sesiones (InicioUtcMs);
                CREATE INDEX IF NOT EXISTS IX_Eventos_Sesion_Inicio ON Eventos (SesionId, InicioUtcMs);
                CREATE TABLE IF NOT EXISTS Migraciones (Nombre TEXT PRIMARY KEY NOT NULL);
                CREATE TABLE IF NOT EXISTS AtencionSesiones (
                    SesionId TEXT PRIMARY KEY NOT NULL REFERENCES Sesiones(Id) ON DELETE CASCADE,
                    Contenido TEXT NOT NULL
                );
                """);
            esquema.ExecuteNonQuery();
            AsegurarColumnasMetricasFatiga(conexion, transaccion);
            AsegurarColumnas(conexion, transaccion, "CalidadSesiones",
                [("CaidasFps", "INTEGER"), ("TiempoFpsBajoMs", "INTEGER"), ("TiempoEvaluadoFpsMs", "INTEGER")]);

            using SqliteCommand marca = CrearComando(conexion, transaccion,
                "INSERT OR IGNORE INTO Migraciones (Nombre) VALUES ('historial_csv_v1');");
            if (marca.ExecuteNonQuery() == 1)
            {
                foreach (SesionConduccion sesion in ImportadorHistorialCsv.Leer(rutaCsv))
                    InsertarSesion(conexion, transaccion, sesion);
            }
            transaccion.Commit();
        }

        private static void InsertarSesion(SqliteConnection conexion, SqliteTransaction transaccion, SesionConduccion sesion)
        {
            using SqliteCommand insercion = CrearComando(conexion, transaccion, """
                INSERT INTO Sesiones (Id, Cedula, Nombre, Apellidos, TipoCarro, Placa, InicioUtcMs, FinUtcMs, CantidadAlarmas)
                VALUES ($id, $cedula, $nombre, $apellidos, $tipo, $placa, $inicio, $fin, $alarmas)
                ON CONFLICT (Id) DO NOTHING;
                """);
            insercion.Parameters.AddWithValue("$id", sesion.Id.ToString());
            insercion.Parameters.AddWithValue("$cedula", sesion.Conductor.Cedula);
            insercion.Parameters.AddWithValue("$nombre", sesion.Conductor.Nombre);
            insercion.Parameters.AddWithValue("$apellidos", sesion.Conductor.Apellidos);
            insercion.Parameters.AddWithValue("$tipo", sesion.Vehiculo.TipoCarro);
            insercion.Parameters.AddWithValue("$placa", sesion.Vehiculo.Placa);
            insercion.Parameters.AddWithValue("$inicio", new DateTimeOffset(sesion.Inicio).ToUnixTimeMilliseconds());
            insercion.Parameters.AddWithValue("$fin", sesion.Fin is DateTime fin
                ? new DateTimeOffset(fin).ToUnixTimeMilliseconds() : DBNull.Value);
            insercion.Parameters.AddWithValue("$alarmas", sesion.CantidadAlarmas);
            if (insercion.ExecuteNonQuery() == 0)
                return;

            if (sesion.Calidad is CalidadSesion calidad)
            {
                using SqliteCommand mediciones = CrearComando(conexion, transaccion, """
                    INSERT INTO CalidadSesiones
                        (SesionId, Fotogramas, Analizables, TiempoActivoMs, TiempoRostroValidoMs, FallosCamara,
                         CaidasFps, TiempoFpsBajoMs, TiempoEvaluadoFpsMs)
                    VALUES ($id, $fotogramas, $analizables, $activo, $rostro, $fallos, $caidas, $fpsBajo, $fpsEvaluado);
                    """);
                mediciones.Parameters.AddWithValue("$id", sesion.Id.ToString());
                mediciones.Parameters.AddWithValue("$fotogramas", calidad.Fotogramas);
                mediciones.Parameters.AddWithValue("$analizables", calidad.Analizables);
                mediciones.Parameters.AddWithValue("$activo", calidad.TiempoActivoMs);
                mediciones.Parameters.AddWithValue("$rostro", calidad.TiempoRostroValidoMs);
                mediciones.Parameters.AddWithValue("$fallos", calidad.FallosCamara);
                mediciones.Parameters.AddWithValue("$caidas", (object?)calidad.CaidasFps ?? DBNull.Value);
                mediciones.Parameters.AddWithValue("$fpsBajo", (object?)calidad.TiempoFpsBajoMs ?? DBNull.Value);
                mediciones.Parameters.AddWithValue("$fpsEvaluado", (object?)calidad.TiempoEvaluadoFpsMs ?? DBNull.Value);
                mediciones.ExecuteNonQuery();
            }

            if (sesion.MetricasFatiga is MetricasFatiga fatiga)
            {
                using SqliteCommand mediciones = CrearComando(conexion, transaccion, """
                    INSERT INTO MetricasFatigaSesiones
                        (SesionId, Parpadeos, CierresProlongados, Cabeceos, TiempoOcularValidoMs,
                         TiempoOcularCerradoMs, VersionOcular, ParpadeosCortos, ParpadeosProlongados,
                         CierresAnomalos, DuracionParpadeosTotalMs, DuracionParpadeoMinMs,
                         DuracionParpadeoMaxMs, CierreMasLargoMs, MicrosuenoMasLargoMs,
                         DiferenciaAperturaPromedio)
                    VALUES ($id, $parpadeos, $cierres, $cabeceos, $valido, $cerrado, $version,
                            $cortos, $prolongados, $anomalos, $duracionTotal, $duracionMinima,
                            $duracionMaxima, $cierreMaximo, $microsuenoMaximo, $diferencia);
                    """);
                mediciones.Parameters.AddWithValue("$id", sesion.Id.ToString());
                mediciones.Parameters.AddWithValue("$parpadeos", fatiga.Parpadeos);
                mediciones.Parameters.AddWithValue("$cierres", fatiga.CierresProlongados);
                mediciones.Parameters.AddWithValue("$cabeceos", fatiga.Cabeceos);
                mediciones.Parameters.AddWithValue("$valido", fatiga.TiempoOcularValidoMs);
                mediciones.Parameters.AddWithValue("$cerrado", fatiga.TiempoOcularCerradoMs);
                mediciones.Parameters.AddWithValue("$version", fatiga.VersionMetricasOculares);
                mediciones.Parameters.AddWithValue("$cortos", fatiga.ParpadeosCortos);
                mediciones.Parameters.AddWithValue("$prolongados", fatiga.ParpadeosProlongados);
                mediciones.Parameters.AddWithValue("$anomalos", fatiga.CierresAnomalos);
                mediciones.Parameters.AddWithValue("$duracionTotal", fatiga.DuracionParpadeosTotalMs);
                mediciones.Parameters.AddWithValue("$duracionMinima", fatiga.DuracionParpadeoMinMs is long minima ? minima : DBNull.Value);
                mediciones.Parameters.AddWithValue("$duracionMaxima", fatiga.DuracionParpadeoMaxMs is long maxima ? maxima : DBNull.Value);
                mediciones.Parameters.AddWithValue("$cierreMaximo", fatiga.CierreMasLargoMs);
                mediciones.Parameters.AddWithValue("$microsuenoMaximo", fatiga.MicrosuenoMasLargoMs);
                mediciones.Parameters.AddWithValue("$diferencia", fatiga.DiferenciaAperturaPromedio is double diferencia ? diferencia : DBNull.Value);
                mediciones.ExecuteNonQuery();

                using SqliteCommand evolucion = CrearComando(conexion, transaccion, """
                    INSERT INTO EvolucionPerclosSesiones (SesionId, InstanteUtcMs, Valor)
                    VALUES ($id, $instante, $valor);
                    """);
                evolucion.Parameters.AddWithValue("$id", sesion.Id.ToString());
                SqliteParameter instante = evolucion.Parameters.Add("$instante", SqliteType.Integer);
                SqliteParameter valor = evolucion.Parameters.Add("$valor", SqliteType.Real);
                foreach (MuestraPerclos muestra in fatiga.EvolucionPerclos)
                {
                    instante.Value = muestra.Instante.ToUnixTimeMilliseconds();
                    valor.Value = muestra.Valor;
                    evolucion.ExecuteNonQuery();
                }
            }

            if (sesion.Atencion is InformeAtencion atencion)
            {
                using SqliteCommand informe = CrearComando(conexion, transaccion,
                    "INSERT INTO AtencionSesiones (SesionId, Contenido) VALUES ($id, $contenido);");
                informe.Parameters.AddWithValue("$id", sesion.Id.ToString());
                informe.Parameters.AddWithValue("$contenido", JsonSerializer.Serialize(atencion));
                informe.ExecuteNonQuery();
            }

            using SqliteCommand eventos = CrearComando(conexion, transaccion, """
                INSERT INTO Eventos (SesionId, Tipo, InicioUtcMs, FinUtcMs, DuracionMs)
                VALUES ($sesion, $tipo, $inicio, $fin, $duracion);
                """);
            eventos.Parameters.AddWithValue("$sesion", sesion.Id.ToString());
            SqliteParameter tipo = eventos.Parameters.Add("$tipo", SqliteType.Text);
            SqliteParameter inicio = eventos.Parameters.Add("$inicio", SqliteType.Integer);
            SqliteParameter termino = eventos.Parameters.Add("$fin", SqliteType.Integer);
            SqliteParameter duracion = eventos.Parameters.Add("$duracion", SqliteType.Integer);
            foreach (EventoSesion evento in sesion.Eventos)
            {
                tipo.Value = evento.CodigoTipo ?? evento.Tipo.ToString();
                inicio.Value = evento.Inicio.ToUnixTimeMilliseconds();
                termino.Value = evento.Fin?.ToUnixTimeMilliseconds() is long fecha ? fecha : DBNull.Value;
                duracion.Value = evento.DuracionMilisegundos is long ms ? ms : DBNull.Value;
                eventos.ExecuteNonQuery();
            }
        }

        private static SqliteCommand CrearComando(SqliteConnection conexion, SqliteTransaction transaccion, string sql)
        {
            SqliteCommand comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = sql;
            return comando;
        }

        private static void AsegurarColumnasMetricasFatiga(SqliteConnection conexion, SqliteTransaction transaccion)
        {
            (string Nombre, string Definicion)[] columnas =
            [
                ("VersionOcular", "INTEGER NOT NULL DEFAULT 0"),
                ("ParpadeosCortos", "INTEGER NOT NULL DEFAULT 0"),
                ("ParpadeosProlongados", "INTEGER NOT NULL DEFAULT 0"),
                ("CierresAnomalos", "INTEGER NOT NULL DEFAULT 0"),
                ("DuracionParpadeosTotalMs", "INTEGER NOT NULL DEFAULT 0"),
                ("DuracionParpadeoMinMs", "INTEGER"),
                ("DuracionParpadeoMaxMs", "INTEGER"),
                ("CierreMasLargoMs", "INTEGER NOT NULL DEFAULT 0"),
                ("MicrosuenoMasLargoMs", "INTEGER NOT NULL DEFAULT 0"),
                ("DiferenciaAperturaPromedio", "REAL")
            ];
            AsegurarColumnas(conexion, transaccion, "MetricasFatigaSesiones", columnas);
        }

        private static void AsegurarColumnas(SqliteConnection conexion, SqliteTransaction transaccion,
            string tabla, (string Nombre, string Definicion)[] columnas)
        {
            var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqliteCommand consulta = CrearComando(conexion, transaccion, $"PRAGMA table_info({tabla});"))
            using (SqliteDataReader lector = consulta.ExecuteReader())
            {
                while (lector.Read())
                    existentes.Add(lector.GetString(1));
            }

            foreach ((string nombre, string definicion) in columnas)
            {
                if (existentes.Contains(nombre))
                    continue;
                using SqliteCommand alteracion = CrearComando(conexion, transaccion,
                    $"ALTER TABLE {tabla} ADD COLUMN {nombre} {definicion};");
                alteracion.ExecuteNonQuery();
            }
        }

        private static long? LeerEnteroOpcional(SqliteDataReader lector, int columna) =>
            lector.IsDBNull(columna) ? null : lector.GetInt64(columna);

        private static DateTimeOffset LeerInstante(SqliteDataReader lector, int columna) =>
            DateTimeOffset.FromUnixTimeMilliseconds(lector.GetInt64(columna));
    }
}
