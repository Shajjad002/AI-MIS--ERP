using AI.MIS.Infrastructure.Security;

namespace AI.MIS.UnitTests.Extended;

/// <summary>Complements tests/AI.MIS.UnitTests/PasswordHashTests.cs.</summary>
public sealed class PasswordHashExtendedTests
{
    [Fact]
    public void Create_ProducesSaltDotKey_With16ByteSaltAnd32ByteKey()
    {
        var parts = PasswordHash.Create("Secret#123").Split('.');

        Assert.Equal(2, parts.Length);
        Assert.Equal(16, Convert.FromBase64String(parts[0]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[1]).Length);
    }

    [Fact]
    public void Create_NeverContainsThePlainPassword()
    {
        Assert.DoesNotContain("Secret#123", PasswordHash.Create("Secret#123"));
    }

    [Theory]
    [InlineData("pässwörd-ঋণ-密码")]                      // non-ASCII
    [InlineData("   leading and trailing spaces   ")]
    [InlineData("a")]
    public void Verify_RoundTripsUnusualPasswords(string password)
    {
        Assert.True(PasswordHash.Verify(password, PasswordHash.Create(password)));
    }

    [Fact]
    public void Verify_LongPassword_RoundTrips()
    {
        var password = new string('x', 5_000);

        Assert.True(PasswordHash.Verify(password, PasswordHash.Create(password)));
    }

    [Fact]
    public void Verify_IsCaseSensitive_AndSensitiveToWhitespace()
    {
        var hash = PasswordHash.Create("Secret#123");

        Assert.False(PasswordHash.Verify("secret#123", hash));
        Assert.False(PasswordHash.Verify("Secret#123 ", hash));
        Assert.False(PasswordHash.Verify("", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenKeyHasTheWrongLength()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var shortKey = Convert.ToBase64String(new byte[8]);

        Assert.False(PasswordHash.Verify("anything", $"{salt}.{shortKey}"));
    }

    [Theory]
    [InlineData("onlyonepart")]
    [InlineData("a.b.c")]                                 // extra dot makes the key part invalid base64
    [InlineData(".")]
    [InlineData("!!!.???")]
    public void Verify_ReturnsFalse_ForMalformedHashes(string hash)
    {
        Assert.False(PasswordHash.Verify("password", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenSaltIsTamperedWith()
    {
        var parts = PasswordHash.Create("Secret#123").Split('.');
        var tampered = $"{Convert.ToBase64String(new byte[16])}.{parts[1]}";

        Assert.False(PasswordHash.Verify("Secret#123", tampered));
    }
}
