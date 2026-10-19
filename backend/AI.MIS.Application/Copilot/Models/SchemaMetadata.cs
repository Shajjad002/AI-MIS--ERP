namespace AI.MIS.Application.Copilot.Models;

public sealed record SchemaColumn(string Name, string Description, string DataType);

public sealed record SchemaTable(string Name, string Description, IReadOnlyList<SchemaColumn> Columns);
