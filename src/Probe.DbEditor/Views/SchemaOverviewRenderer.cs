using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Views;

public sealed class SchemaOverviewRenderer
{
    private const double CardWidth = 190;
    private const double CardHeight = 64;
    private const double HorizontalGap = 80;
    private const double VerticalGap = 56;
    private const int Columns = 4;

    public void Render(Canvas canvas, IReadOnlyList<string> tables, IReadOnlyList<ForeignKeyEdge> foreignKeys)
    {
        canvas.Children.Clear();
        var positions = CalculatePositions(tables);

        foreach (var edge in foreignKeys)
        {
            AddRelationship(canvas, positions, edge);
        }

        foreach (var table in tables)
        {
            AddTable(canvas, table, positions[table]);
        }

        var requiredRows = Math.Max(1, (int)Math.Ceiling(tables.Count / (double)Columns));
        canvas.Width = Math.Max(1200, 80 + Columns * (CardWidth + HorizontalGap));
        canvas.Height = Math.Max(700, 80 + requiredRows * (CardHeight + VerticalGap));
    }

    private static Dictionary<string, Point> CalculatePositions(IReadOnlyList<string> tables)
    {
        var positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tables.Count; i++)
        {
            var column = i % Columns;
            var row = i / Columns;
            positions[tables[i]] = new Point(
                40 + column * (CardWidth + HorizontalGap),
                40 + row * (CardHeight + VerticalGap));
        }

        return positions;
    }

    private static void AddRelationship(
        Canvas canvas,
        IReadOnlyDictionary<string, Point> positions,
        ForeignKeyEdge edge)
    {
        if (!positions.TryGetValue(edge.TableName, out var from) ||
            !positions.TryGetValue(edge.ReferencedTableName, out var to))
        {
            return;
        }

        canvas.Children.Add(new Line
        {
            X1 = from.X + CardWidth,
            Y1 = from.Y + CardHeight / 2,
            X2 = to.X,
            Y2 = to.Y + CardHeight / 2,
            Stroke = ResourceBrush(canvas, "AccentBrush", Brushes.SteelBlue),
            StrokeThickness = 1.5,
            ToolTip = $"{edge.TableName}.{edge.ColumnName} -> {edge.ReferencedTableName}.{edge.ReferencedColumnName}"
        });
    }

    private static void AddTable(Canvas canvas, string table, Point point)
    {
        var border = new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            Background = ResourceBrush(canvas, "SurfaceBrush", Brushes.White),
            BorderBrush = ResourceBrush(canvas, "AccentBrush", Brushes.Gray),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = new TextBlock
            {
                Text = table,
                Foreground = ResourceBrush(canvas, "TextBrush", Brushes.Black),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        Canvas.SetLeft(border, point.X);
        Canvas.SetTop(border, point.Y);
        canvas.Children.Add(border);
    }

    private static Brush ResourceBrush(FrameworkElement element, string resourceKey, Brush fallback)
    {
        return element.TryFindResource(resourceKey) as Brush ?? fallback;
    }
}
