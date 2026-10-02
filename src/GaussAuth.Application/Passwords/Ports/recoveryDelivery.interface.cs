namespace GaussAuth.Application.Passwords.Ports;

public interface IRecoveryDelivery
{
    /// <summary>
    /// Whether a delivery channel is configured and usable. When false the recovery request is answered generically
    /// without generating a reset credential, because there would be nowhere to deliver it.
    /// </summary>
    bool IsAvailable { get; }

    Task DeliverAsync(RecoveryDeliveryInstruction instruction, CancellationToken cancellationToken);
}
