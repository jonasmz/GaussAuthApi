namespace GaussAuth.Application.Passwords.Ports;

public interface IRecoveryDelivery
{
    Task DeliverAsync(RecoveryDeliveryInstruction instruction, CancellationToken cancellationToken);
}
