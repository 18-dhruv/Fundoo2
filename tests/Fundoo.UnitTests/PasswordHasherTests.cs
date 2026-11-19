using Fundoo.User.Infrastructure.Security;
using NUnit.Framework;

namespace Fundoo.UnitTests;

[TestFixture]
public sealed class PasswordHasherTests
{
    [Test]
    public void Hash_ProducesHashThatVerifiesOriginalPassword()
    {
        var hasher = new BCryptPasswordHasher();
        const string password = "Fundoo-test-password-42!";

        var hash = hasher.Hash(password);

        Assert.That(hash, Is.Not.EqualTo(password));
        Assert.That(hasher.Verify(password, hash), Is.True);
    }

    [Test]
    public void Verify_ReturnsFalseForDifferentPassword()
    {
        var hasher = new BCryptPasswordHasher();
        var hash = hasher.Hash("correct-password");

        Assert.That(hasher.Verify("wrong-password", hash), Is.False);
    }
}
