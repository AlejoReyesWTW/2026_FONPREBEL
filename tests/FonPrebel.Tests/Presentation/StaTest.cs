using System.Runtime.ExceptionServices;

namespace FonPrebel.Tests.Presentation;

/// <summary>
/// Ejecuta código de WPF en su propio hilo STA, que es el único apartamento donde se pueden crear
/// elementos de interfaz. La excepción del hilo, si la hay, se vuelve a lanzar en el hilo de la prueba
/// para que el fallo se vea como un fallo de la prueba y no como un bloqueo silencioso.
/// </summary>
internal static class StaTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Run<object?>(() =>
        {
            action();
            return null;
        });
    }

    public static T Run<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        T result = default!;
        ExceptionDispatchInfo? failure = null;

        Thread thread = new(() =>
        {
            try
            {
                result = function();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!thread.Join(Timeout))
        {
            throw new TimeoutException("La operación de interfaz no terminó dentro del tiempo esperado.");
        }

        failure?.Throw();

        return result;
    }
}
