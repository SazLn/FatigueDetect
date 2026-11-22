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
                                        string tipoCarro, string placa, string? camaraId = null,
                                        CalibracionGuiada? calibracion = null,
                                        UmbralesPersonalizados? umbrales = null)
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
                Inicio = DateTime.Now,
                CamaraId = PersonalizacionConductor.NormalizarContexto(camaraId, "AUTOMATICA"),
                Calibracion = calibracion,
                UmbralesAplicados = umbrales
            };
        }

        public PerfilPersonalConductor? ObtenerPerfil(string cedula, string? camaraId, string placa) =>
            repositorio.LeerPerfilPersonal(cedula,
                PersonalizacionConductor.NormalizarContexto(camaraId, "AUTOMATICA"),
                PersonalizacionConductor.NormalizarContexto(ValidadorRegistro.NormalizarPlaca(placa), "SIN-VEHICULO"));

        public void Finalizar(SesionConduccion sesion)
        {
            sesion.Fin = DateTime.Now;
            AnalisisConfiabilidadEvento.Completar(sesion);
            PerfilPersonalConductor? perfil = repositorio.LeerPerfilPersonal(sesion.Conductor.Cedula,
                sesion.CamaraId, sesion.Vehiculo.Placa);
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
