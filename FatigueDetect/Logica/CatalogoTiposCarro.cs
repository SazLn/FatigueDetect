using System;
using System.Collections.Generic;
using System.Linq;
using FatigueDetect.Datos;

namespace FatigueDetect.Logica
{
    /// <summary>Lista de tipos de carro: los básicos más los que el usuario ha registrado.</summary>
    public class CatalogoTiposCarro
    {
        private readonly TiposCarroRepository repositorio;

        public CatalogoTiposCarro(TiposCarroRepository repositorio)
        {
            this.repositorio = repositorio;
        }

        public List<string> ObtenerTodos()
        {
            var lista = new List<string>(ValidadorRegistro.TiposDeCarro);

            IEnumerable<string> guardados;
            try
            {
                guardados = repositorio.LeerTodos();
            }
            catch (Exception)
            {
                guardados = new List<string>();
            }

            foreach (string tipo in guardados.OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!Contiene(lista, tipo))
                {
                    lista.Add(tipo);
                }
            }

            return lista;
        }

        /// <summary>
        /// Si el tipo ya existe devuelve el existente; si es nuevo lo guarda.
        /// Devuelve el nombre final que se debe usar.
        /// </summary>
        public string Registrar(string tipo)
        {
            string limpio = Normalizar(tipo);
            if (limpio.Length == 0)
            {
                return "";
            }

            List<string> existentes = ObtenerTodos();
            string? coincidencia = existentes.FirstOrDefault(
                x => string.Equals(x, limpio, StringComparison.CurrentCultureIgnoreCase));

            if (coincidencia != null)
            {
                return coincidencia;
            }

            try
            {
                repositorio.Agregar(limpio);
            }
            catch (Exception)
            {
                // Si no se puede guardar, la sesión continúa igual con ese tipo.
            }

            return limpio;
        }

        private static string Normalizar(string tipo)
        {
            string texto = string.Join(" ",
                (tipo ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            return texto.Length == 0 ? "" : char.ToUpper(texto[0]) + texto.Substring(1);
        }

        private static bool Contiene(List<string> lista, string tipo)
        {
            return lista.Any(x => string.Equals(x, tipo, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
