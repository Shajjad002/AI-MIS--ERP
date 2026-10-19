using System.Security.Claims;
using AI.MIS.Application.Copilot;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace AI.MIS.Infrastructure.Database;

public sealed class QueryAuthorizationService : IQueryAuthorizationService
{
    public void Validate(string sql, ClaimsPrincipal user)
    {
        if (user.IsInRole("Administrator") || user.IsInRole("MIS Analyst"))
            return;

        if (!user.IsInRole("Branch User"))
            throw new UnauthorizedAccessException("The authenticated user has no query role.");

        var allowed = user.FindAll("branch_code").Select(claim => claim.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowed.Count == 0)
            throw new UnauthorizedAccessException("The branch user has no authorized branches.");

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var errors);
        if (errors.Count > 0) throw new UnauthorizedAccessException("The query could not be checked for branch authorization.");
        var visitor = new BranchScopeVisitor(allowed);
        fragment.Accept(visitor);

        if (!visitor.HasSafeBranchScope)
            throw new UnauthorizedAccessException("Branch users must query only their authorized branch data.");
    }

    private sealed class BranchScopeVisitor(HashSet<string> allowedBranches) : TSqlFragmentVisitor
    {
        private int querySpecificationCount;
        private bool hasSafeBranchScope;
        private string? sourceTable;
        private string? sourceAlias;
        private bool validSource;

        public bool HasSafeBranchScope => querySpecificationCount == 1 && validSource && hasSafeBranchScope;

        public override void Visit(QuerySpecification node)
        {
            querySpecificationCount++;
            if (querySpecificationCount == 1 &&
                node.FromClause?.TableReferences.Count == 1 &&
                node.FromClause.TableReferences[0] is NamedTableReference table)
            {
                validSource = true;
                sourceTable = table.SchemaObject.BaseIdentifier.Value;
                sourceAlias = table.Alias?.Value;
                hasSafeBranchScope = GuaranteesAllowedBranch(node.WhereClause?.SearchCondition);
            }

            base.Visit(node);
        }

        private bool GuaranteesAllowedBranch(BooleanExpression? expression)
        {
            return expression switch
            {
                BooleanParenthesisExpression parenthesis => GuaranteesAllowedBranch(parenthesis.Expression),
                BooleanBinaryExpression binary when binary.BinaryExpressionType == BooleanBinaryExpressionType.And =>
                    GuaranteesAllowedBranch(binary.FirstExpression) || GuaranteesAllowedBranch(binary.SecondExpression),
                BooleanBinaryExpression binary when binary.BinaryExpressionType == BooleanBinaryExpressionType.Or =>
                    GuaranteesAllowedBranch(binary.FirstExpression) && GuaranteesAllowedBranch(binary.SecondExpression),
                BooleanComparisonExpression comparison => IsAllowedBranchEquality(comparison),
                _ => false
            };
        }

        private bool IsAllowedBranchEquality(BooleanComparisonExpression comparison)
        {
            if (comparison.ComparisonType != BooleanComparisonType.Equals)
                return false;

            return IsAllowedBranchEquality(comparison.FirstExpression, comparison.SecondExpression) ||
                IsAllowedBranchEquality(comparison.SecondExpression, comparison.FirstExpression);
        }

        private bool IsAllowedBranchEquality(ScalarExpression columnExpression, ScalarExpression literalExpression)
        {
            if (columnExpression is not ColumnReferenceExpression column ||
                literalExpression is not StringLiteral literal ||
                !string.Equals(column.MultiPartIdentifier.Identifiers[^1].Value, "BranchCode", StringComparison.OrdinalIgnoreCase) ||
                !allowedBranches.Contains(literal.Value))
                return false;

            var identifiers = column.MultiPartIdentifier.Identifiers;
            if (identifiers.Count < 2)
                return true;

            var qualifier = identifiers[^2].Value;
            return string.Equals(qualifier, sourceAlias ?? sourceTable, StringComparison.OrdinalIgnoreCase) ||
                (sourceAlias is not null && string.Equals(qualifier, sourceTable, StringComparison.OrdinalIgnoreCase));
        }
    }
}
