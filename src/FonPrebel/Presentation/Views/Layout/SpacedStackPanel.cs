using System.Windows;
using System.Windows.Controls;

namespace FonPrebel.Presentation.Views.Layout;

/// <summary>
/// S25: apila a sus hijos en vertical con una separación uniforme entre bloques. Existe porque
/// <see cref="StackPanel"/> no tiene separación: sin esta pieza cada vista copiaría márgenes distintos y
/// la separación dejaría de ser consistente. Los hijos con <see cref="UIElement.Visibility"/> colapsada no
/// cuentan ni ocupan separación.
/// </summary>
public class SpacedStackPanel : Panel
{
    /// <summary>Separación vertical entre bloques, en píxeles. Por omisión, 16.</summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(SpacedStackPanel),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double spacing = Math.Max(0d, Spacing);
        double width = 0d;
        double height = 0d;
        int visible = 0;

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Measure(new Size(0d, 0d));
                continue;
            }

            if (visible > 0)
            {
                height += spacing;
            }

            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
            visible++;
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double spacing = Math.Max(0d, Spacing);
        double y = 0d;

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            double childHeight = child.DesiredSize.Height;
            child.Arrange(new Rect(0d, y, finalSize.Width, childHeight));
            y += childHeight + spacing;
        }

        return finalSize;
    }
}
