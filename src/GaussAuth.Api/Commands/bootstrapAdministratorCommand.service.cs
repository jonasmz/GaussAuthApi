using GaussAuth.Api.DependencyInjection;
using GaussAuth.Application.Administration.Authorization;
using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Users.CreateUser;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Infrastructure.Bootstrap;
using GaussAuth.Infrastructure.Configuration;
using GaussAuth.Infrastructure.DependencyInjection;
using GaussAuth.Infrastructure.Identity;
using GaussAuth.Infrastructure.Persistence;
using GaussAuth.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaussAuth.Api.Commands;

/// <summary>A minimal, no-HTTP host for the one-time first-administrator bootstrap procedure.</summary>
public static class BootstrapAdministratorCommand
{
    public const int FailureExitCode = 1;

    public static async Task<int> RunAsync(string[] arguments)
    {
        // Inputs are configuration-only. Arguments are intentionally ignored by the dispatcher contract.
        var builder = Host.CreateApplicationBuilder(arguments);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

        if (!TryReadInput(builder.Configuration, out var input, out var inputCategory))
        {
            await Console.Error.WriteLineAsync($"Bootstrap administrator failed. Category {inputCategory}.");
            return FailureExitCode;
        }

        string connection;
        try { connection = AuthenticationDatabaseConnection.Read(builder.Configuration); }
        catch (StartupConfigurationException)
        {
            await Console.Error.WriteLineAsync("Bootstrap administrator failed. Category configuration.");
            return FailureExitCode;
        }

        AddBootstrapServices(builder.Services, connection);
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<FirstAdministratorBootstrap>()
                .RunAsync(input!, CancellationToken.None);
            if (result.Succeeded)
            {
                await Console.Out.WriteLineAsync($"Bootstrap administrator created. UserId={result.UserId}");
                await Console.Out.WriteLineAsync($"Set Administration__GlobalAdministratorUserIds__0={result.UserId} and restart the API.");
                return 0;
            }

            await Console.Error.WriteLineAsync($"Bootstrap administrator failed. Category {result.Category}.{PasswordPolicyHint(result)}");
            return FailureExitCode;
        }
        catch (Exception)
        {
            // Intentionally do not emit exception details: connection strings and bootstrap inputs are secrets.
            await Console.Error.WriteLineAsync("Bootstrap administrator failed. Category database.");
            return FailureExitCode;
        }
    }

    private static void AddBootstrapServices(IServiceCollection services, string connection)
    {
        services.AddAuthenticationPersistence(connection);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICredentialProvisioningService, IdentityCredentialProvisioningService>();
        services.AddScoped<CreateUserHandler>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SecurityEventCatalog>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<AdministrativeActorContext>();
        services.AddScoped<IAdministrativeActorContext>(provider => provider.GetRequiredService<AdministrativeActorContext>());
        services.AddScoped<ISecurityEventRecorder, PersistedSecurityEventRecorder>();
        services.AddScoped<FirstAdministratorBootstrap>();
    }

    private static bool TryReadInput(IConfiguration configuration, out FirstAdministratorBootstrapInput? input, out string category)
    {
        input = null;
        category = "missing-input";
        var email = configuration["Bootstrap:AdministratorEmail"];
        var inlinePassword = configuration["Bootstrap:AdministratorPassword"];
        var passwordFile = configuration["Bootstrap:AdministratorPasswordFile"];
        if (string.IsNullOrWhiteSpace(email) || (!string.IsNullOrWhiteSpace(inlinePassword) && !string.IsNullOrWhiteSpace(passwordFile))) return false;
        if (!IsValidEmail(email)) { category = "invalid-email"; return false; }

        string? password = inlinePassword;
        if (!string.IsNullOrWhiteSpace(passwordFile))
        {
            try { password = File.ReadAllText(passwordFile).TrimEnd('\r', '\n'); }
            catch (Exception) { category = "configuration"; return false; }
        }

        if (string.IsNullOrWhiteSpace(password)) return false;
        var firstName = configuration["Bootstrap:AdministratorFirstName"] ?? "Auth";
        var lastName = configuration["Bootstrap:AdministratorLastName"] ?? "Administrator";
        input = new FirstAdministratorBootstrapInput(email, password, firstName, lastName, $"{firstName} {lastName}");
        return true;
    }

    private static string PasswordPolicyHint(FirstAdministratorBootstrapResult result)
    {
        if (result.Category != "password-policy" || result.ValidationErrors is null) return string.Empty;
        var rules = result.ValidationErrors.Values.SelectMany(values => values)
            .Select(value => value.Split(':', 2)[0].Trim()).Where(value => value.Length > 0).Distinct();
        return $" Rules: {string.Join(", ", rules)}.";
    }

    private static bool IsValidEmail(string email)
    {
        try { return new System.Net.Mail.MailAddress(email).Address.Equals(email, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }
}
