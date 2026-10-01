namespace GaussAuth.Application.Profiles.Avatars;

public enum ProfileImageRejection
{
    Empty,
    TooLarge,
    UnsupportedType,
    Undecodable,
    DimensionsExceeded
}
