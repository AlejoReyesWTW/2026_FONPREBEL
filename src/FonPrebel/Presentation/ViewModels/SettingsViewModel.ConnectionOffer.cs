using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Oferta de comprobación de conexiones posterior al guardado (§2.9.2 r2, tarea A12): con los datos ya
/// guardados la pantalla ofrece probar el servidor de archivos y el correo. Se ofrece y no se ejecuta
/// sola, porque una comprobación real toca la red y tarda.
/// </summary>
public sealed partial class SettingsViewModel
{
    /// <summary>
    /// Texto de la oferta que aparece tras guardar (§2.9.2 r2): comprobar las conexiones con los datos
    /// recién guardados. Se ofrece, no se ejecuta sola, porque una comprobación real toca la red y tarda.
    /// </summary>
    public string ConnectionOfferMessage { get; } =
        "¿Quieres comprobar ahora el servidor de archivos y el correo con los datos guardados?";

    /// <summary>Verdadero mientras la oferta de comprobar las conexiones está a la vista.</summary>
    [ObservableProperty]
    private bool _hasConnectionOffer;

    /// <summary>Resultado de la comprobación ofrecida, una línea por destino; vacío mientras no se ejecutó.</summary>
    [ObservableProperty]
    private string _connectionOfferResultText = string.Empty;

    /// <summary>Verdadero cuando la comprobación ofrecida dejó un resultado para mostrar.</summary>
    [ObservableProperty]
    private bool _hasConnectionOfferResult;

    /// <summary>
    /// Corre la comprobación de los destinos que el monitor ya declara, sin repetir sus claves acá, y deja
    /// el resultado visible con el motivo de los que no se pudieron comprobar.
    /// </summary>
    [RelayCommand]
    private async Task OfferConnectionTest()
    {
        IReadOnlyList<ConnectionTargetSnapshot> targets = _connectionMonitor.Snapshots;

        if (targets.Count == 0)
        {
            HasConnectionOffer = false;

            return;
        }

        HasConnectionOffer = false;
        IsBusy = true;

        try
        {
            foreach (ConnectionTargetSnapshot target in targets)
            {
                await _connectionMonitor.CheckNowAsync(target.TargetKey).ConfigureAwait(true);
            }

            ConnectionOfferResultText = DescribeConnectionOfferResult(_connectionMonitor.Snapshots);
        }
        finally
        {
            IsBusy = false;
        }

        HasConnectionOfferResult = ConnectionOfferResultText.Length > 0;
    }

    /// <summary>Cierra la oferta sin comprobar nada.</summary>
    [RelayCommand]
    private void DismissConnectionOffer()
    {
        HasConnectionOffer = false;
        HasConnectionOfferResult = false;
        ConnectionOfferResultText = string.Empty;
    }

    /// <summary>Una línea por destino con el estado informado y su motivo cuando corresponde.</summary>
    private static string DescribeConnectionOfferResult(IReadOnlyList<ConnectionTargetSnapshot> snapshots) =>
        string.Join(
            Environment.NewLine,
            snapshots.Select(snapshot =>
            {
                string state = ConnectionStatusText.For(snapshot.Status);
                string? reason = snapshot.LastResult?.Reason;

                return string.IsNullOrWhiteSpace(reason)
                    ? $"{snapshot.TargetName}: {state}."
                    : $"{snapshot.TargetName}: {state} — {reason}.";
            }));
}
