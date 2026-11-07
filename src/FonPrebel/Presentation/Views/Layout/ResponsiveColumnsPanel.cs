using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace FonPrebel.Presentation.Views.Layout;

/// <summary>
/// S25: reparte el ancho disponible entre sus hijos en columnas y, cuando la ventana es más angosta que
/// <see cref="StackThreshold"/>, los apila en una sola columna para que nada quede amontonado. Es la pieza
/// que hace que las tarjetas y los paneles ocupen el ancho en 1180x760 y pasen a una columna en el modo
/// compacto, y la que alinea etiqueta y campo en los formularios.
/// </summary>
public class ResponsiveColumnsPanel : Panel
{
    /// <summary>
    /// Ancho de cada columna separado por comas: un número es ancho fijo, <c>*</c> es la parte
    /// proporcional del espacio que sobra y <c>Auto</c> es el ancho que pide el hijo. Por omisión, <c>*</c>.
    /// </summary>
    public static readonly DependencyProperty ColumnPatternProperty = DependencyProperty.Register(
        nameof(ColumnPattern),
        typeof(string),
        typeof(ResponsiveColumnsPanel),
        new FrameworkPropertyMetadata("*", FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Separación entre columnas y, cuando se apila, entre filas. Por omisión, 16.</summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(ResponsiveColumnsPanel),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Ancho a partir del cual se usan columnas. Por debajo, los hijos se apilan. Por omisión, 720: en
    /// 1180x760 el área de contenido ronda los 850 píxeles, así que las vistas conservan sus columnas y en
    /// el modo compacto (640 de ancho con la barra lateral abierta) pasan a una sola.
    /// </summary>
    public static readonly DependencyProperty StackThresholdProperty = DependencyProperty.Register(
        nameof(StackThreshold),
        typeof(double),
        typeof(ResponsiveColumnsPanel),
        new FrameworkPropertyMetadata(720d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Techo de alto de cada hijo cuando están apilados. Sin valor, cada hijo ocupa el alto que pide. Se usa
    /// en los paneles que reparten su alto en filas (por ejemplo las dos bitácoras) para que, ya apilados,
    /// conserven un área acotada con desplazamiento propio en lugar de crecer con todo el texto; si el alto
    /// disponible no alcanza para el techo, se reparte lo que hay y no queda contenido recortado.
    /// </summary>
    public static readonly DependencyProperty StackedItemHeightProperty = DependencyProperty.Register(
        nameof(StackedItemHeight),
        typeof(double),
        typeof(ResponsiveColumnsPanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public string ColumnPattern
    {
        get => (string)GetValue(ColumnPatternProperty);
        set => SetValue(ColumnPatternProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double StackThreshold
    {
        get => (double)GetValue(StackThresholdProperty);
        set => SetValue(StackThresholdProperty, value);
    }

    public double StackedItemHeight
    {
        get => (double)GetValue(StackedItemHeightProperty);
        set => SetValue(StackedItemHeightProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double spacing = Math.Max(0d, Spacing);
        double width = double.IsInfinity(availableSize.Width) ? 0d : availableSize.Width;
        List<UIElement> children = VisibleChildren();

        if (children.Count == 0)
        {
            return default;
        }

        if (ShouldStack(width, children.Count))
        {
            double height = 0d;
            double stackedHeight = StackedChildHeight(availableSize.Height, children.Count, spacing);

            for (int index = 0; index < children.Count; index++)
            {
                if (index > 0)
                {
                    height += spacing;
                }

                children[index].Measure(new Size(width, stackedHeight));
                height += double.IsInfinity(stackedHeight)
                    ? children[index].DesiredSize.Height
                    : stackedHeight;
            }

            return new Size(width, height);
        }

        double[] widths = ColumnWidths(width, children, spacing);
        double rowHeight = 0d;

        for (int index = 0; index < children.Count; index++)
        {
            children[index].Measure(new Size(widths[index], availableSize.Height));
            rowHeight = Math.Max(rowHeight, children[index].DesiredSize.Height);
        }

        return new Size(width, rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double spacing = Math.Max(0d, Spacing);
        List<UIElement> children = VisibleChildren();

        if (children.Count == 0)
        {
            return finalSize;
        }

        if (ShouldStack(finalSize.Width, children.Count))
        {
            double y = 0d;
            double stackedHeight = StackedChildHeight(finalSize.Height, children.Count, spacing);

            foreach (UIElement child in children)
            {
                double childHeight = double.IsInfinity(stackedHeight) ? child.DesiredSize.Height : stackedHeight;
                child.Arrange(new Rect(0d, y, finalSize.Width, childHeight));
                y += childHeight + spacing;
            }

            return finalSize;
        }

        double[] widths = ColumnWidths(finalSize.Width, children, spacing);
        double x = 0d;

        for (int index = 0; index < children.Count; index++)
        {
            children[index].Arrange(new Rect(x, 0d, widths[index], finalSize.Height));
            x += widths[index] + spacing;
        }

        return finalSize;
    }

    /// <summary>
    /// Alto de cada hijo apilado. <see cref="StackedItemHeight"/> es el techo; si el alto disponible es
    /// finito se reparte entre los hijos, de modo que en una ventana angosta los paneles se achican en vez de
    /// quedar recortados. Sin techo configurado, cada hijo ocupa el alto que pide.
    /// </summary>
    private double StackedChildHeight(double availableHeight, int childCount, double spacing)
    {
        if (StackedItemHeight <= 0d)
        {
            return double.PositiveInfinity;
        }

        if (double.IsInfinity(availableHeight) || childCount == 0)
        {
            return StackedItemHeight;
        }

        double share = Math.Max(0d, (availableHeight - (spacing * (childCount - 1))) / childCount);
        return Math.Min(StackedItemHeight, share);
    }

    private bool ShouldStack(double width, int childCount)
    {
        if (childCount <= 1 || StackThreshold <= 0d)
        {
            return false;
        }

        return width <= 0d || width < StackThreshold;
    }

    private double[] ColumnWidths(double width, List<UIElement> children, double spacing)
    {
        GridLength[] lengths = ParsePattern(ColumnPattern, children.Count);
        double[] widths = new double[children.Count];
        double fixedWidth = 0d;
        double starWeight = 0d;
        double autoWidth = 0d;

        for (int index = 0; index < children.Count; index++)
        {
            GridLength length = lengths[index];

            switch (length.GridUnitType)
            {
                case GridUnitType.Pixel:
                    widths[index] = length.Value;
                    fixedWidth += length.Value;
                    break;

                case GridUnitType.Auto:
                    children[index].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    widths[index] = children[index].DesiredSize.Width;
                    autoWidth += widths[index];
                    break;

                default:
                    starWeight += length.Value > 0d ? length.Value : 1d;
                    break;
            }
        }

        double remaining = width - (spacing * (children.Count - 1)) - fixedWidth - autoWidth;
        double unit = starWeight > 0d && remaining > 0d ? remaining / starWeight : 0d;

        for (int index = 0; index < children.Count; index++)
        {
            if (lengths[index].GridUnitType == GridUnitType.Star)
            {
                double weight = lengths[index].Value > 0d ? lengths[index].Value : 1d;
                widths[index] = Math.Max(0d, unit * weight);
            }
        }

        return widths;
    }

    private static GridLength[] ParsePattern(string? pattern, int count)
    {
        GridLength[] lengths = new GridLength[count];
        string[] parts = (pattern ?? string.Empty).Split(',');

        for (int index = 0; index < count; index++)
        {
            lengths[index] = index < parts.Length
                ? ParseLength(parts[index])
                : new GridLength(1d, GridUnitType.Star);
        }

        return lengths;
    }

    private static GridLength ParseLength(string part)
    {
        string token = part.Trim();

        if (token.Length == 0 || token == "*")
        {
            return new GridLength(1d, GridUnitType.Star);
        }

        if (token.EndsWith('*') &&
            double.TryParse(
                token[..^1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double weight))
        {
            return new GridLength(weight > 0d ? weight : 1d, GridUnitType.Star);
        }

        if (string.Equals(token, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return GridLength.Auto;
        }

        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? new GridLength(Math.Max(0d, value), GridUnitType.Pixel)
            : new GridLength(1d, GridUnitType.Star);
    }

    private List<UIElement> VisibleChildren()
    {
        List<UIElement> visible = new(InternalChildren.Count);

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                visible.Add(child);
            }
            else
            {
                child.Measure(new Size(0d, 0d));
            }
        }

        return visible;
    }
}
