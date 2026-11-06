namespace FonPrebel.Presentation.Navigation;

/// <summary>
/// Registro único de las secciones de navegación. La UI se construye a partir de esta lista,
/// de modo que el orden y los títulos no puedan desincronizarse entre la vista y el modelo.
/// </summary>
public static class SectionCatalog
{
    /// <summary>Sección que se abre al iniciar la aplicación.</summary>
    public const string DefaultSectionKey = "execution";

    /// <summary>Las siete secciones, en el orden obligatorio del spec.</summary>
    public static IReadOnlyList<SectionDefinition> Sections { get; } = new SectionDefinition[]
    {
        new("execution", "Ejecución", Wpf.Ui.Controls.SymbolRegular.Play24),
        new("history", "Historial", Wpf.Ui.Controls.SymbolRegular.History24),
        new("logs", "Logs", Wpf.Ui.Controls.SymbolRegular.DocumentText24),
        new("settings", "Configuración", Wpf.Ui.Controls.SymbolRegular.Settings24),
        new("metrics", "Métricas", Wpf.Ui.Controls.SymbolRegular.DataBarVertical24),
        new("connections", "Conexiones", Wpf.Ui.Controls.SymbolRegular.PlugConnected24),
        new("login", "Inicio de sesión", Wpf.Ui.Controls.SymbolRegular.ContactCard24, IsEnabled: false),
    };

    /// <summary>Las secciones que van al pie de la barra lateral, separadas del resto.</summary>
    public static IReadOnlyList<string> FooterSectionKeys { get; } = new[] { "login" };
}
