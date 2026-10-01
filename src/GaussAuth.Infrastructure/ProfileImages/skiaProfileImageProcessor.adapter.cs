using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Application.Profiles.Avatars.Ports;
using SkiaSharp;

namespace GaussAuth.Infrastructure.ProfileImages;

public sealed class SkiaProfileImageProcessor(ProfileImageLimits limits) : IProfileImageProcessor
{
    public async Task<ProfileImageProcessingResult> ProcessAsync(Stream input, CancellationToken cancellationToken)
    {
        var bytes = await ReadBoundedAsync(input, cancellationToken);
        if (bytes is null)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.TooLarge);
        }

        if (bytes.Length == 0)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.Empty);
        }

        // Skia work is synchronous and CPU bound; keep it off the request thread.
        return await Task.Run(() => Process(bytes), cancellationToken);
    }

    private ProfileImageProcessingResult Process(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return ProfileImageProcessingResult.Rejected(
                LooksLikeKnownSignature(bytes) ? ProfileImageRejection.Undecodable : ProfileImageRejection.UnsupportedType);
        }

        var format = MapFormat(codec.EncodedFormat);
        if (format is null)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.UnsupportedType);
        }

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > limits.MaxDimension || info.Height > limits.MaxDimension)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.DimensionsExceeded);
        }

        // Decoding the first frame and re-encoding from raw pixels drops every metadata block (EXIF, XMP, ICC, text chunks).
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.Undecodable);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(EncodedFormatFor(format.Value), QualityFor(format.Value));
        if (encoded is null)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.Undecodable);
        }

        if (encoded.Size > limits.MaxBytes)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.TooLarge);
        }

        return ProfileImageProcessingResult.Success(
            new ProcessedProfileImage(encoded.ToArray(), format.Value, info.Width, info.Height));
    }

    private async Task<byte[]?> ReadBoundedAsync(Stream input, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limits.MaxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool LooksLikeKnownSignature(byte[] bytes) =>
        bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) ||
        bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) ||
        (bytes.Length > 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8));

    private static ProfileImageFormat? MapFormat(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Jpeg => ProfileImageFormat.Jpeg,
        SKEncodedImageFormat.Png => ProfileImageFormat.Png,
        SKEncodedImageFormat.Webp => ProfileImageFormat.WebP,
        _ => null
    };

    private static SKEncodedImageFormat EncodedFormatFor(ProfileImageFormat format) => format switch
    {
        ProfileImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
        ProfileImageFormat.Png => SKEncodedImageFormat.Png,
        ProfileImageFormat.WebP => SKEncodedImageFormat.Webp,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static int QualityFor(ProfileImageFormat format) => format == ProfileImageFormat.Png ? 100 : 85;
}
