using FonPrebel.Presentation.Navigation;

namespace FonPrebel.Tests;

/// <summary>
/// Fija el requisito S16: la barra lateral tiene siete secciones, en el orden y con los títulos
/// del negocio, con Inicio de sesión al final y deshabilitado.
/// </summary>
public sealed class SectionCatalogTests
{
    private static readonly string[] ExpectedKeys =
    {
        "execution",
        "history",
        "logs",
        "settings",
        "metrics",
        "connections",
        "login",
    };

    private static readonly string[] ExpectedTitles =
    {
        "Ejecución",
        "Historial",
        "Logs",
        "Configuración",
        "Métricas",
        "Conexiones",
        "Inicio de sesión",
    };

    [Fact]
    public void Sections_contains_exactly_seven_sections()
    {
        Assert.Equal(7, SectionCatalog.Sections.Count);
    }

    [Fact]
    public void Sections_keep_the_order_required_by_the_spec()
    {
        string[] keys = SectionCatalog.Sections.Select(section => section.Key).ToArray();

        Assert.Equal(ExpectedKeys, keys);
    }

    [Fact]
    public void Sections_show_the_business_titles()
    {
        string[] titles = SectionCatalog.Sections.Select(section => section.Title).ToArray();

        Assert.Equal(ExpectedTitles, titles);
    }

    [Fact]
    public void Only_the_last_section_is_disabled()
    {
        SectionDefinition last = SectionCatalog.Sections[^1];

        Assert.Equal("login", last.Key);
        Assert.False(last.IsEnabled);

        SectionDefinition[] enabled = SectionCatalog.Sections
            .Take(SectionCatalog.Sections.Count - 1)
            .ToArray();

        Assert.All(enabled, section => Assert.True(section.IsEnabled, $"La sección '{section.Key}' debe estar habilitada."));
    }

    [Fact]
    public void Section_keys_are_unique()
    {
        IReadOnlyList<SectionDefinition> sections = SectionCatalog.Sections;
        int distinctKeys = sections.Select(section => section.Key).Distinct(StringComparer.Ordinal).Count();

        Assert.Equal(sections.Count, distinctKeys);
    }

    [Fact]
    public void Footer_contains_only_the_login_section()
    {
        Assert.Equal(new[] { "login" }, SectionCatalog.FooterSectionKeys.ToArray());
    }
}
