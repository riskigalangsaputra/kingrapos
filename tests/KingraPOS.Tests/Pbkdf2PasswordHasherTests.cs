using KingraPOS.Infrastructure.Security;

namespace KingraPOS.Tests;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_accepts_the_original_password()
    {
        var hash = _hasher.Hash("rahasia123");

        Assert.True(_hasher.Verify("rahasia123", hash));
    }

    [Fact]
    public void Verify_rejects_a_different_password()
    {
        var hash = _hasher.Hash("rahasia123");

        Assert.False(_hasher.Verify("rahasia124", hash));
    }

    [Fact]
    public void Hash_uses_a_new_salt_every_time()
    {
        var first = _hasher.Hash("rahasia123");
        var second = _hasher.Hash("rahasia123");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("rahasia123", first));
        Assert.True(_hasher.Verify("rahasia123", second));
    }

    [Fact]
    public void Hash_uses_the_documented_format()
    {
        var parts = _hasher.Hash("rahasia123").Split('$');

        Assert.Equal(5, parts.Length);
        Assert.Equal("pbkdf2", parts[0]);
        Assert.Equal("sha256", parts[1]);
        Assert.Equal(210_000, int.Parse(parts[2]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bukan-hash")]
    [InlineData("pbkdf2$sha256$abc$zzz$zzz")]
    [InlineData("pbkdf2$md5$1000$zzz$zzz")]
    public void Verify_rejects_malformed_hashes(string hash)
    {
        Assert.False(_hasher.Verify("rahasia123", hash));
    }
}
