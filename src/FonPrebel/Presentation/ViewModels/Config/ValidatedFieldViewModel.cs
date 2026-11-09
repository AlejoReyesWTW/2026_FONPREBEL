using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Anexo E: un campo con su propia validación. Mientras valida muestra un <c>ProgressRing</c> de 12 px;
/// al terminar queda en verde o en rojo con el mensaje concreto que trae el nombre del parámetro, y
/// cada cambio vuelve a validar desde cero.
/// </summary>
/// <remarks>
/// <see cref="IsValid"/> dice si el valor pasa su comprobación; el gate de INICIAR exige además
/// <see cref="IsFilled"/>, porque un obligatorio vacío no es un valor válido sino un pendiente
/// (§2.9.2 r16). <see cref="HasIndicator"/> es sólo la señal visual, que se limpia al guardar sin
/// invalidar el valor.
/// </remarks>
public sealed partial class ValidatedFieldViewModel : ObservableObject
{
    /// <summary>
    /// §2.9.2 r18: tiempo mínimo que el indicador queda visible. Una comprobación que termina antes de
    /// que el usuario alcance a verla no comunica nada.
    /// </summary>
    public static readonly TimeSpan DefaultMinimumIndicatorDuration = TimeSpan.FromMilliseconds(400);

    private readonly Func<string?, ValidationResult> _validate;
    private readonly TimeSpan _minimumIndicatorDuration;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isEditing;

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
    /// <param name="minimumIndicatorDuration">
    /// Tiempo mínimo que el indicador permanece visible (§2.9.2 r18); <c>null</c> usa el valor por omisión.
    /// </param>
    public ValidatedFieldViewModel(
        string label,
        Func<string?, ValidationResult> validate,
        bool isEnabled = true,
        string? hint = null,
        TimeSpan? minimumIndicatorDuration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(validate);

        Label = label;
        Hint = hint ?? string.Empty;
        IsEnabled = isEnabled;
        _validate = validate;
        _minimumIndicatorDuration = minimumIndicatorDuration ?? DefaultMinimumIndicatorDuration;
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

    /// <summary>Valor con el que el campo quedó almacenado en la base (§2.9.2 r10).</summary>
    public string StoredValue { get; private set; } = string.Empty;

    /// <summary>§2.9.2 r10: indica si el valor difiere del almacenado.</summary>
    public bool HasValueChanged => !string.Equals(Value, StoredValue, StringComparison.Ordinal);

    /// <summary>§2.9.2 r14: mientras no se habilite el lápiz, el campo se muestra en sólo lectura.</summary>
    public bool IsReadOnly => !IsEditing;

    /// <summary>§2.9.2 r14: el lápiz sólo está disponible en los campos habilitados y fuera de edición.</summary>
    public bool CanBeginEdit => IsEnabled && !IsEditing;

    /// <summary>§2.9.2 r14: habilita la edición de este campo y sólo de este.</summary>
    [RelayCommand]
    private void BeginEdit()
    {
        if (!CanBeginEdit)
        {
            return;
        }

        IsEditing = true;
    }

    /// <summary>§2.9.2 r14: cancela la edición y devuelve el valor almacenado.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        Value = StoredValue;
        IsEditing = false;
    }

    /// <summary>Fija el valor almacenado tras cargar o guardar y deja el campo en sólo lectura.</summary>
    public void AcceptStoredValue()
    {
        StoredValue = Value;
        IsEditing = false;
        OnPropertyChanged(nameof(HasValueChanged));
    }

    /// <summary>§2.9.2 r16: verdadero cuando el campo tiene un valor escrito.</summary>
    public bool IsFilled => !string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// §2.9.2 r16: verdadero cuando un valor <em>escrito</em> no pasa su comprobación. Es lo único que
    /// impide guardar; un campo vacío es un pendiente y no un bloqueo.
    /// </summary>
    public bool IsBlocking => IsFilled && !IsValid && !IsValidating;

    /// <summary>§2.9.2 r16: obligatorio todavía sin completar; cuenta como configuración incompleta.</summary>
    public bool IsMissing => !IsFilled && !IsValid;

    /// <summary>Indica si el campo muestra el borde y el mensaje rojos del Anexo E.</summary>
    public bool ShowsError => HasIndicator && IsBlocking;

    /// <summary>§2.9.2 r16: indica si el campo muestra el motivo en tono neutro por estar sin completar.</summary>
    public bool ShowsPending => HasIndicator && IsMissing;

    /// <summary>Indica si el campo muestra el borde verde del Anexo E.</summary>
    public bool ShowsSuccess => HasIndicator && IsValid;

    /// <summary>
    /// §2.9.2 r17: motivo de la comprobación sin repetir el nombre del campo, que la pantalla compone con
    /// la etiqueta para nombrar cada campo que impide guardar.
    /// </summary>
    public string Motive => StripLabel(Message);

    /// <summary>Valida el valor actual y actualiza el estado visible.</summary>
    public async Task RevalidateAsync()
    {
        IsValid = false;
        IsValidating = true;

        string current = Value;

        // §2.9.2 r18: el indicador arranca con la comprobación, así una comprobación instantánea también se ve.
        Task minimumIndicator = Task.Delay(_minimumIndicatorDuration);

        try
        {
            ValidationResult result = await Task.Run(() => Validate(current)).ConfigureAwait(true);
            await minimumIndicator.ConfigureAwait(true);

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
        OnPropertyChanged(nameof(HasValueChanged));
        OnPropertyChanged(nameof(IsFilled));
        OnPropertyChanged(nameof(IsBlocking));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(ShowsError));
        OnPropertyChanged(nameof(ShowsPending));
        PendingValidation = RevalidateAsync();
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanBeginEdit));
    }

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(CanBeginEdit));

    partial void OnIsValidChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsError));
        OnPropertyChanged(nameof(ShowsSuccess));
        OnPropertyChanged(nameof(ShowsPending));
        OnPropertyChanged(nameof(IsBlocking));
        OnPropertyChanged(nameof(IsMissing));
    }

    partial void OnIsValidatingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBlocking));
        OnPropertyChanged(nameof(ShowsError));
    }

    partial void OnHasIndicatorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsError));
        OnPropertyChanged(nameof(ShowsSuccess));
        OnPropertyChanged(nameof(ShowsPending));
    }

    partial void OnMessageChanged(string value)
    {
        OnPropertyChanged(nameof(ShowsError));
        OnPropertyChanged(nameof(Motive));
    }

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

    private string StripLabel(string message)
    {
        string prefix = $"{Label}: ";

        return message.StartsWith(prefix, StringComparison.Ordinal)
            ? message[prefix.Length..].TrimEnd()
            : message;
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
