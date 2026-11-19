using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FatigueDetect.Datos
{
    public sealed record DispositivoCamara(string Id, string Nombre, int Indice);

    public static class CatalogoCamaras
    {
        private static readonly Guid EnumeradorSistema = new("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
        private static readonly Guid EntradaVideo = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");

        public static IReadOnlyList<DispositivoCamara> Listar()
        {
            var resultado = new List<DispositivoCamara>();
            var sistema = (ICreateDevEnum)Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeradorSistema)!)!;
            IEnumMoniker? enumerador = null;
            IBindCtx? contexto = null;
            try
            {
                Guid categoria = EntradaVideo;
                int estado = sistema.CreateClassEnumerator(ref categoria, out enumerador, 0);
                Marshal.ThrowExceptionForHR(estado);
                if (enumerador == null)
                    return resultado;

                Marshal.ThrowExceptionForHR(CreateBindCtx(0, out contexto));
                var elementos = new IMoniker[1];
                // OpenCV abre por posición en este enumerador y admite 20 posiciones en DirectShow.
                for (int indice = 0; indice < 20; indice++)
                {
                    estado = enumerador.Next(1, elementos, IntPtr.Zero);
                    Marshal.ThrowExceptionForHR(estado);
                    if (estado != 0)
                        break;

                    IMoniker moniker = elementos[0];
                    object? propiedades = null;
                    try
                    {
                        moniker.GetDisplayName(contexto, null, out string id);
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        Guid interfaz = typeof(IPropertyBag).GUID;
                        moniker.BindToStorage(contexto, null, ref interfaz, out propiedades);
                        if (((IPropertyBag)propiedades).Read("FriendlyName", out object valor, IntPtr.Zero) == 0
                            && valor is string nombre && !string.IsNullOrWhiteSpace(nombre))
                        {
                            resultado.Add(new DispositivoCamara(id, nombre, indice));
                        }
                    }
                    catch (COMException)
                    {
                        // Un dispositivo puede desaparecer en mitad de la enumeración.
                    }
                    finally
                    {
                        Liberar(propiedades);
                        Liberar(moniker);
                    }
                }
            }
            finally
            {
                Liberar(contexto);
                Liberar(enumerador);
                Liberar(sistema);
            }
            return resultado;
        }

        private static void Liberar(object? objeto)
        {
            if (objeto != null)
                Marshal.ReleaseComObject(objeto);
        }

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reservado, out IBindCtx contexto);

        [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICreateDevEnum
        {
            [PreserveSig]
            int CreateClassEnumerator(ref Guid categoria, out IEnumMoniker? enumerador, uint opciones);
        }

        [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyBag
        {
            [PreserveSig]
            int Read([MarshalAs(UnmanagedType.LPWStr)] string nombre,
                [MarshalAs(UnmanagedType.Struct)] out object valor, IntPtr registroErrores);
        }
    }
}
