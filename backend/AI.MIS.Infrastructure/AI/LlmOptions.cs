namespace AI.MIS.Infrastructure.AI;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}
