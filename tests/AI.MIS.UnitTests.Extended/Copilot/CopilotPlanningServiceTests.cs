using System.Text.Json;
using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.UnitTests.Extended.Support;

namespace AI.MIS.UnitTests.Extended;

public sealed class CopilotPlanningServiceTests
{
    private const string ValidJson = """
        {"intent":"LoanCollectionByBranch","metrics":["CollectionAmount"],"dimensions":["BranchCode"],
         "periodStart":"2026-08-01","periodEnd":"2026-08-31","filters":{"BranchCode":"B01"},
         "chartType":"bar","needsClarification":false,"clarificationQuestion":null,
         "proposedSql":"  SELECT BranchCode, SUM(CollectionAmount) AS Total FROM LoanCollection GROUP BY BranchCode  "}
        """;

    private static (CopilotPlanningService Service, FakeLlmClient Llm) Create(string llmJson, IReadOnlyList<SchemaTable>? schema = null)
    {
        var llm = new FakeLlmClient(llmJson);
        return (new CopilotPlanningService(llm, new FakeSchemaProvider(schema ?? Schema.Approved())), llm);
    }

    private static string JsonWithSql(string? sql, bool needsClarification = false, string? clarification = null) =>
        JsonSerializer.Serialize(new
        {
            intent = "Test",
            metrics = Array.Empty<string>(),
            dimensions = Array.Empty<string>(),
            filters = new Dictionary<string, string>(),
            needsClarification,
            clarificationQuestion = clarification,
            proposedSql = sql
        });

    [Fact]
    public async Task InterpretAsync_ParsesStructuredResponse()
    {
        var (service, _) = Create(ValidJson);

        var result = await service.InterpretAsync("Show August collection by branch");

        Assert.Equal("LoanCollectionByBranch", result.Intent);
        Assert.Equal(new[] { "CollectionAmount" }, result.Metrics);
        Assert.Equal(new[] { "BranchCode" }, result.Dimensions);
        Assert.Equal(new DateOnly(2026, 8, 1), result.PeriodStart);
        Assert.Equal(new DateOnly(2026, 8, 31), result.PeriodEnd);
        Assert.Equal("B01", result.Filters["BranchCode"]);
        Assert.Equal("bar", result.ChartType);
        Assert.False(result.NeedsClarification);
    }

    [Fact]
    public async Task InterpretAsync_TrimsProposedSql()
    {
        var (service, _) = Create(ValidJson);

        var result = await service.InterpretAsync("question");

        Assert.Equal("SELECT BranchCode, SUM(CollectionAmount) AS Total FROM LoanCollection GROUP BY BranchCode", result.ProposedSql);
    }

    [Fact]
    public async Task InterpretAsync_SendsQuestionAsUserMessage_AndApprovedSchemaInSystemPrompt()
    {
        var (service, llm) = Create(ValidJson);

        await service.InterpretAsync("What is the August collection?");

        var call = Assert.Single(llm.Calls);
        Assert.Equal("What is the August collection?", call.User);
        Assert.Contains("LoanRecoverable", call.System);
        Assert.Contains("RecoverableAmount", call.System);
        Assert.Contains("never use data-modifying SQL", call.System);
    }

    [Fact]
    public async Task InterpretAsync_UsesSchemaFromProvider_NotAHardcodedOne()
    {
        var custom = new[] { new SchemaTable("ZetaTable", "custom", [new SchemaColumn("ZetaColumn", "c", "int")]) };
        var (service, llm) = Create(ValidJson, custom);

        await service.InterpretAsync("question");

        Assert.Contains("ZetaTable", llm.Calls[0].System);
        Assert.DoesNotContain("LoanCollection", llm.Calls[0].System);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task InterpretAsync_RejectsBlankQuestion_WithoutCallingTheLlm(string question)
    {
        var (service, llm) = Create(ValidJson);

        await Assert.ThrowsAsync<ArgumentException>(() => service.InterpretAsync(question));

        Assert.Empty(llm.Calls);
    }

    [Fact]
    public async Task InterpretAsync_RejectsNullQuestion()
    {
        var (service, _) = Create(ValidJson);

        await Assert.ThrowsAsync<ArgumentException>(() => service.InterpretAsync(null!));
    }

    [Theory]
    [InlineData("DELETE FROM LoanCollection")]
    [InlineData("DROP TABLE Branch")]
    [InlineData("UPDATE Branch SET RegionCode = 'x'")]
    [InlineData("INSERT INTO Branch VALUES (1)")]
    [InlineData("EXEC xp_cmdshell 'dir'")]
    [InlineData("TRUNCATE TABLE LoanCollection")]
    public async Task InterpretAsync_RejectsAnythingThatDoesNotStartWithSelect(string sql)
    {
        var (service, _) = Create(JsonWithSql(sql));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InterpretAsync("question"));

        Assert.Contains("non-read-only", ex.Message);
    }

    [Theory]
    [InlineData("select 1")]
    [InlineData("SeLeCt 1")]
    [InlineData("\n  SELECT 1")]
    public async Task InterpretAsync_AcceptsSelectRegardlessOfCaseOrLeadingWhitespace(string sql)
    {
        var (service, _) = Create(JsonWithSql(sql));

        var result = await service.InterpretAsync("question");

        Assert.StartsWith("SELECT", result.ProposedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InterpretAsync_AllowsNoSql_WhenAiAsksForClarification()
    {
        var (service, _) = Create(JsonWithSql(null, needsClarification: true, clarification: "Which month?"));

        var result = await service.InterpretAsync("show collection");

        Assert.True(result.NeedsClarification);
        Assert.Equal("Which month?", result.ClarificationQuestion);
        Assert.Null(result.ProposedSql);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"intent":"","metrics":[],"dimensions":[],"filters":{}}""")]
    [InlineData("""{"intent":"x","dimensions":[],"filters":{}}""")]
    [InlineData("""{"intent":"x","metrics":[],"filters":{}}""")]
    [InlineData("""{"intent":"x","metrics":[],"dimensions":[]}""")]
    public async Task InterpretAsync_RejectsResponsesMissingRequiredFields(string json)
    {
        var (service, _) = Create(json);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InterpretAsync("question"));

        Assert.Contains("required interpretation schema", ex.Message);
    }

    [Fact]
    public async Task InterpretAsync_RejectsJsonNull()
    {
        var (service, _) = Create("null");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InterpretAsync("question"));
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("")]
    [InlineData("{\"intent\": ")]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public async Task InterpretAsync_MalformedLlmOutput_ShouldSurfaceAsInvalidOperation(string llmOutput)
    {
        // SUSPECTED DEFECT: JsonSerializer throws JsonException, which CopilotController does not catch
        // (it handles ArgumentException / InvalidOperationException only), so the user would get HTTP 500.
        var (service, _) = Create(llmOutput);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InterpretAsync("question"));
    }
}
