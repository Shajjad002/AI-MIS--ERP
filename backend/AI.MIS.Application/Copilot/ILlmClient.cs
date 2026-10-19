namespace AI.MIS.Application.Copilot;

public interface ILlmClient
{
    Task<string> CompleteJsonAsync(string systemInstruction, string userMessage, CancellationToken cancellationToken = default);
}
