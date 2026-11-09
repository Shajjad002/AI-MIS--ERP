using System.Reflection;
using System.Security.Claims;
using AI.MIS.Api.Controllers;
using AI.MIS.Application.Rag;
using AI.MIS.Application.Users;
using AI.MIS.Infrastructure.Database;
using AI.MIS.UnitTests.Extended.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AI.MIS.UnitTests.Extended;

public sealed class AuthControllerTests
{
    private static AuthController Create(Func<string, string, AuthenticationResult?> authenticate) => new(new FakeAuthenticationService(authenticate));

    private static readonly AuthenticationResult Token = new("jwt", DateTimeOffset.UtcNow.AddHours(1), "alice", ["Branch User"]);

    [Fact]
    public async Task Login_ReturnsTheTokenResult_ForValidCredentials()
    {
        var result = await Create((u, p) => u == "alice" && p == "pw" ? Token : null).Login(new AuthController.LoginRequest("alice", "pw"), default);

        Assert.Same(Token, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Login_Returns401WithAGenericMessage_ForBadCredentials()
    {
        var result = await Create((_, _) => null).Login(new AuthController.LoginRequest("alice", "wrong"), default);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        // The message must not reveal whether the user name or the password was wrong.
        Assert.Equal("Invalid username or password.", Reflect.Prop<string>(unauthorized.Value, "message"));
    }

    [Fact]
    public async Task Login_MapsConfigurationProblemsTo503_AndInvalidInputTo400()
    {
        var notConfigured = await Create((_, _) => throw new InvalidOperationException("Authentication is not configured.")).Login(new("a", "b"), default);
        var invalid = await Create((_, _) => throw new ArgumentException("User name is required.")).Login(new("", ""), default);

        Assert.Equal(503, Assert.IsType<ObjectResult>(notConfigured).StatusCode);
        Assert.IsType<BadRequestObjectResult>(invalid);
    }
}

public sealed class DocumentsControllerTests
{
    private sealed class Rig
    {
        public FakeDocumentStore Store { get; } = new();
        public FakeRagService Rag { get; } = new();
        public ClaimsPrincipal User { get; set; } = TestUsers.Admin();

        public DocumentsController Build() => new(Store, Rag)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = User } }
        };
    }

    private static IFormFile File(string name = "policy.md", string content = "# Policy\nText") =>
        new FormFile(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), 0, content.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/markdown"
        };

    [Fact]
    public async Task Upload_SavesTheFile_IndexesIt_AndReturnsCreated()
    {
        var rig = new Rig();
        rig.Rag.Index = _ => 7;

        var result = await rig.Build().Upload(File(), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(7, Reflect.Prop<int>(created.Value, "indexedChunks"));
        var saved = Assert.Single(rig.Store.Documents);
        Assert.Equal("policy.md", saved.FileName);
        Assert.Equal(Guid.Parse(rig.User.FindFirst(ClaimTypes.NameIdentifier)!.Value), saved.UploadedBy);   // records WHO uploaded
    }

    [Fact]
    public async Task Upload_Returns400_WhenNoFileIsSent()
    {
        Assert.IsType<BadRequestObjectResult>(await new Rig().Build().Upload(null!, default));
    }

    [Fact]
    public async Task Upload_ReturnsUnauthorized_WhenTheTokenHasNoUserId_AndStoresNothing()
    {
        var rig = new Rig { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Administrator")], "unit-test")) };

        Assert.IsType<UnauthorizedResult>(await rig.Build().Upload(File(), default));
        Assert.Empty(rig.Store.Documents);
    }

    [Fact]
    public async Task Upload_Returns400_WhenTheStoreRejectsTheFile()
    {
        var rig = new Rig();
        rig.Store.SaveThrows = new InvalidOperationException("Only .txt, .md, .pdf, and .docx documents are supported.");

        var result = await rig.Build().Upload(File("virus.exe"), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("supported", Reflect.Prop<string>(bad.Value, "error"));
    }

    [Fact]
    public async Task Upload_Returns503_WhenIndexingFails_ButTellsTheCallerWhichDocumentWasSaved()
    {
        var rig = new Rig();
        rig.Rag.Index = _ => throw new InvalidOperationException("RAG embeddings are not configured.");

        var result = await rig.Build().Upload(File(), default);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(Assert.Single(rig.Store.Documents).Id, Reflect.Prop<Guid>(response.Value, "documentId"));   // retry via /index
    }

    [Fact]
    public async Task Reindex_Returns404_ForAnUnknownDocument()
    {
        Assert.IsType<NotFoundResult>(await new Rig().Build().Reindex(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Reindex_ReturnsTheChunkCount_ForAKnownDocument()
    {
        var rig = new Rig();
        rig.Rag.Index = _ => 12;
        var doc = (await rig.Store.SaveAsync(new MemoryStream(new byte[] { 1 }), "a.md", "text/markdown", 1, Guid.NewGuid()));

        var result = await rig.Build().Reindex(doc.Id, default);

        Assert.Equal(12, Reflect.Prop<int>(Assert.IsType<OkObjectResult>(result).Value, "indexedChunks"));
    }

    [Fact]
    public async Task Reindex_MapsMissingFileTo404_AndProviderProblemsTo503()
    {
        var rig = new Rig();
        var doc = await rig.Store.SaveAsync(new MemoryStream(new byte[] { 1 }), "a.md", "text/markdown", 1, Guid.NewGuid());

        rig.Rag.Index = _ => throw new FileNotFoundException("The document file is missing from storage.");
        Assert.IsType<NotFoundObjectResult>(await rig.Build().Reindex(doc.Id, default));

        rig.Rag.Index = _ => throw new InvalidOperationException("embeddings down");
        Assert.Equal(503, Assert.IsType<ObjectResult>(await rig.Build().Reindex(doc.Id, default)).StatusCode);
    }

    [Fact]
    public async Task Search_MapsBlankQueryTo400_AndProviderProblemsTo503()
    {
        var rig = new Rig();
        rig.Rag.Search = _ => throw new ArgumentException("A search query is required.");
        Assert.IsType<BadRequestObjectResult>(await rig.Build().Search(new RagSearchRequest(""), default));

        rig.Rag.Search = _ => throw new InvalidOperationException("embeddings down");
        Assert.Equal(503, Assert.IsType<ObjectResult>(await rig.Build().Search(new RagSearchRequest("q"), default)).StatusCode);
    }

    [Fact]
    public async Task Ask_ReturnsTheRagAnswer()
    {
        var rig = new Rig();
        rig.Rag.Answer = q => new RagAnswer($"answer to {q}", []);

        var result = await rig.Build().Ask(new DocumentsController.AskRequest("Define recoverable"), default);

        Assert.Equal("answer to Define recoverable", Assert.IsType<RagAnswer>(Assert.IsType<OkObjectResult>(result).Value).Answer);
    }
}

public sealed class SchemaMetadataControllerTests
{
    [Fact]
    public async Task Get_ReturnsTheApprovedSchema()
    {
        var schema = Schema.Approved();

        var result = await new SchemaMetadataController(new FakeSchemaProvider(schema)).Get(default);

        Assert.Same(schema, Assert.IsType<OkObjectResult>(result).Value);
    }
}

/// <summary>Regression guard: authorization lives in attributes, so a deleted [Authorize] must fail a test.</summary>
public sealed class ControllerAuthorizationTests
{
    [Theory]
    [InlineData(typeof(DocumentsController))]
    [InlineData(typeof(UserManagementController))]
    [InlineData(typeof(MenuManagementController))]
    [InlineData(typeof(MISReportController))]
    [InlineData(typeof(ExcelImportsController))]
    public void SensitiveControllers_RequireAnAuthenticatedUser(Type controller)
    {
        Assert.NotEmpty(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    [Fact]
    public void UserManagement_IsRestrictedToAdministrators()
    {
        Assert.Equal("Administrator", typeof(UserManagementController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void Documents_AreRestrictedToAdministratorsAndMisAnalysts()
    {
        var roles = typeof(DocumentsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');

        Assert.Equal(new[] { "Administrator", "MIS Analyst" }, roles.OrderBy(r => r));
    }

    [Fact]
    public void DocumentRoleNames_MatchTheRolesTheQueryAuthorizerTreatsAsPrivileged()
    {
        // Guards against the two hard-coded role strings drifting apart.
        var roles = typeof(DocumentsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
        var authorization = new QueryAuthorizationService();

        Assert.All(roles, role => Assert.Null(Record.Exception(() => authorization.Validate("SELECT 1", TestUsers.With(role)))));
    }

    [Fact]
    public void MenuManagement_OnlyListVisibleIsOpenToEveryAuthenticatedUser()
    {
        var actions = typeof(MenuManagementController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotEmpty(actions);
        foreach (var action in actions.Where(a => a.Name != nameof(MenuManagementController.ListVisible)))
            Assert.True(action.GetCustomAttribute<AuthorizeAttribute>()?.Roles == "Administrator", $"{action.Name} must be Administrator-only.");
    }

    [Fact]
    public void QueryEndpoint_IsAuthenticatedAndRateLimited()
    {
        var query = typeof(CopilotController).GetMethod(nameof(CopilotController.Query))!;

        Assert.NotNull(query.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("copilot", query.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public void InterpretEndpoint_ShouldRequireAuthentication()
    {
        // SUSPECTED DEFECT: /api/copilot/interpret has no [Authorize]. Anyone on the network can call the paid LLM
        // and the approved schema is sent in the prompt. (The /query endpoint is protected.)
        var interpret = typeof(CopilotController).GetMethod(nameof(CopilotController.Interpret))!;

        Assert.NotNull(interpret.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public void SchemaMetadataEndpoint_ShouldRequireAuthentication()
    {
        // SUSPECTED DEFECT: /api/schema-metadata is anonymous and discloses table/column names to anyone.
        Assert.NotEmpty(typeof(SchemaMetadataController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }
}
