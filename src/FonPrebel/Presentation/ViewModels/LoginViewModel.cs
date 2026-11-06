namespace FonPrebel.Presentation.ViewModels;

/// <summary>Sección Inicio de sesión: reservada para la etapa final, no funcional a propósito.</summary>
public sealed class LoginViewModel : SectionViewModel
{
    public LoginViewModel()
        : base("login", "Inicio de sesión",
               "Módulo reservado para la etapa final, contra la base central de SQL Server. Todavía no está habilitado y no bloquea el uso del aplicativo.")
    {
    }
}
