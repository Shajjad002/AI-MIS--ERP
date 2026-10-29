using AI.MIS.Infrastructure.Security;

namespace AI.MIS.UnitTests;

public sealed class PasswordHashTests
{
    [Fact]
    public void Create_ProducesHashThatVerifiesForOriginalPassword()
    {
        var hash = PasswordHash.Create("correct horse battery staple");

        Assert.True(PasswordHash.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_ReturnsFalseForDifferentPassword()
    {
        var hash = PasswordHash.Create("correct horse battery staple");

        Assert.False(PasswordHash.Verify("wrong password", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("%%% .%%%")]
    public void Verify_ReturnsFalseForMalformedHash(string hash)
    {
        Assert.False(PasswordHash.Verify("any password", hash));
    }

    [Fact]
    public void Create_UsesDifferentSaltForEachHash()
    {
        Assert.NotEqual(PasswordHash.Create("same password"), PasswordHash.Create("same password"));
    }
}
