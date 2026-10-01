using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Application.Profiles.Avatars.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Infrastructure.ProfileImages;

/// <summary>
/// Local filesystem storage. File names are generated opaque references only; every path is
/// resolved beneath the configured root from a strictly parsed reference or a generated token.
/// </summary>
public sealed class LocalProfileImageStorage : IProfileImageStorage
{
    private const string StagingDirectoryName = ".staging";
    private const string FinalDirectoryName = "avatars";

    private readonly string finalRoot;
    private readonly string stagingRoot;
    private readonly ILogger<LocalProfileImageStorage> logger;

    public LocalProfileImageStorage(ProfileImagesOptions options, ILogger<LocalProfileImageStorage> logger)
    {
        this.logger = logger;
        finalRoot = Path.Combine(options.RootPath, FinalDirectoryName);
        stagingRoot = Path.Combine(options.RootPath, StagingDirectoryName);
        Directory.CreateDirectory(finalRoot);
        Directory.CreateDirectory(stagingRoot);
    }

    public async Task<StagedProfileImage> StageAsync(ProcessedProfileImage image, CancellationToken cancellationToken)
    {
        var reference = ProfileImageReference.Create(image.Format);
        var token = Guid.NewGuid().ToString("N") + ".tmp";
        var path = Path.Combine(stagingRoot, token);
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await stream.WriteAsync(image.Content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        return new StagedProfileImage(reference, token);
    }

    public Task PromoteAsync(StagedProfileImage staged, CancellationToken cancellationToken)
    {
        var source = ResolveStaging(staged.StagingToken);
        var destination = ResolveFinal(staged.Reference);
        File.Move(source, destination, overwrite: false);
        return Task.CompletedTask;
    }

    public Task<bool> DiscardStagedAsync(StagedProfileImage staged, CancellationToken cancellationToken) =>
        Task.FromResult(TryDelete(ResolveStaging(staged.StagingToken), staged.Reference.Value, "discard-staged"));

    public Task<bool> DeleteAsync(ProfileImageReference reference, CancellationToken cancellationToken) =>
        Task.FromResult(TryDelete(ResolveFinal(reference), reference.Value, "delete"));

    public Task<ProfileImageContent?> OpenReadAsync(ProfileImageReference reference, CancellationToken cancellationToken)
    {
        var path = ResolveFinal(reference);
        try
        {
            var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<ProfileImageContent?>(new ProfileImageContent(stream, reference.MediaType, stream.Length));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<ProfileImageContent?>(null);
        }
    }

    private string ResolveFinal(ProfileImageReference reference) =>
        Path.Combine(finalRoot, reference.Value);

    private string ResolveStaging(string token)
    {
        if (token.Length != 36 || !token.EndsWith(".tmp", StringComparison.Ordinal) ||
            !token[..32].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new ArgumentException("Staging token is not a generated token.", nameof(token));
        }

        return Path.Combine(stagingRoot, token);
    }

    private bool TryDelete(string path, string reference, string operation)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                "Profile image storage {Operation} failed for reference {Reference}: {FailureType}.",
                operation, reference, exception.GetType().Name);
            return false;
        }
    }
}
