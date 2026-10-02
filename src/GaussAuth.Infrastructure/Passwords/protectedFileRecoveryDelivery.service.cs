using System.Text.Json;
using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Infrastructure.Passwords;

public sealed class ProtectedFileRecoveryDelivery(IConfiguration configuration, IHostEnvironment environment) : IRecoveryDelivery
{
    // A protected local file is a development/testing aid only; Production-class environments need an operator-supplied adapter.
    public bool IsAvailable =>
        !environment.IsProductionClass() && !string.IsNullOrWhiteSpace(configuration["PasswordRecovery:DeliveryFile"]);

    public async Task DeliverAsync(RecoveryDeliveryInstruction instruction, CancellationToken cancellationToken)
    {
        if (environment.IsProductionClass())
            throw new InvalidOperationException("No password-recovery delivery adapter is configured for this environment.");
        var path = configuration["PasswordRecovery:DeliveryFile"];
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Password-recovery delivery file is not configured.");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var serialized = JsonSerializer.Serialize(new { instruction.UserId, instruction.Email, instruction.ResetCredential });
        await File.AppendAllTextAsync(path, serialized + Environment.NewLine, cancellationToken);
    }
}
