namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Confirmación modal de la interfaz (§2.9.2 r12). Existe como contrato para que la lógica de la pantalla
/// decida <b>qué</b> preguntar y el analista responda; la implementación concreta vive en Presentation y
/// las pruebas usan una falsa, sin abrir ventanas.
/// </summary>
public interface IConfirmationDialog
{
    /// <summary>Muestra un aviso modal con dos acciones y devuelve verdadero si el analista confirma.</summary>
    /// <param name="title">Título breve del aviso.</param>
    /// <param name="message">Texto completo de la advertencia.</param>
    /// <returns>Verdadero cuando el analista confirma; falso cuando cancela.</returns>
    bool Confirm(string title, string message);
}
