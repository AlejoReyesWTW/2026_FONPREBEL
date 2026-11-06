namespace FonPrebel.Application.Services;

/// <summary>
/// Estado compartido que gobierna el botón INICIAR (S18, Anexo D): la pantalla Configuración publica si
/// toda la configuración obligatoria es válida y la sección Ejecución decide con eso si habilita el
/// botón. Es deliberadamente mínimo: no valida nada por su cuenta ni guarda valores.
/// </summary>
/// <remarks>
/// El estado inicial es "incompleta": mientras la configuración no se haya cargado y validado, el
/// proceso no arranca. Cambiar un valor vuelve a publicar el resultado, de modo que el gate se cierra
/// solo cuando un parámetro se invalida.
/// </remarks>
public sealed class ConfigurationReadinessGate
{
    /// <summary>Indica si toda la configuración obligatoria es válida.</summary>
    public bool IsConfigurationComplete { get; private set; }

    /// <summary>Se dispara cuando cambia <see cref="IsConfigurationComplete"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Publica el resultado de la validación de la configuración completa.</summary>
    /// <param name="isComplete">Verdadero sólo si todos los parámetros obligatorios son válidos.</param>
    public void Report(bool isComplete)
    {
        if (IsConfigurationComplete == isComplete)
        {
            return;
        }

        IsConfigurationComplete = isComplete;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
