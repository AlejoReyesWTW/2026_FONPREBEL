using CommunityToolkit.Mvvm.ComponentModel;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Base de las secciones colapsables de la pantalla Configuración (V4). Cada sección arma sus campos,
/// los carga desde el almacén, los guarda y avisa cuando alguno cambia de estado para que el gate de
/// INICIAR se vuelva a calcular.
/// </summary>
public abstract class SettingsSectionViewModel : ObservableObject
{
    private readonly List<ValidatedFieldViewModel> _fields = new();
    private readonly TimeSpan? _minimumIndicatorDuration;

    /// <summary>Crea la sección con el tiempo mínimo del indicador de sus campos (§2.9.2 r18).</summary>
    /// <param name="minimumIndicatorDuration">Tiempo mínimo visible; <c>null</c> usa el valor por omisión.</param>
    protected SettingsSectionViewModel(TimeSpan? minimumIndicatorDuration = null)
    {
        _minimumIndicatorDuration = minimumIndicatorDuration;
    }

    /// <summary>Campos de la sección, en el orden en que se muestran.</summary>
    public IReadOnlyList<ValidatedFieldViewModel> Fields => _fields;

    /// <summary>Se dispara cuando un campo cambia su estado de validación.</summary>
    public event EventHandler? FieldStateChanged;

    /// <summary>Indica si todos los campos de la sección están validados y son válidos.</summary>
    public bool IsValid => _fields.All(field => field.IsValid && !field.IsValidating);

    /// <summary>Fija los valores almacenados tras cargar o guardar y deja los campos en sólo lectura.</summary>
    public virtual void AcceptStoredValues()
    {
        foreach (ValidatedFieldViewModel field in _fields)
        {
            field.AcceptStoredValue();
        }
    }

    /// <summary>Carga los valores guardados; devuelve el motivo si el almacén no responde.</summary>
    public abstract Task<ConfigurationStoreOutcome> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Guarda los valores válidos de la sección; devuelve el motivo si el almacén no responde.</summary>
    public abstract Task<ConfigurationWriteResult> SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Vuelve a validar todos los campos de la sección y espera el resultado.</summary>
    /// <remarks>
    /// En paralelo: el indicador de cada campo se mantiene visible un tiempo mínimo (§2.9.2 r18), así que
    /// validar uno por uno multiplicaría esa espera por la cantidad de campos.
    /// </remarks>
    public Task RevalidateAllAsync() =>
        Task.WhenAll(_fields.Select(field => field.RevalidateAsync()));

    /// <summary>Limpia los indicadores visuales después de guardar (Anexo E, estado guardado).</summary>
    public void ClearIndicators()
    {
        foreach (ValidatedFieldViewModel field in _fields)
        {
            field.ClearIndicator();
        }
    }

    /// <summary>Agrega un campo a la sección y conecta su aviso de validación.</summary>
    protected ValidatedFieldViewModel AddField(
        string label,
        Func<string?, ValidationResult> validate,
        bool isEnabled = true,
        string? hint = null)
    {
        ValidatedFieldViewModel field = new(label, validate, isEnabled, hint, _minimumIndicatorDuration);

        field.ValidationCompleted += (_, _) => FieldStateChanged?.Invoke(this, EventArgs.Empty);
        field.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ValidatedFieldViewModel.IsValid)
                or nameof(ValidatedFieldViewModel.IsValidating))
            {
                FieldStateChanged?.Invoke(this, EventArgs.Empty);
            }
        };

        _fields.Add(field);

        return field;
    }

    /// <summary>Texto del campo a partir de un entero guardado; vacío cuando el parámetro está pendiente.</summary>
    protected static string ToText(ConfigurationIntResult result) =>
        result.Succeeded && result.Value is int number
            ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;

    /// <summary>
    /// §2.9.2 r16: un campo vacío es un parámetro pendiente, así que se guarda como nulo y no bloquea; un
    /// valor escrito que no es un entero es inválido y no se persiste. Devuelve falso sólo en ese segundo caso.
    /// </summary>
    protected static bool TryReadPendingNumber(ValidatedFieldViewModel field, out int? value)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (string.IsNullOrWhiteSpace(field.Value))
        {
            value = null;

            return true;
        }

        if (NumericText.TryParseInt(field.Value, out int parsed))
        {
            value = parsed;

            return true;
        }

        value = null;

        return false;
    }

    /// <summary>Texto del campo a partir de texto guardado; vacío cuando el parámetro está pendiente.</summary>
    protected static string ToText(ConfigurationReadResult<string> result) =>
        result.Succeeded ? result.Value ?? string.Empty : string.Empty;
}
