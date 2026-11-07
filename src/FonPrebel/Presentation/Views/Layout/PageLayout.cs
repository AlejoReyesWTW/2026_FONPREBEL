using System.Windows;
using System.Windows.Controls;

namespace FonPrebel.Presentation.Views.Layout;

/// <summary>
/// Forma en que el cuerpo de la página ocupa el alto. <see cref="Scroll"/> deja que el contenido crezca y
/// la página se desplaza; <see cref="Fill"/> mide el contenido con el alto visible, para que las vistas de
/// paneles repartan ese alto entre filas y no crezcan con todo su texto.
/// </summary>
public enum PageBodyMode
{
    /// <summary>El contenido crece a lo que necesite y el cuerpo se desplaza en vertical.</summary>
    Scroll,

    /// <summary>El contenido se mide con el alto visible: las filas proporcionales reparten la pantalla.</summary>
    Fill,
}

/// <summary>
/// S25: patrón único de distribución de todas las pantallas. Entrega un encabezado fijo (título y
/// descripción de la sección), un cuerpo desplazable y un pie fijo, los tres dentro de la misma columna
/// centrada con ancho máximo, para que ninguna vista repita medidas ni deje el contenido pegado al borde
/// superior izquierdo.
///
/// Criterio de ancho: el contenido crece hasta <see cref="ContentMaxWidth"/> y queda centrado cuando la
/// ventana es más ancha, de modo que en una pantalla grande no se desparrama; en la ventana angosta el
/// relleno lateral (<see cref="PagePadding"/>) evita que el contenido toque los bordes. El cuerpo sólo se
/// desplaza en vertical: al no permitir desplazamiento horizontal se mide con el ancho real de la ventana
/// y los paneles que reparten el ancho disponible pueden apilarse cuando ese ancho no alcanza.
///
/// La plantilla que hace todo esto vive en <c>Layout/PageLayout.xaml</c>. Las demás piezas del patrón son
/// <see cref="SpacedStackPanel"/> (separación vertical uniforme) y <see cref="ResponsiveColumnsPanel"/>
/// (columnas que reparten el ancho y se apilan en ventana angosta).
/// </summary>
public class PageLayout : ContentControl
{
    /// <summary>Título de la sección. En las vistas se enlaza con el título del ViewModel.</summary>
    public static readonly DependencyProperty HeaderTitleProperty = DependencyProperty.Register(
        nameof(HeaderTitle),
        typeof(object),
        typeof(PageLayout),
        new PropertyMetadata(null));

    /// <summary>Descripción de la sección, debajo del título.</summary>
    public static readonly DependencyProperty HeaderSubtitleProperty = DependencyProperty.Register(
        nameof(HeaderSubtitle),
        typeof(object),
        typeof(PageLayout),
        new PropertyMetadata(null));

    /// <summary>
    /// <see cref="PageBodyMode.Scroll"/> por omisión. Las vistas de formularios y tarjetas la usan; las de
    /// paneles que reparten el alto (Logs) usan <see cref="PageBodyMode.Fill"/>.
    /// </summary>
    public static readonly DependencyProperty BodyModeProperty = DependencyProperty.Register(
        nameof(BodyMode),
        typeof(PageBodyMode),
        typeof(PageLayout),
        new PropertyMetadata(PageBodyMode.Scroll));

    /// <summary>Pie fijo de la sección (por ejemplo la barra de acciones). Sin valor, no ocupa alto.</summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer),
        typeof(object),
        typeof(PageLayout),
        new PropertyMetadata(null));

    /// <summary>
    /// Ancho máximo de la columna de contenido, en píxeles independientes del dispositivo. Criterio:
    /// 900 a 1000 para formularios (líneas de lectura cortas), hasta 1200 para las vistas de tarjetas y
    /// paneles, que necesitan repartir ancho entre columnas. Por omisión, 960.
    /// </summary>
    public static readonly DependencyProperty ContentMaxWidthProperty = DependencyProperty.Register(
        nameof(ContentMaxWidth),
        typeof(double),
        typeof(PageLayout),
        new PropertyMetadata(960d));

    /// <summary>
    /// Relleno lateral de la columna de contenido. Criterio: 32 en formularios y vistas de tarjetas, 40 a
    /// 48 en las vistas que se leen de borde a borde. Por omisión, 32.
    /// </summary>
    public static readonly DependencyProperty PagePaddingProperty = DependencyProperty.Register(
        nameof(PagePadding),
        typeof(Thickness),
        typeof(PageLayout),
        new PropertyMetadata(new Thickness(32d, 0d, 32d, 0d)));

    public object? HeaderTitle
    {
        get => GetValue(HeaderTitleProperty);
        set => SetValue(HeaderTitleProperty, value);
    }

    public object? HeaderSubtitle
    {
        get => GetValue(HeaderSubtitleProperty);
        set => SetValue(HeaderSubtitleProperty, value);
    }

    public PageBodyMode BodyMode
    {
        get => (PageBodyMode)GetValue(BodyModeProperty);
        set => SetValue(BodyModeProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public double ContentMaxWidth
    {
        get => (double)GetValue(ContentMaxWidthProperty);
        set => SetValue(ContentMaxWidthProperty, value);
    }

    public Thickness PagePadding
    {
        get => (Thickness)GetValue(PagePaddingProperty);
        set => SetValue(PagePaddingProperty, value);
    }
}
