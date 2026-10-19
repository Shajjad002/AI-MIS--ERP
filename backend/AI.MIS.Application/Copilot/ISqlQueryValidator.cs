namespace AI.MIS.Application.Copilot;

public interface ISqlQueryValidator
{
    void Validate(string sql, IReadOnlyList<Models.SchemaTable> approvedSchema);
}
