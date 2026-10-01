using GaussAuth.Api.Sessions;
using GaussAuth.Api.Users;
using GaussAuth.Application.Profiles.Avatars;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;

namespace GaussAuth.Api.Profiles;

public static class ProfileAvatarEndpoints
{
    /// <summary>Allowance for multipart framing around the file part.</summary>
    public const long MultipartOverheadBytes = 16 * 1024;

    public static IEndpointRouteBuilder MapProfileAvatarEndpoints(this IEndpointRouteBuilder app, ProfileImageLimits limits)
    {
        var bodyLimit = limits.MaxBytes + MultipartOverheadBytes;

        app.MapPut("/me/profile/avatar", (HttpContext context, ProfileAvatarService service, CancellationToken ct) =>
                UploadAsync(context, service, bodyLimit, ct))
            .RequireRateLimiting("profile-image-write")
            .WithMetadata(new RequestSizeLimitAttribute(bodyLimit), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = bodyLimit });

        app.MapDelete("/me/profile/avatar", RemoveAsync)
            .RequireRateLimiting("profile-image-write");

        app.MapGet("/profile-images/{avatarReference}", GetAsync);
        return app;
    }

    private static async Task<IResult> UploadAsync(HttpContext context, ProfileAvatarService service, long bodyLimit, CancellationToken cancellationToken)
    {
        var credential = context.Request.ReadBearerCredential();
        if (credential is null) return InvalidCredential(context);

        if (!context.Request.HasFormContentType ||
            !context.Request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(StatusCodes.Status400BadRequest, "A single image file part is required.");
        }

        if (context.Request.ContentLength > bodyLimit)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "The upload exceeds the allowed size.");
        }

        IFormCollection form;
        try
        {
            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeFeature)
            {
                sizeFeature.MaxRequestBodySize = bodyLimit;
            }

            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "The upload exceeds the allowed size.");
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException or IOException)
        {
            return Problem(StatusCodes.Status400BadRequest, "A single image file part is required.");
        }

        if (form.Files.Count != 1 || form.Count != 0 || form.Files[0].Name != "file")
        {
            return Problem(StatusCodes.Status400BadRequest, "A single image file part is required.");
        }

        await using var content = form.Files[0].OpenReadStream();
        var result = await service.SetAsync(credential, content, cancellationToken);
        return result.Outcome switch
        {
            ProfileAvatarOutcome.Success => TypedResults.Ok(UserProfileResponse.FromDomain(result.Profile!)),
            ProfileAvatarOutcome.NotAuthenticated => InvalidCredential(context),
            _ => RejectionResult(result.Rejection!.Value)
        };
    }

    private static async Task<IResult> RemoveAsync(HttpContext context, ProfileAvatarService service, CancellationToken cancellationToken)
    {
        var credential = context.Request.ReadBearerCredential();
        if (credential is null) return InvalidCredential(context);

        var result = await service.RemoveAsync(credential, cancellationToken);
        return result.Outcome == ProfileAvatarOutcome.Success ? TypedResults.NoContent() : InvalidCredential(context);
    }

    private static async Task<IResult> GetAsync(string avatarReference, HttpContext context, ProfileAvatarReadService service, CancellationToken cancellationToken)
    {
        var image = await service.OpenAsync(avatarReference, cancellationToken);
        if (image is null) return TypedResults.NotFound();

        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        context.Response.Headers.ContentDisposition = "inline";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.Stream(image.Content, image.MediaType);
    }

    private static IResult RejectionResult(ProfileImageRejection rejection) => rejection switch
    {
        ProfileImageRejection.TooLarge => Problem(StatusCodes.Status413PayloadTooLarge, "The image exceeds the allowed size."),
        ProfileImageRejection.UnsupportedType => Problem(StatusCodes.Status415UnsupportedMediaType, "Only JPEG, PNG, and WebP images are supported."),
        _ => Problem(StatusCodes.Status422UnprocessableEntity, "The image is invalid or not allowed.")
    };

    private static IResult Problem(int status, string title) => TypedResults.Problem(statusCode: status, title: title);

    private static IResult InvalidCredential(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Problem(StatusCodes.Status401Unauthorized, "Access is not valid.");
    }
}
