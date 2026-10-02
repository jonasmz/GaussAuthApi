using GaussAuth.Infrastructure.Administration;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class BootstrapAdministratorTests
{
    private const string Password = "Bootstrap!2026";

    [TestMethod]
    public async Task Bootstrap_command_creates_one_active_user_once_and_never_echoes_the_password()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        var migrated = await ApiProcess.RunAsync(new Dictionary<string, string?> { ["ConnectionStrings__AuthenticationDatabase"] = database.ConnectionString }, TimeSpan.FromSeconds(60), "migrate");
        Assert.AreEqual(0, migrated.ExitCode, migrated.Output);

        var environment = new Dictionary<string, string?>
        {
            ["ConnectionStrings__AuthenticationDatabase"] = database.ConnectionString,
            ["Bootstrap__AdministratorEmail"] = "first.admin@example.test",
            ["Bootstrap__AdministratorPassword"] = Password,
            ["Administration__GlobalAdministratorUserIds__0"] = null
        };
        var first = await ApiProcess.RunAsync(environment, TimeSpan.FromSeconds(60), "bootstrap-admin");
        Assert.AreEqual(0, first.ExitCode, first.Output);
        Assert.Contains("Administration__GlobalAdministratorUserIds__0=", first.Output);
        Assert.DoesNotContain(Password, first.Output);

        await using (var db = new AuthenticationDbContext(new DbContextOptionsBuilder<AuthenticationDbContext>().UseNpgsql(database.ConnectionString).Options))
        {
            var user = await db.DomainUsers.SingleAsync();
            Assert.IsTrue(user.IsActive);
            Assert.IsFalse((await db.SecurityEvents.SingleAsync(item => item.EventType == "user.created")).Metadata?.Contains(Password, StringComparison.Ordinal) ?? false);
        }

        var second = await ApiProcess.RunAsync(environment, TimeSpan.FromSeconds(60), "bootstrap-admin");
        Assert.AreNotEqual(0, second.ExitCode, second.Output);
        Assert.Contains("users-exist", second.Output);
        Assert.DoesNotContain(Password, second.Output);
    }

    [DataRow("weak", "first.admin@example.test", "password-policy")]
    [DataRow("Bootstrap!2026", "not-an-email", "invalid-email")]
    [TestMethod]
    public async Task Bootstrap_rejects_invalid_input_without_leaking_values(string password, string email, string category)
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        await ApiProcess.RunAsync(new Dictionary<string, string?> { ["ConnectionStrings__AuthenticationDatabase"] = database.ConnectionString }, TimeSpan.FromSeconds(60), "migrate");
        var result = await ApiProcess.RunAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings__AuthenticationDatabase"] = database.ConnectionString,
            ["Bootstrap__AdministratorEmail"] = email,
            ["Bootstrap__AdministratorPassword"] = password
        }, TimeSpan.FromSeconds(60), "bootstrap-admin");
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        Assert.Contains(category, result.Output);
        Assert.DoesNotContain(password, result.Output);
        Assert.DoesNotContain(email, result.Output);
    }

    [TestMethod]
    public void Global_authority_requires_explicit_configuration_after_bootstrap()
    {
        var userId = Guid.NewGuid();
        var empty = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Administration:GlobalAdministratorUserIds:0"] = userId.ToString()
        }).Build();
        Assert.IsFalse(new ConfiguredGlobalAdministratorPolicy(empty).IsGlobalAdministrator(userId));
        Assert.IsTrue(new ConfiguredGlobalAdministratorPolicy(configured).IsGlobalAdministrator(userId));
    }
}
