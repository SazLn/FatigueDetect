using System;
using System.Collections.Generic;
using FatigueDetect.Datos;
using FatigueDetect.Entidades;

namespace FatigueDetect.Logica
{
    public class SesionService
    {
        private readonly HistorialSqliteRepository repositorio;

        public SesionService(HistorialSqliteRepository repositorio)
        {
            this.repositorio = repositorio;
        }

        public string RutaHistorial => repositorio.RutaArchivo;

        public SesionConduccion Iniciar(string cedula, string nombre, string apellidos,
                                        string tipoCarro, string placa)
        {
            return new SesionConduccion
            {
                Conductor = new Conductor
                {
                    Cedula = cedula.Trim(),
                    Nombre = nombre.Trim(),
                    Apellidos = apellidos.Trim()
                },
                Vehiculo = new Vehiculo
                {
                    TipoCarro = tipoCarro.Trim(),
                    Placa = ValidadorRegistro.NormalizarPlaca(placa)
                },
                Inicio = DateTime.Now
            };
        }

        public void Finalizar(SesionConduccion sesion)
        {
            sesion.Fin = DateTime.Now;
            PerfilPersonalConductor? perfil = repositorio.LeerPerfilPersonal(sesion.Conductor.Cedula);
            var resultado = PersonalizacionConductor.Procesar(perfil, sesion, DateTimeOffset.Now);
            sesion.ComparacionPersonal = resultado.Comparacion;
            repositorio.Guardar(sesion, resultado.PerfilActualizado);
        }

        public List<SesionConduccion> ObtenerHistorial()
        {
            return repositorio.LeerTodas();
        }

        public void BorrarHistorial()
        {
            repositorio.Borrar();
        }
    }
}
