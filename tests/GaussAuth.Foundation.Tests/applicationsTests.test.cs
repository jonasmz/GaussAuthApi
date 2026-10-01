using System.Net;
using System.Net.Http.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ApplicationsTests
{
    [TestMethod]
    public async Task Application_creation_normalizes_code_and_rejects_duplicate()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var code = $"app-{Guid.NewGuid():N}";
        using var created = await client.PostAsJsonAsync("/applications", new { code = $" {code.ToUpperInvariant()} ", name = "Application" });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = await client.PostAsJsonAsync("/applications", new { code, name = "Other" });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
