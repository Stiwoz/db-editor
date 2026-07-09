using System.ComponentModel;

namespace Probe.DbEditor.Views.SqlEditor;

public sealed record SqlOrderByColumn(string ColumnName, ListSortDirection Direction);
