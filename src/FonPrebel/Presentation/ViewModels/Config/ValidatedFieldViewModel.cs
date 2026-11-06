using CommunityToolkit.Mvvm.ComponentModel;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Anexo E: un campo con su propia validación. Mientras valida muestra un <c>ProgressRing</c> de 12 px;
/// al terminar queda en verde o en rojo con el mensaje concreto que trae el nombre del parámetro, y
/// cada cambio vuelve a validar desde cero.
/// </summary>
/// <remarks>
/// <see cref="IsValid"/> es el estado semántico y es lo que decide el gate de INICIAR;
/// <see cref="HasIndicator"/> es sólo la señal visual, que se limpia al guardar sin invalidar el valor.
/// </remarks>
public sealed partial class ValidatedFieldViewModel : ObservableObject
{
    private readonly Func<string?, ValidationResult> _validate;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private bool _hasIndicator;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>Crea el campo con su regla de validación.</summary>
    /// <param name="label">Nombre visible del parámetro; también encabeza el mensaje de error.</param>
    /// <param name="validate">Regla que decide si el valor es aceptable.</param>
    /// <param name="isEnabled">Falso para los campos que el diseño deshabilita.</param>
    /// <param name="hint">Texto de ayuda opcional.</param>
    public ValidatedFieldViewModel(
        string label,
        Func<string?, ValidationResult> validate,
        bool isEnabled = true,
        string? hint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(validate);

        Label = label;
        Hint = hint ?? string.Empty;
        IsEnabled = isEnabled;
        _validate = validate;
        PendingValidation = Task.CompletedTask;
    }

    /// <summary>Nombre visible del parámetro.</summary>
    public string Label { get; }

    /// <summary>Texto de ayuda opcional.</summary>
    public string Hint { get; }

    /// <summary>Se dispara cada vez que termina una validación.</summary>
    public event EventHandler? ValidationCompleted;

    /// <summary>Última validación en curso, para que las pruebas puedan esperarla.</summary>
    public Task PendingValidation { get; private set; }

    /// <summary>Resultado de la última validación terminada.</summary>
    public ValidationResult LastResult { get; private set; } = ValidationResult.Valid;

    /// <summary>Indica si el campo muestra el borde y el mensaje del Anexo E.</summary>
    public bool ShowsError => HasIndicator && !IsValid;

    /// <summary>Indica si el campo muestra el borde verde del Anexo E.</summary>
    public bool ShowsSuccess => HasIndicator && IsValid;

    /// <summary>Valida el valor actual y actualiza el estado visible.</summary>
    public async Task RevalidateAsync()
    {
        IsValid = false;
        IsValidating = true;

        string current = Value;

        try
        {
            ValidationResult result = await Task.Run(() => Validate(current)).ConfigureAwait(true);

            if (!string.Equals(Value, current, StringComparison.Ordinal))
            {
                return;
            }

            Apply(result);
        }
        finally
        {
            if (string.Equals(Value, current, StringComparison.Ordinal))
            {
                IsValidating = false;
            }
        }
    }

    /// <summary>Limpia el indicador visual sin cambiar el resultado semántico (Anexo E, estado guardado).</summary>
    public void ClearIndicator()
    {
        HasIndicator = false;
        Message = string.Empty;
    }

    partial void OnValueChanged(string value)
    {
        IsValid = false;
        PendingValidation = RevalidateAsync();
    }

    partial void OnIsValidChanged(bool value) => OnPropertyChanged(nameof(ShowsError));

    partial void OnHasIndicatorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsError));
        OnPropertyChanged(nameof(ShowsSuccess));
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(ShowsError));

    private ValidationResult Validate(string value)
    {
        try
        {
            return _validate(value);
        }
        catch (Exception)
        {
            // Una regla no debe tumbar la pantalla: el campo queda inválido con un motivo concreto.
            return ValidationResult.Invalid(Label, "no se pudo validar el valor.");
        }
    }

    private void Apply(ValidationResult result)
    {
        LastResult = result;
        IsValid = result.IsValid;
        Message = result.Message;
        HasIndicator = true;

        ValidationCompleted?.Invoke(this, EventArgs.Empty);
    }
}
