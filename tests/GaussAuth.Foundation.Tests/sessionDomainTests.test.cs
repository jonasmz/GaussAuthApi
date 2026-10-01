using GaussAuth.Domain.Sessions;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class SessionDomainTests
{
    [TestMethod]
    public void Create_sets_absolute_expiry_and_rejects_invalid_values()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, TimeSpan.FromHours(8));
        Assert.AreEqual(now.AddHours(8), session.ExpiresAt);
        Assert.Throws<ArgumentException>(() => Session.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), now, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentException>(() => Session.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), now, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentException>(() => Session.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, now, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentException>(() => Session.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, TimeSpan.Zero));
    }

    [TestMethod]
    public void State_is_active_until_expiry_and_revocation_wins()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, TimeSpan.FromHours(1));
        Assert.AreEqual(SessionState.Active, session.GetState(now.AddMinutes(59)));
        Assert.AreEqual(SessionState.Expired, session.GetState(session.ExpiresAt));
        session.Revoke(now.AddMinutes(10));
        session.Revoke(now.AddMinutes(20));
        Assert.AreEqual(now.AddMinutes(10), session.RevokedAt);
        Assert.AreEqual(SessionState.Revoked, session.GetState(session.ExpiresAt.AddDays(1)));
    }
}
