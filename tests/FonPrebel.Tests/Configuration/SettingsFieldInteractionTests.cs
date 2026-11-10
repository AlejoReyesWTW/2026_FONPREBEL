using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FonPrebel.Presentation.ViewModels.Config;
using FonPrebel.Presentation.Views;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S30 (§2.9.2 r19): la edición de un campo no atrapa el foco. Estas comprobaciones son estructurales
/// sobre el árbol visual de la vista —lo único observable sin mover el mouse de la máquina— y fijan las
/// tres condiciones que la regla necesita: el campo acepta el clic aun en sólo lectura, nada que el
/// analista pueda estar pulsando desaparece del árbol al entrar en edición y el indicador de progreso no
/// se superpone al área de texto del campo.
/// </summary>
public sealed class SettingsFieldInteractionTests
{
    /// <summary>Renglón de etiqueta y campo: 240 fijos y el resto para el campo.</summary>
    private const string EditToolTip = "Editar este campo";

    [Fact]
    public async Task Every_field_accepts_a_click_while_it_is_read_only()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        RunOnUiThread(() =>
        {
            SettingsView view = CreateView(context);
            // Sólo los campos del formulario: las contraseñas usan PasswordBox, que no son éstos.
            List<TextBox> fields = FindVisualChildren<TextBox>(view)
                .Where(textBox => textBox.DataContext is ValidatedFieldViewModel)
                .ToList();

            Assert.Equal(context.Settings.AllFields.Count, fields.Count);
            Assert.All(fields, field => Assert.True(field.IsReadOnly, "Al abrir la sección los campos están en sólo lectura."));

            // §2.9.2 r19: en sólo lectura el campo sigue recibiendo el clic, así el cursor sale del campo
            // anterior con el mouse; no está deshabilitado ni fuera del alcance del puntero.
            Assert.All(fields, field => Assert.True(field.IsHitTestVisible, "El campo tiene que aceptar el clic."));
            Assert.All(fields, field => Assert.True(field.Focusable, "El campo tiene que poder recibir el foco."));
            Assert.All(fields, field => Assert.True(field.IsTabStop));
        });
    }

    [Fact]
    public async Task The_edit_action_does_not_leave_the_tree_when_the_field_enters_edit_mode()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        ValidatedFieldViewModel route1 = context.Settings.Routes.Route1;

        RunOnUiThread(() =>
        {
            SettingsView view = CreateView(context);
            Button pencil = FindEditButton(view, route1);

            Assert.Equal(Visibility.Visible, pencil.Visibility);

            route1.BeginEditCommand.Execute(null);
            view.UpdateLayout();

            Assert.True(route1.IsEditing);
            Assert.NotEqual(Visibility.Collapsed, pencil.Visibility);
            Assert.False(pencil.IsEnabled);
        });
    }

    [Fact]
    public async Task The_progress_indicator_does_not_overlap_the_text_of_the_field()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        RunOnUiThread(() =>
        {
            SettingsView view = CreateView(context);
            // 16 px es el tamaño del indicador del campo (ProgressRingStyles.xaml); sirve para distinguirlo
            // del indicador de sección, que no vive en una columna del campo.
            List<FrameworkElement> rings = FindVisualChildren<FrameworkElement>(view)
                .Where(element => element.GetType().Name == "ProgressRing" && element.Width == 16d)
                .ToList();

            Assert.Equal(context.Settings.AllFields.Count, rings.Count);
            Assert.All(rings, ring =>
            {
                Assert.True(VisualTreeHelper.GetParent(ring) is Grid);
                Grid container = (Grid)VisualTreeHelper.GetParent(ring);

                Assert.True(container.ColumnDefinitions.Count >= 2);
                Assert.True(Grid.GetColumn(ring) > 0);
            });
        });
    }

    private static SettingsView CreateView(ConfigurationTestContext context)
    {
        SettingsView view = new() { DataContext = context.Settings, Width = 1180, Height = 760 };
        view.Measure(new Size(1180, 760));
        view.Arrange(new Rect(0, 0, 1180, 760));
        view.UpdateLayout();

        return view;
    }

    private static Button FindEditButton(DependencyObject root, ValidatedFieldViewModel field) =>
        FindVisualChildren<Button>(root)
            .Single(button => button.ToolTip as string == EditToolTip && ReferenceEquals(button.DataContext, field));

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Ejecuta el cuerpo en un hilo STA, como hace WPF al mostrar la sección.</summary>
    private static void RunOnUiThread(Action body)
    {
        Exception? failure = null;

        Thread thread = new(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "El hilo de interfaz no terminó a tiempo.");

        if (failure is not null)
        {
            throw failure;
        }
    }
}
