namespace GaussAuth.Application.Passwords.Ports;

public sealed record RecoveryDeliveryInstruction(Guid UserId, string Email, string ResetCredential);
