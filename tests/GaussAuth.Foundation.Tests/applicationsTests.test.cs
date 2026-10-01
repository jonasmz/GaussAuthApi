using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    [TestMethod]
    public async Task Application_retrieval_listing_and_lifecycle_are_safe()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var code = $"app-{Guid.NewGuid():N}";
        using var created = await client.PostAsJsonAsync("/applications", new { code, name = "Application" });
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        using var byId = await client.GetAsync($"/applications/{id}");
        using var byCode = await client.GetAsync($"/applications/by-code/{code}");
        using var list = await client.GetAsync("/applications?limit=1");
        using var invalidLimit = await client.GetAsync("/applications?limit=0");
        using var deactivate = await client.PostAsync($"/applications/{id}/deactivate", null);
        using var repeatedDeactivate = await client.PostAsync($"/applications/{id}/deactivate", null);
        using var activate = await client.PostAsync($"/applications/{id}/activate", null);
        using var missing = await client.GetAsync($"/applications/{Guid.NewGuid()}");
        Assert.AreEqual(HttpStatusCode.OK, byId.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, byCode.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, repeatedDeactivate.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, activate.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }
    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
