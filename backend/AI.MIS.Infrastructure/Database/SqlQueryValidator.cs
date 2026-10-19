using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Models;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace AI.MIS.Infrastructure.Database;

public sealed class SqlQueryValidator : ISqlQueryValidator
{
    public void Validate(string sql, IReadOnlyList<SchemaTable> approvedSchema)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("A SQL query is required.", nameof(sql));

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        IList<ParseError> errors;
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out errors);
        if (errors.Count > 0)
            throw new InvalidOperationException($"The SQL query could not be parsed: {errors[0].Message}");

        if (fragment is not TSqlScript script || script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1 ||
            script.Batches[0].Statements[0] is not SelectStatement)
            throw new InvalidOperationException("Only one read-only SELECT statement is allowed.");

        var schema = approvedSchema.ToDictionary(table => table.Name, StringComparer.OrdinalIgnoreCase);
        var visitor = new ReferenceVisitor();
        fragment.Accept(visitor);

        foreach (var table in visitor.Tables)
        {
            if (!schema.ContainsKey(table))
                throw new InvalidOperationException($"The query references a table that is not approved: {table}.");
        }

        foreach (var column in visitor.Columns)
        {
            if (column.Name == "*")
                continue;

            var candidates = column.TableAlias is not null
                ? visitor.TableAliases.Where(pair => string.Equals(pair.Key, column.TableAlias, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value)
                : visitor.Tables;
            if (!candidates.Any())
                throw new InvalidOperationException($"The query references an unknown table alias: {column.TableAlias}.");

            if (!candidates.Any(table => schema[table].Columns.Any(approved => string.Equals(approved.Name, column.Name, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException($"The query references a column that is not approved: {column.Name}.");
        }
    }

    private sealed class ReferenceVisitor : TSqlFragmentVisitor
    {
        public HashSet<string> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> TableAliases { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(string Name, string? TableAlias)> Columns { get; } = [];

        public override void Visit(NamedTableReference node)
        {
            var table = node.SchemaObject.BaseIdentifier.Value;
            Tables.Add(table);
            if (!string.IsNullOrWhiteSpace(node.Alias?.Value))
                TableAliases[node.Alias.Value] = table;
            base.Visit(node);
        }

        public override void Visit(ColumnReferenceExpression node)
        {
            var identifiers = node.MultiPartIdentifier.Identifiers;
            var name = identifiers[^1].Value;
            var tableAlias = identifiers.Count > 1 ? identifiers[^2].Value : null;
            Columns.Add((name, tableAlias));
            base.Visit(node);
        }
    }
}
