using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Applications;
using GaussAuth.Application.Authorization;
using GaussAuth.Application.Login;
using GaussAuth.Application.Memberships;
using GaussAuth.Application.Permissions.Ports;
using GaussAuth.Application.Roles;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Users.CreateUser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Helpers to run tests against the administrative boundary: a host whose global administrators are controllable, an
/// administrator client created without using the (now protected) management routes, and Application administrators
/// holding chosen <c>auth.*</c> permissions through ordinary roles.
/// </summary>
internal static class AdministrativeTestHost
{
    public const string Password = "Quickstart!2026";

    /// <summary>
    /// Replaces the global administrator policy with <paramref name="policy"/> and lifts the administrative rate limit so
    /// setup-heavy tests never receive <c>429</c>. Must be called before the host is first used.
    /// </summary>
    public static WebApplicationFactory<Program> WithGlobalAdministrators(this WebApplicationFactory<Program> factory, TestGlobalAdministratorPolicy policy)
    {
        Environment.SetEnvironmentVariable("RateLimiting__Administration__PermitLimit", "1000000");
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGlobalAdministratorPolicy>();
            services.AddSingleton<IGlobalAdministratorPolicy>(policy);
        }));
    }

    /// <summary>Process-wide policy used by the setup helpers of tests that only need one global administrator client.</summary>
    public static TestGlobalAdministratorPolicy SharedPolicy { get; } = new();

    /// <summary><see cref="WithGlobalAdministrators(WebApplicationFactory{Program}, TestGlobalAdministratorPolicy)"/> with the shared policy.</summary>
    public static WebApplicationFactory<Program> WithGlobalAdministrators(this WebApplicationFactory<Program> factory) =>
        factory.WithGlobalAdministrators(SharedPolicy);

    /// <summary>Creates a global administrator (using the shared policy) on a migrated host and returns a client that sends its credential on <c>/admin</c> routes.</summary>
    public static async Task<AdministratorClient> CreateAdminClientAsync(this WebApplicationFactory<Program> factory)
    {
        using var signedIn = await factory.CreateGlobalAdministratorAsync(SharedPolicy);
        // The returned client sends the administrator credential only on /admin routes (see AdministratorBearerHandler).
        var client = factory.CreateDefaultClient(new AdministratorBearerHandler(signedIn.AccessToken));
        return signedIn with { Client = client };
    }

    /// <summary>Creates a user, Application, and active membership through application services, signs in, and registers the user as global administrator.</summary>
    public static async Task<AdministratorClient> CreateGlobalAdministratorAsync(this WebApplicationFactory<Program> factory, TestGlobalAdministratorPolicy policy)
    {
        var administrator = await CreateSignedInUserAsync(factory, applicationId: null, applicationCode: null);
        policy.Add(administrator.UserId);
        return administrator;
    }

    /// <summary>
    /// Creates a user with an active membership in the given Application (a new Application when none is given), a role
    /// holding the named permissions (looked up among the seeded administrative permissions), and signs the user in.
    /// </summary>
    public static async Task<AdministratorClient> CreateApplicationAdministratorAsync(this WebApplicationFactory<Program> factory,
        Guid? applicationId = null, string? applicationCode = null, params string[] permissionCodes)
    {
        return await CreateSignedInUserAsync(factory, applicationId, applicationCode, permissionCodes);
    }

    /// <summary>Registers an Application through the application service, which also seeds its administrative permissions.</summary>
    public static async Task<(Guid ApplicationId, string ApplicationCode)> CreateApplicationAsync(this WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var code = $"app-{Guid.NewGuid():N}";
        var result = await scope.ServiceProvider.GetRequiredService<ApplicationService>().CreateAsync(code, "Application", CancellationToken.None);
        return (result.Application!.Id, code);
    }

    public static async Task<Guid> CreateUserAsync(this WebApplicationFactory<Program> factory, string? email = null)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateUserHandler>().HandleAsync(
            new CreateUserCommand(email ?? $"admin-test-{Guid.NewGuid():N}@example.test", Password, "A", "B", "AB", null), CancellationToken.None);
        return result.User!.Id;
    }

    public static async Task CreateMembershipAsync(this WebApplicationFactory<Program> factory, Guid userId, Guid applicationId)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<MembershipService>().CreateAsync(userId, applicationId, CancellationToken.None);
        if (result.Membership is null) throw new InvalidOperationException("Test membership could not be created.");
    }

    /// <summary>
    /// Signs a user in through the login and session services (the same flow the login endpoint runs, without the
    /// login rate limit) and returns a client carrying the bearer credential.
    /// </summary>
    public static async Task<AdministratorClient> SignInAsync(this WebApplicationFactory<Program> factory, string email, Guid applicationId, string applicationCode)
    {
        using var scope = factory.Services.CreateScope();
        var authenticated = await scope.ServiceProvider.GetRequiredService<LoginService>().AuthenticateAsync(applicationCode, email, Password, CancellationToken.None);
        if (!authenticated.IsSuccess) throw new InvalidOperationException("Test sign-in failed.");
        var session = await scope.ServiceProvider.GetRequiredService<SessionService>().CreateAsync(authenticated, CancellationToken.None);
        if (!session.IsSuccess) throw new InvalidOperationException("Test sign-in failed.");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessCredential!);
        return new AdministratorClient(client, session.UserId!.Value, applicationId, applicationCode, email, session.SessionId!.Value, session.AccessCredential!);
    }

    private static async Task<AdministratorClient> CreateSignedInUserAsync(WebApplicationFactory<Program> factory, Guid? applicationId,
        string? applicationCode, params string[] permissionCodes)
    {
        if (applicationId is null || applicationCode is null) (applicationId, applicationCode) = await factory.CreateApplicationAsync();
        var email = $"admin-test-{Guid.NewGuid():N}@example.test";
        var userId = await factory.CreateUserAsync(email);
        await factory.CreateMembershipAsync(userId, applicationId.Value);
        if (permissionCodes.Length > 0) await GrantPermissionsAsync(factory, applicationId.Value, userId, permissionCodes);
        return await factory.SignInAsync(email, applicationId.Value, applicationCode);
    }

    /// <summary>Creates a role holding the named permissions (which must already exist in the Application) and assigns it to the user.</summary>
    public static async Task<Guid> GrantPermissionsAsync(this WebApplicationFactory<Program> factory, Guid applicationId, Guid userId, params string[] permissionCodes)
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var role = (await provider.GetRequiredService<RoleService>().CreateAsync(applicationId, $"Role {Guid.NewGuid():N}", null, CancellationToken.None)).Role
            ?? throw new InvalidOperationException("Test role could not be created.");
        var permissions = provider.GetRequiredService<IPermissionRepository>();
        var rolePermissions = provider.GetRequiredService<RolePermissionService>();
        foreach (var code in permissionCodes)
        {
            var permission = await permissions.GetByCodeAsync(applicationId, code, CancellationToken.None)
                ?? throw new InvalidOperationException($"Permission {code} does not exist in the test Application.");
            var assigned = await rolePermissions.AssignAsync(applicationId, role.Id, permission.Id, CancellationToken.None);
            if (assigned.RolePermission is null) throw new InvalidOperationException("Test permission assignment failed.");
        }

        var assignedRole = await provider.GetRequiredService<UserRoleService>().AssignAsync(applicationId, userId, role.Id, CancellationToken.None);
        if (assignedRole.UserRole is null) throw new InvalidOperationException("Test role assignment failed.");
        return role.Id;
    }
}
