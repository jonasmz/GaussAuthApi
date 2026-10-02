namespace GaussAuth.Application.Administration;

/// <summary>Administrative limits. Invalid configuration fails at startup.</summary>
public sealed record AdministrationOptions
{
    public const int DefaultMaxBulkSessionRevocation = 1000;
    public const int MaximumAllowedBulkSessionRevocation = 10_000;

    public int MaxBulkSessionRevocation { get; }

    public AdministrationOptions(int maxBulkSessionRevocation = DefaultMaxBulkSessionRevocation)
    {
        if (maxBulkSessionRevocation is < 1 or > MaximumAllowedBulkSessionRevocation)
            throw new InvalidOperationException("Administration:MaxBulkSessionRevocation must be between 1 and 10000.");
        MaxBulkSessionRevocation = maxBulkSessionRevocation;
    }
}
