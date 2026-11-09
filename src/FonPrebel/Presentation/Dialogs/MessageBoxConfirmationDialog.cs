using System.Windows;
using FonPrebel.Application.Interfaces;

namespace FonPrebel.Presentation.Dialogs;

/// <summary>
/// Implementación de <see cref="IConfirmationDialog"/> con el cuadro de mensaje de WPF: un aviso modal con
/// las acciones Sí y No, que devuelve verdadero sólo cuando el analista confirma (§2.9.2 r12).
/// </summary>
public sealed class MessageBoxConfirmationDialog : IConfirmationDialog
{
    /// <inheritdoc />
    public bool Confirm(string title, string message)
    {
        MessageBoxResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
