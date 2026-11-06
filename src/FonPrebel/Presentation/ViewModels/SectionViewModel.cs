using CommunityToolkit.Mvvm.ComponentModel;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Base de los ViewModels de sección. Cada sección muestra su título y una descripción de lo que
/// hará; las funcionalidades concretas se agregan tarea por tarea.
/// </summary>
public abstract class SectionViewModel : ObservableObject
{
    protected SectionViewModel(string sectionKey, string title, string description)
    {
        SectionKey = sectionKey;
        Title = title;
        Description = description;
    }

    /// <summary>Clave de la sección, coincide con <c>SectionCatalog</c>.</summary>
    public string SectionKey { get; }

    /// <summary>Título visible de la sección.</summary>
    public string Title { get; }

    /// <summary>Qué hace esta sección, en lenguaje del negocio.</summary>
    public string Description { get; }
}
