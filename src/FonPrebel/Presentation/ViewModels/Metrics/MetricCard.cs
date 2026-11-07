using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.ViewModels.Metrics;

/// <summary>
/// Una tarjeta del tablero de Métricas (ficha V5, §2.22): un número grande, su etiqueta y, cuando la
/// tarjeta lo necesita, líneas de detalle con su propio contador.
/// </summary>
/// <remarks>
/// Es inmutable a propósito: cada refresco arma tarjetas nuevas a partir de los contadores leídos de
/// SQLite, así la pantalla nunca muestra un valor que no venga de la base. <see cref="Note"/> es la
/// explicación de una tarjeta pendiente, que va debajo de los contadores y con ajuste de línea.
/// </remarks>
public sealed record MetricCard(
    string Title,
    SymbolRegular Icon,
    string Value,
    string Caption,
    IReadOnlyList<MetricDetail> Details,
    bool IsPending = false,
    string? Note = null);

/// <summary>Una línea de detalle de una tarjeta: etiqueta corta y su contador ya formateado.</summary>
public sealed record MetricDetail(string Label, string Value);
