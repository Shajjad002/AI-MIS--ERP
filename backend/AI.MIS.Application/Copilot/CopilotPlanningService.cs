using System.Text.Json;
using AI.MIS.Application.Copilot.Models;

namespace AI.MIS.Application.Copilot;

public sealed class CopilotPlanningService(ILlmClient llmClient, ISchemaMetadataProvider schemaMetadataProvider) : ICopilotPlanningService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CopilotInterpretation> InterpretAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("A question is required.", nameof(question));

        var schema = await schemaMetadataProvider.GetApprovedSchemaAsync(cancellationToken);
        var schemaJson = JsonSerializer.Serialize(schema, JsonOptions);
        var prompt = $$"""
            You are an ERP MIS assistant. Return only JSON with these fields: intent (string), metrics (string array), dimensions (string array), periodStart (ISO date or null), periodEnd (ISO date or null), filters (object of string values), chartType (bar|line|pie|donut|table|null), needsClarification (boolean), clarificationQuestion (string or null), proposedSql (string or null).
            Rules: interpret only from the approved schema below; use SELECT queries only; never use data-modifying SQL; never invent tables or columns; request clarification when needed.
            Approved schema: {{schemaJson}}
            """;
        var json = await llmClient.CompleteJsonAsync(prompt, question, cancellationToken);
        var interpretation = JsonSerializer.Deserialize<CopilotInterpretation>(json, JsonOptions)
            ?? throw new InvalidOperationException("The AI returned an empty structured response.");

        if (string.IsNullOrWhiteSpace(interpretation.Intent) || interpretation.Metrics is null || interpretation.Dimensions is null || interpretation.Filters is null)
            throw new InvalidOperationException("The AI response did not satisfy the required interpretation schema.");

        var sql = interpretation.ProposedSql?.Trim();
        if (!string.IsNullOrEmpty(sql) && !sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The AI proposed a non-read-only query, which was rejected.");

        return interpretation with { ProposedSql = sql };
    }
}
