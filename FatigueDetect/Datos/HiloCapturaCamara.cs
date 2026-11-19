using System;
using System.Threading;
using System.Threading.Tasks;

namespace FatigueDetect.Datos
{
    public static class HiloCapturaCamara
    {
        public static Thread Iniciar(ThreadStart accion)
        {
            // DirectShow inicializa COM al abrir la cámara y lo libera en el mismo hilo.
            var hilo = new Thread(accion) { IsBackground = true };
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            return hilo;
        }

        public static Task<T> EjecutarAsync<T>(Func<T> accion)
        {
            var resultado = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Iniciar(() =>
            {
                try
                {
                    resultado.SetResult(accion());
                }
                catch (OperationCanceledException ex)
                {
                    resultado.SetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    resultado.SetException(ex);
                }
            });
            return resultado.Task;
        }

        public static Task EjecutarAsync(Action accion) =>
            EjecutarAsync<object?>(() =>
            {
                accion();
                return null;
            });
    }
}
