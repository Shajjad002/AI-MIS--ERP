namespace AI.MIS.Application.Copilot.Models;

public sealed record CopilotInterpretation(
    string Intent,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<string> Dimensions,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    IReadOnlyDictionary<string, string> Filters,
    string? ChartType,
    bool NeedsClarification,
    string? ClarificationQuestion,
    string? ProposedSql);
