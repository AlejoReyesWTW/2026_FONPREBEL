using FonPrebel.Application.Interfaces;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// Diálogo modal de prueba (§2.9.2 r12): registra cuántas veces se pidió confirmación y con qué texto, y
/// responde lo que fije la prueba. No abre ninguna ventana, así que las pruebas corren sin interfaz.
/// </summary>
internal sealed class FakeConfirmationDialog : IConfirmationDialog
{
    /// <summary>Respuesta que devuelve el diálogo; por defecto el analista confirma.</summary>
    public bool Response { get; set; } = true;

    /// <summary>Cantidad de veces que se pidió confirmación.</summary>
    public int RequestCount { get; private set; }

    /// <summary>Título del último aviso mostrado.</summary>
    public string LastTitle { get; private set; } = string.Empty;

    /// <summary>Mensaje del último aviso mostrado.</summary>
    public string LastMessage { get; private set; } = string.Empty;

    public bool Confirm(string title, string message)
    {
        RequestCount++;
        LastTitle = title;
        LastMessage = message;

        return Response;
    }
}
