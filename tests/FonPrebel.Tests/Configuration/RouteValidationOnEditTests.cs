using System.IO;
using FonPrebel.Application.Validation;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.ViewModels.Config;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S28 (§2.9.2 r15): al editar un campo de ruta se comprueba en el acto que exista y que se pueda leer o
/// escribir, el propio campo informa que está validando mientras tanto, y una ruta inexistente o sin
/// permiso lo deja inválido con su motivo e impide que el guardado se complete.
/// </summary>
public sealed class RouteValidationOnEditTests
{
    [Fact]
    public async Task An_inexistent_route_ends_invalid_with_its_reason_and_the_save_does_not_persist()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();
        int writes = context.WriteCount;

        ValidatedFieldViewModel route1 = context.Settings.Routes.Route1;

        route1.BeginEditCommand.Execute(null);
        route1.Value = Path.Combine(context.Database.Paths.Root, "consolidado-inexistente.xlsx");
        await route1.PendingValidation;

        Assert.False(route1.IsValid);
        Assert.True(route1.ShowsError);
        Assert.Contains(FileRouteValidation.Route1Parameter, route1.Message, StringComparison.Ordinal);
        Assert.Contains("no existe un archivo en esa ruta", route1.Message, StringComparison.Ordinal);

        bool saved = await context.Settings.SaveAsync();

        Assert.False(saved);
        Assert.Equal(writes, context.WriteCount);
        Assert.Equal(
            SettingsViewModel.DescribeBlockers(new[] { route1 }),
            context.Settings.StatusMessage);
    }

    [Fact]
    public async Task A_route_reports_is_validating_while_it_checks_the_path()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();

        ValidatedFieldViewModel route2 = context.Settings.Routes.Route2;
        bool reportedValidating = false;

        route2.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ValidatedFieldViewModel.IsValidating) && route2.IsValidating)
            {
                reportedValidating = true;
            }
        };

        route2.BeginEditCommand.Execute(null);
        route2.Value = context.Route2Folder;

        Assert.True(route2.IsValidating);
        Assert.True(reportedValidating);

        await route2.PendingValidation;

        Assert.False(route2.IsValidating);
        Assert.True(route2.IsValid);
    }

    [Fact]
    public async Task An_existing_and_accessible_route_ends_valid_and_the_progress_indicator_stops()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();

        ValidatedFieldViewModel route4 = context.Settings.Routes.Route4;

        route4.BeginEditCommand.Execute(null);
        route4.Value = context.Route4Folder;
        await route4.PendingValidation;

        Assert.True(route4.IsValid);
        Assert.False(route4.IsValidating);
        Assert.True(route4.ShowsSuccess);
        Assert.DoesNotContain(FileRouteValidation.Route4Parameter, route4.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_folder_route_that_does_not_exist_ends_invalid_with_its_reason()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        ValidatedFieldViewModel route4 = context.Settings.Routes.Route4;

        route4.BeginEditCommand.Execute(null);
        route4.Value = Path.Combine(context.Database.Paths.Root, "pdfs-inexistente");
        await route4.PendingValidation;

        Assert.False(route4.IsValid);
        Assert.Contains(FileRouteValidation.Route4Parameter, route4.Message, StringComparison.Ordinal);
        Assert.Contains("no existe una carpeta en esa ruta", route4.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_folder_where_a_file_is_expected_says_so_instead_of_claiming_the_file_is_missing()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        ValidatedFieldViewModel route1 = context.Settings.Routes.Route1;
        route1.BeginEditCommand.Execute(null);
        route1.Value = context.Database.Paths.Root;
        await route1.PendingValidation;

        Assert.False(route1.IsValid);
        Assert.Contains("esa ruta es una carpeta", route1.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_where_a_folder_is_expected_says_so_instead_of_claiming_the_folder_is_missing()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();
        string existingFile = Path.Combine(context.Database.Paths.Root, "consolidado.xlsx");
        await File.WriteAllTextAsync(existingFile, "x");

        ValidatedFieldViewModel route2 = context.Settings.Routes.Route2;
        route2.BeginEditCommand.Execute(null);
        route2.Value = existingFile;
        await route2.PendingValidation;

        Assert.False(route2.IsValid);
        Assert.Contains("esa ruta es un archivo", route2.Message, StringComparison.Ordinal);
    }
}
