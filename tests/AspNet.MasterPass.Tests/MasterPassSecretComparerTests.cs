using AspNet.MasterPass.Internal;
using Microsoft.AspNetCore.Identity;

namespace AspNet.MasterPass.Tests;

public sealed class MasterPassSecretComparerTests
{
    [Fact]
    public void Plaintext_secret_matches()
    {
        Assert.True(MasterPassSecretComparer.Matches("my-secret", "my-secret"));
    }

    [Fact]
    public void Plaintext_secret_is_case_sensitive()
    {
        Assert.False(MasterPassSecretComparer.Matches("My-Secret", "my-secret"));
    }

    [Fact]
    public void Identity_hash_matches_its_password()
    {
        var hash = new PasswordHasher<object>().HashPassword(new object(), "my-secret");

        Assert.True(MasterPassSecretComparer.LooksLikeIdentityHash(hash));
        Assert.True(MasterPassSecretComparer.Matches("my-secret", hash));
        Assert.False(MasterPassSecretComparer.Matches("other-secret", hash));
    }

    [Fact]
    public void Plaintext_is_not_treated_as_an_identity_hash()
    {
        Assert.False(MasterPassSecretComparer.LooksLikeIdentityHash("my-secret"));
        Assert.False(MasterPassSecretComparer.LooksLikeIdentityHash(
            "just-a-long-plaintext-secret-that-is-definitely-not-base64-encoded"));
    }
}