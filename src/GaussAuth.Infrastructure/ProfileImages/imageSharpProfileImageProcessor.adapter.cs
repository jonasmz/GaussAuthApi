using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Application.Profiles.Avatars.Ports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace GaussAuth.Infrastructure.ProfileImages;

public sealed class ImageSharpProfileImageProcessor(ProfileImageLimits limits) : IProfileImageProcessor
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

        try
        {
            ImageInfo info;
            try
            {
                info = await Image.IdentifyAsync(new DecoderOptions(), new MemoryStream(bytes, writable: false), cancellationToken);
            }
            catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
            {
                return ProfileImageProcessingResult.Rejected(ProfileImageRejection.UnsupportedType);
            }

            var format = MapFormat(info.Metadata.DecodedImageFormat);
            if (format is null)
            {
                return ProfileImageProcessingResult.Rejected(ProfileImageRejection.UnsupportedType);
            }

            if (info.Width <= 0 || info.Height <= 0 || info.Width > limits.MaxDimension || info.Height > limits.MaxDimension)
            {
                return ProfileImageProcessingResult.Rejected(ProfileImageRejection.DimensionsExceeded);
            }

            using var image = await Image.LoadAsync(
                new DecoderOptions { MaxFrames = 1 },
                new MemoryStream(bytes, writable: false),
                cancellationToken);

            Strip(image);

            using var output = new MemoryStream();
            await image.SaveAsync(output, EncoderFor(format.Value), cancellationToken);
            if (output.Length > limits.MaxBytes)
            {
                return ProfileImageProcessingResult.Rejected(ProfileImageRejection.TooLarge);
            }

            return ProfileImageProcessingResult.Success(
                new ProcessedProfileImage(output.ToArray(), format.Value, image.Width, image.Height));
        }
        catch (Exception exception) when (exception is InvalidImageContentException or UnknownImageFormatException
            or NotSupportedException or InvalidOperationException or ImageProcessingException)
        {
            return ProfileImageProcessingResult.Rejected(ProfileImageRejection.Undecodable);
        }
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

    private static ProfileImageFormat? MapFormat(IImageFormat? format) => format?.Name switch
    {
        "JPEG" => ProfileImageFormat.Jpeg,
        "PNG" => ProfileImageFormat.Png,
        "WEBP" => ProfileImageFormat.WebP,
        _ => null
    };

    private static void Strip(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.CicpProfile = null;
    }

    private static IImageEncoder EncoderFor(ProfileImageFormat format) => format switch
    {
        ProfileImageFormat.Jpeg => new JpegEncoder { Quality = 85, SkipMetadata = true },
        ProfileImageFormat.Png => new PngEncoder { SkipMetadata = true },
        ProfileImageFormat.WebP => new WebpEncoder { SkipMetadata = true },
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
