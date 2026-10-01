namespace GaussAuth.Application.Profiles.Avatars;

public sealed record ProfileImageProcessingResult
{
    private ProfileImageProcessingResult(ProcessedProfileImage? image, ProfileImageRejection? rejection)
    {
        Image = image;
        Rejection = rejection;
    }

    public ProcessedProfileImage? Image { get; }

    public ProfileImageRejection? Rejection { get; }

    public bool IsSuccess => Image is not null;

    public static ProfileImageProcessingResult Success(ProcessedProfileImage image) => new(image, null);

    public static ProfileImageProcessingResult Rejected(ProfileImageRejection rejection) => new(null, rejection);
}
