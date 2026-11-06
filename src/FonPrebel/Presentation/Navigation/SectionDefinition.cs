using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.Navigation;

/// <summary>
/// Una sección de la barra lateral de navegación.
/// El orden de <see cref="SectionCatalog.Sections"/> es el orden visible y está fijado por el spec (S16).
/// </summary>
/// <param name="Key">Clave técnica estable, no visible al usuario.</param>
/// <param name="Title">Título visible, en el idioma del negocio.</param>
/// <param name="Icon">Icono Fluent de la sección.</param>
/// <param name="IsEnabled">Una sección deshabilitada muestra su lugar pero no se puede abrir.</param>
public sealed record SectionDefinition(string Key, string Title, SymbolRegular Icon, bool IsEnabled = true);
