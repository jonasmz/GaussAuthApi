using GaussAuth.Domain.Applications;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ConsumerCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Create_requires_an_application_and_a_bounded_non_empty_hash()
    {
        var credential = ConsumerCredential.Create(Guid.NewGuid(), "hash-1", Now);
        Assert.AreEqual("hash-1", credential.CurrentHash);
        Assert.IsNull(credential.RetiringHash);
        Assert.IsNull(credential.RotatedAtUtc);
        Assert.IsNull(credential.RetiredAtUtc);

        Assert.Throws<ArgumentException>(() => ConsumerCredential.Create(Guid.Empty, "hash", Now));
        Assert.Throws<ArgumentException>(() => ConsumerCredential.Create(Guid.NewGuid(), " ", Now));
        Assert.Throws<ArgumentException>(() => ConsumerCredential.Create(Guid.NewGuid(), new string('a', 513), Now));
        Assert.IsNotNull(ConsumerCredential.Create(Guid.NewGuid(), new string('a', 512), Now));
    }

    [TestMethod]
    public void Rotate_moves_the_current_hash_to_retiring_and_discards_older_retiring()
    {
        var credential = ConsumerCredential.Create(Guid.NewGuid(), "one", Now);
        credential.Rotate("two", Now.AddMinutes(1));
        Assert.AreEqual("two", credential.CurrentHash);
        Assert.AreEqual("one", credential.RetiringHash);
        Assert.AreEqual(Now.AddMinutes(1), credential.RotatedAtUtc);

        credential.Rotate("three", Now.AddMinutes(2));
        Assert.AreEqual("three", credential.CurrentHash);
        Assert.AreEqual("two", credential.RetiringHash);
        Assert.Throws<ArgumentException>(() => credential.Rotate("three", Now));
        Assert.Throws<ArgumentException>(() => credential.Rotate("", Now));
    }

    [TestMethod]
    public void RetirePrevious_clears_only_the_retiring_hash_and_is_a_no_op_without_one()
    {
        var credential = ConsumerCredential.Create(Guid.NewGuid(), "one", Now);
        credential.RetirePrevious(Now);
        Assert.IsNull(credential.RetiredAtUtc);

        credential.Rotate("two", Now);
        credential.RetirePrevious(Now.AddMinutes(5));
        Assert.AreEqual("two", credential.CurrentHash);
        Assert.IsNull(credential.RetiringHash);
        Assert.AreEqual(Now.AddMinutes(5), credential.RetiredAtUtc);
    }

    [TestMethod]
    public void Importing_a_configured_hash_keeps_it_as_retiring_and_enforces_distinct_hashes()
    {
        var id = Guid.NewGuid();
        var credential = ConsumerCredential.CreateFromConfigured(id, "configured", "new", Now);
        Assert.AreEqual("new", credential.CurrentHash);
        Assert.AreEqual("configured", credential.RetiringHash);
        Assert.IsNotNull(credential.RotatedAtUtc);
        Assert.Throws<ArgumentException>(() => ConsumerCredential.CreateFromConfigured(id, "same", "same", Now));
        Assert.Throws<ArgumentException>(() => ConsumerCredential.CreateFromConfigured(id, new string('a', 513), "new", Now));
    }
}
