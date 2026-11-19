using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                SELECT Id, Cedula, Nombre, Apellidos, TipoCarro, Placa, InicioUtcMs, FinUtcMs, CantidadAlarmas
                FROM Sesiones ORDER BY InicioUtcMs DESC, Id;
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
                        CantidadAlarmas = lector.GetInt32(8)
                    });
                }
            }

            using (SqliteCommand consulta = CrearComando(conexion, transaccion, """
                SELECT SesionId, Tipo, InicioUtcMs, FinUtcMs
                FROM Eventos ORDER BY SesionId, InicioUtcMs, Id;
                """))
            using (SqliteDataReader lector = consulta.ExecuteReader())
            {
                while (lector.Read())
                {
                    if (!Enum.TryParse(lector.GetString(1), out TipoEventoSesion tipo) || !Enum.IsDefined(tipo))
                        continue;

                    sesiones[lector.GetString(0)].RegistrarEvento(new EventoSesion(tipo, LeerInstante(lector, 2),
                        lector.IsDBNull(3) ? null : LeerInstante(lector, 3)));
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
                CREATE INDEX IF NOT EXISTS IX_Sesiones_Inicio ON Sesiones (InicioUtcMs);
                CREATE INDEX IF NOT EXISTS IX_Eventos_Sesion_Inicio ON Eventos (SesionId, InicioUtcMs);
                CREATE TABLE IF NOT EXISTS Migraciones (Nombre TEXT PRIMARY KEY NOT NULL);
                """);
            esquema.ExecuteNonQuery();

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
                tipo.Value = evento.Tipo.ToString();
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

        private static DateTimeOffset LeerInstante(SqliteDataReader lector, int columna) =>
            DateTimeOffset.FromUnixTimeMilliseconds(lector.GetInt64(columna));
    }
}
