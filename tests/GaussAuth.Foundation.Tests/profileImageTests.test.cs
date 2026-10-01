using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Application.Profiles.Avatars.Ports;
using GaussAuth.Application.Users.Ports;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using GaussAuth.Domain.Users;
using GaussAuth.Infrastructure.ProfileImages;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ProfileImageTests
{
    private static readonly ProfileImageLimits SmallLimits = new(64 * 1024, 64);

    [TestMethod]
    [DataRow(ProfileImageFormat.Jpeg, ".jpg", "image/jpeg")]
    [DataRow(ProfileImageFormat.Png, ".png", "image/png")]
    [DataRow(ProfileImageFormat.WebP, ".webp", "image/webp")]
    public void Generated_references_are_opaque_and_round_trip(ProfileImageFormat format, string extension, string mediaType)
    {
        var first = ProfileImageReference.Create(format);
        var second = ProfileImageReference.Create(format);

        Assert.AreNotEqual(first.Value, second.Value);
        Assert.IsTrue(first.Value.EndsWith(extension, StringComparison.Ordinal));
        Assert.IsTrue(ProfileImageReference.TryParse(first.Value, out var parsed));
        Assert.AreEqual(format, parsed.Format);
        Assert.AreEqual(mediaType, parsed.MediaType);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("../../etc/passwd")]
    [DataRow("..%2f..%2fsecret.png")]
    [DataRow("0123456789abcdef0123456789abcdef.svg")]
    [DataRow("0123456789ABCDEF0123456789abcdef.png")]
    [DataRow("0123456789abcdef0123456789abcde/.png")]
    [DataRow("0123456789abcdef0123456789abcdef.png/")]
    [DataRow("photo.png")]
    public void Malformed_references_are_rejected(string? value)
    {
        Assert.IsFalse(ProfileImageReference.TryParse(value, out _));
    }

    [TestMethod]
    [DataRow(ProfileImageFormat.Jpeg)]
    [DataRow(ProfileImageFormat.Png)]
    [DataRow(ProfileImageFormat.WebP)]
    public async Task Allowed_formats_are_accepted_by_content_and_reencoded_in_same_format(ProfileImageFormat format)
    {
        var processor = new SkiaProfileImageProcessor(SmallLimits);

        var result = await processor.ProcessAsync(new MemoryStream(Encode(format, 32, 24)), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(format, result.Image!.Format);
        Assert.AreEqual(32, result.Image.Width);
        Assert.AreEqual(24, result.Image.Height);
        Assert.AreEqual(format, DetectFormat(result.Image.Content));
    }

    [TestMethod]
    public async Task Invalid_content_is_rejected_regardless_of_name_or_declared_type()
    {
        var processor = new SkiaProfileImageProcessor(SmallLimits);
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        var truncated = Encode(ProfileImageFormat.Png, 32, 32)[..40];

        Assert.AreEqual(ProfileImageRejection.UnsupportedType, (await processor.ProcessAsync(new MemoryStream(svg), CancellationToken.None)).Rejection);
        Assert.AreEqual(ProfileImageRejection.UnsupportedType, (await processor.ProcessAsync(new MemoryStream("GIF89a not really"u8.ToArray()), CancellationToken.None)).Rejection);
        Assert.AreEqual(ProfileImageRejection.Empty, (await processor.ProcessAsync(new MemoryStream(), CancellationToken.None)).Rejection);
        Assert.IsFalse((await processor.ProcessAsync(new MemoryStream(truncated), CancellationToken.None)).IsSuccess);
    }

    [TestMethod]
    public async Task Gif_is_rejected_even_though_the_decoder_recognizes_it()
    {
        byte[] gif =
        [
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
            0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0x02, 0x02, 0x44, 0x01, 0x00, 0x3B
        ];
        var processor = new SkiaProfileImageProcessor(SmallLimits);

        var result = await processor.ProcessAsync(new MemoryStream(gif), CancellationToken.None);

        Assert.AreEqual(ProfileImageRejection.UnsupportedType, result.Rejection);
    }

    [TestMethod]
    public async Task Configured_byte_and_dimension_limits_are_enforced()
    {
        var processor = new SkiaProfileImageProcessor(new ProfileImageLimits(2048, 64));

        var tooWide = await processor.ProcessAsync(new MemoryStream(Encode(ProfileImageFormat.Png, 65, 10)), CancellationToken.None);
        var tooTall = await processor.ProcessAsync(new MemoryStream(Encode(ProfileImageFormat.Png, 10, 65)), CancellationToken.None);
        var oversized = await processor.ProcessAsync(new MemoryStream(new byte[2049]), CancellationToken.None);

        Assert.AreEqual(ProfileImageRejection.DimensionsExceeded, tooWide.Rejection);
        Assert.AreEqual(ProfileImageRejection.DimensionsExceeded, tooTall.Rejection);
        Assert.AreEqual(ProfileImageRejection.TooLarge, oversized.Rejection);
    }

    [TestMethod]
    public async Task Metadata_is_stripped_on_reencode()
    {
        var processor = new SkiaProfileImageProcessor(SmallLimits);
        var original = WithExifSegment(Encode(ProfileImageFormat.Jpeg, 16, 16), "secret-tool-name private-person");
        Assert.IsTrue(Contains(original, "private-person"u8), "Fixture must carry metadata.");

        var result = await processor.ProcessAsync(new MemoryStream(original), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(Contains(result.Image!.Content, "private-person"u8));
        Assert.IsFalse(Contains(result.Image.Content, "secret-tool-name"u8));
        Assert.IsFalse(Contains(result.Image.Content, "Exif"u8), "No EXIF segment survives.");
    }

    [TestMethod]
    public async Task Storage_uses_generated_names_and_stays_beneath_the_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaussauth-profile-image-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalProfileImageStorage(
                new ProfileImagesOptions { RootPath = root }, NullLogger<LocalProfileImageStorage>.Instance);
            var processed = new ProcessedProfileImage(Encode(ProfileImageFormat.Png, 8, 8), ProfileImageFormat.Png, 8, 8);

            var staged = await storage.StageAsync(processed, CancellationToken.None);
            Assert.IsNull(await storage.OpenReadAsync(staged.Reference, CancellationToken.None), "Staged content is not publicly readable.");
            await storage.PromoteAsync(staged, CancellationToken.None);

            await using (var content = (await storage.OpenReadAsync(staged.Reference, CancellationToken.None))!.Content)
            {
                Assert.AreEqual(processed.Content.Length, content.Length);
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                StringAssert.StartsWith(Path.GetFullPath(file), root);
            }

            Assert.AreEqual(staged.Reference.Value, Path.GetFileName(Directory.GetFiles(Path.Combine(root, "avatars")).Single()));
            Assert.IsTrue(await storage.DeleteAsync(staged.Reference, CancellationToken.None));
            Assert.IsTrue(await storage.DeleteAsync(staged.Reference, CancellationToken.None), "Deleting an absent file is idempotent.");
            Assert.IsNull(await storage.OpenReadAsync(staged.Reference, CancellationToken.None));

            var discarded = await storage.StageAsync(processed, CancellationToken.None);
            Assert.IsTrue(await storage.DiscardStagedAsync(discarded, CancellationToken.None));
            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(root, ".staging")).Length);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.PromoteAsync(
                new StagedProfileImage(staged.Reference, "../../outside.tmp"), CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Options_use_defaults_and_reject_invalid_configuration()
    {
        var valid = Load(new() { ["ProfileImages:RootPath"] = "/var/lib/gaussauth/images" }, production: true);
        Assert.AreEqual(5L * 1024 * 1024, valid.MaxBytes);
        Assert.AreEqual(4096, valid.MaxDimension);

        var configured = Load(new() { ["ProfileImages:RootPath"] = "/x/y", ["ProfileImages:MaxBytes"] = "1024", ["ProfileImages:MaxDimension"] = "128" }, production: true);
        Assert.AreEqual(1024, configured.MaxBytes);
        Assert.AreEqual(128, configured.MaxDimension);

        Assert.ThrowsExactly<InvalidOperationException>(() => Load(new(), production: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => Load(new() { ["ProfileImages:RootPath"] = "relative/path" }, production: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => Load(new() { ["ProfileImages:RootPath"] = "/x", ["ProfileImages:MaxBytes"] = "0" }, production: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => Load(new() { ["ProfileImages:RootPath"] = "/x", ["ProfileImages:MaxDimension"] = "-5" }, production: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => Load(new() { ["ProfileImages:RootPath"] = "/x", ["ProfileImages:MaxBytes"] = "abc" }, production: true));
    }

    [TestMethod]
    public void Domain_avatar_transitions_accept_only_opaque_references()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = UserProfile.Create(Guid.NewGuid(), "A", "B", "AB", null, now);
        var reference = ProfileImageReference.Create(ProfileImageFormat.WebP).Value;

        profile.SetAvatarReference(reference, now.AddMinutes(1));
        Assert.AreEqual(reference, profile.AvatarReference);
        Assert.AreEqual(now.AddMinutes(1), profile.UpdatedAt);

        foreach (var bad in new[] { "", " ", "../x.png", "a/b.png", "a\\b.png", "C:\\x.png", "a b.png", ".hidden", new string('a', 200) })
        {
            Assert.ThrowsExactly<ArgumentException>(() => profile.SetAvatarReference(bad, now));
        }

        Assert.AreEqual(reference, profile.AvatarReference);
        profile.ClearAvatarReference(now.AddMinutes(2));
        Assert.IsNull(profile.AvatarReference);
        profile.ClearAvatarReference(now.AddMinutes(3));
        Assert.IsNull(profile.AvatarReference);
    }

    [TestMethod]
    public void Domain_and_application_do_not_depend_on_imaging_filesystem_or_http_types()
    {
        var root = FindRepositoryRoot();
        var forbidden = new[] { "SkiaSharp", "System.IO.File", "System.IO.Directory", "FileStream", "Microsoft.AspNetCore", "IFormFile" };
        foreach (var tree in new[] { "src/GaussAuth.Domain", "src/GaussAuth.Application" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, tree), "*.cs", SearchOption.AllDirectories)
                         .Where(path => !path.Contains("/obj/") && !path.Contains("/bin/")))
            {
                var text = File.ReadAllText(file);
                foreach (var token in forbidden)
                {
                    Assert.IsFalse(text.Contains(token, StringComparison.Ordinal), $"{Path.GetRelativePath(root, file)} references {token}.");
                }
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GaussAuth.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }


    // ---- HTTP lifecycle (US1) and retrieval (US2) ----

    [TestMethod]
    public async Task Owner_can_upload_replace_and_idempotently_remove_their_own_avatar()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);
        var other = await CreateLoginAsync(client);

        var first = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 20, 20));
        Assert.AreEqual(HttpStatusCode.OK, first.Status);
        var firstReference = first.Body.GetProperty("avatarReference").GetString()!;
        Assert.AreEqual($"/profile-images/{firstReference}", first.Body.GetProperty("avatarUrl").GetString());
        Assert.IsTrue(ProfileImageReference.TryParse(firstReference, out _));
        CollectionAssert.AreEqual(new[] { firstReference }, env.StoredFiles());

        var otherUpload = await UploadAsync(client, other.AccessToken, Encode(ProfileImageFormat.Jpeg, 20, 20));
        var otherReference = otherUpload.Body.GetProperty("avatarReference").GetString()!;

        var unrelated = Path.Combine(env.Root, "avatars", "notes-not-an-avatar.txt");
        await File.WriteAllTextAsync(unrelated, "keep me");

        var second = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.WebP, 20, 20));
        Assert.AreEqual(HttpStatusCode.OK, second.Status);
        var secondReference = second.Body.GetProperty("avatarReference").GetString()!;
        Assert.AreNotEqual(firstReference, secondReference);
        Assert.IsFalse(env.StoredFiles().Contains(firstReference), "Old file is retired after the reference commits.");
        Assert.IsTrue(env.StoredFiles().Contains(secondReference));
        Assert.IsTrue(env.StoredFiles().Contains(otherReference), "Another user's avatar is untouched.");
        Assert.IsTrue(File.Exists(unrelated), "Unrelated files are never deleted.");
        Assert.AreEqual(secondReference, await ReadReferenceAsync(client, user.UserId));
        Assert.AreEqual(otherReference, await ReadReferenceAsync(client, other.UserId));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var removed = await client.DeleteAsync("/me/profile/avatar");
        using var removedAgain = await client.DeleteAsync("/me/profile/avatar");
        Assert.AreEqual(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, removedAgain.StatusCode);
        Assert.IsNull(await ReadReferenceAsync(client, user.UserId));
        Assert.IsFalse(env.StoredFiles().Contains(secondReference));
        Assert.IsTrue(env.StoredFiles().Contains(otherReference));
    }

    [TestMethod]
    public async Task Avatar_endpoints_require_a_valid_session_and_ignore_request_user_ids()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);
        var victim = await CreateLoginAsync(client);

        var anonymous = await UploadAsync(client, null, Encode(ProfileImageFormat.Png, 8, 8));
        var invalid = await UploadAsync(client, "not-a-credential", Encode(ProfileImageFormat.Png, 8, 8));
        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.AreEqual(HttpStatusCode.Unauthorized, invalid.Status);
        using var anonymousDelete = await client.DeleteAsync("/me/profile/avatar");
        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymousDelete.StatusCode);

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encode(ProfileImageFormat.Png, 8, 8)), "file", "a.png" },
            { new StringContent(victim.UserId.ToString()), "userId" }
        };
        using var request = new HttpRequestMessage(HttpMethod.Put, "/me/profile/avatar") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var rejected = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.IsNull(await ReadReferenceAsync(client, victim.UserId));
        Assert.AreEqual(0, env.StoredFiles().Length);
    }

    [TestMethod]
    public async Task Upload_requires_exactly_one_file_part()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        using var json = await client.PutAsJsonAsync("/me/profile/avatar", new { file = "x" });
        using var none = await client.PutAsync("/me/profile/avatar", new MultipartFormDataContent());
        using var two = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encode(ProfileImageFormat.Png, 8, 8)), "file", "a.png" },
            { new ByteArrayContent(Encode(ProfileImageFormat.Png, 8, 8)), "file", "b.png" }
        };
        using var twoResponse = await client.PutAsync("/me/profile/avatar", two);
        using var wrongName = new MultipartFormDataContent { { new ByteArrayContent(Encode(ProfileImageFormat.Png, 8, 8)), "image", "a.png" } };
        using var wrongNameResponse = await client.PutAsync("/me/profile/avatar", wrongName);

        Assert.AreEqual(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, twoResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, wrongNameResponse.StatusCode);
        Assert.AreEqual(0, env.StoredFiles().Length);
    }

    [TestMethod]
    public async Task Unsafe_uploads_are_rejected_without_changing_the_profile_or_echoing_input()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);
        var good = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8, 8));
        var goodReference = good.Body.GetProperty("avatarReference").GetString();

        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        var spoofed = await UploadAsync(client, user.AccessToken, svg, "secret-name.png", "image/png");
        var oversized = await UploadAsync(client, user.AccessToken, new byte[(5 * 1024 * 1024) + 1], "big.png", "image/png");
        var corrupt = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 32, 32)[..50], "corrupt.png", "image/png");

        Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, spoofed.Status);
        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, oversized.Status);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, corrupt.Status);
        foreach (var rejected in new[] { spoofed, oversized, corrupt })
        {
            Assert.IsFalse(rejected.Raw.Contains("secret-name", StringComparison.Ordinal));
            Assert.IsFalse(rejected.Raw.Contains(env.Root, StringComparison.Ordinal));
        }

        Assert.AreEqual(goodReference, await ReadReferenceAsync(client, user.UserId));
        CollectionAssert.AreEqual(new[] { goodReference }, env.StoredFiles());
        Assert.AreEqual(0, Directory.GetFiles(Path.Combine(env.Root, ".staging")).Length);
    }

    [TestMethod]
    public async Task Public_retrieval_serves_only_current_known_references_with_trusted_headers()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);

        var upload = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 10, 10), "x.html", "text/html");
        var reference = upload.Body.GetProperty("avatarReference").GetString()!;

        using var anonymous = factory.CreateClient();
        using var response = await anonymous.GetAsync($"/profile-images/{reference}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.AreEqual("inline", response.Content.Headers.ContentDisposition?.DispositionType ?? string.Join(',', response.Headers.GetValues("Content-Disposition")));
        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        StringAssert.Contains(response.Headers.CacheControl!.ToString(), "public");
        StringAssert.Contains(response.Headers.CacheControl.ToString(), "immutable");
        Assert.AreEqual(10, SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync()).Width);

        var replaced = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Jpeg, 10, 10));
        using var retired = await anonymous.GetAsync($"/profile-images/{reference}");
        using var current = await anonymous.GetAsync($"/profile-images/{replaced.Body.GetProperty("avatarReference").GetString()}");
        Assert.AreEqual(HttpStatusCode.NotFound, retired.StatusCode);
        Assert.AreEqual("image/jpeg", current.Content.Headers.ContentType!.MediaType);

        foreach (var bad in new[] { "0123456789abcdef0123456789abcdef.png", "..", "%2e%2e%2fappsettings.json", "photo.png", "0123456789abcdef0123456789abcdef.svg", "..%5C..%5Cx.png" })
        {
            using var notFound = await anonymous.GetAsync($"/profile-images/{bad}");
            Assert.AreEqual(HttpStatusCode.NotFound, notFound.StatusCode, bad);
            var body = await notFound.Content.ReadAsStringAsync();
            Assert.IsFalse(body.Contains(env.Root, StringComparison.Ordinal));
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var removed = await client.DeleteAsync("/me/profile/avatar");
        using var afterRemoval = await anonymous.GetAsync($"/profile-images/{replaced.Body.GetProperty("avatarReference").GetString()}");
        Assert.AreEqual(HttpStatusCode.NotFound, afterRemoval.StatusCode);
    }

    [TestMethod]
    public async Task Avatar_changes_and_rejections_record_allow_listed_security_events()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);

        await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8, 8));
        await UploadAsync(client, user.AccessToken, "<svg/>"u8.ToArray(), "secret-name.svg");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var removed = await client.DeleteAsync("/me/profile/avatar");
        using var removedAgain = await client.DeleteAsync("/me/profile/avatar");

        using var scope = factory.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().SecurityEvents
            .Where(item => item.UserId == user.UserId && item.EventType.StartsWith("profile.avatar"))
            .ToListAsync();
        CollectionAssert.AreEquivalent(
            new[] { "profile.avatar.updated", "profile.avatar.upload.rejected", "profile.avatar.removed" },
            events.Select(item => item.EventType).ToArray());
        var rejected = events.Single(item => item.EventType == "profile.avatar.upload.rejected");
        Assert.AreEqual("unsupported-type", rejected.Reason);
        Assert.IsTrue(events.All(item => item.Metadata is null && item.SubjectId is null));
    }


    // ---- Failure injection, concurrency and safety (US3) ----

    [TestMethod]
    public async Task Old_file_cleanup_failure_keeps_the_new_avatar_current_and_logs_a_safe_orphan_warning()
    {
        using var env = new ImageEnvironment();
        var faults = new StorageFaults();
        var logs = new CapturingLoggerProvider();
        using var factory = await env.CreateFactoryAsync(services => InjectStorage(services, faults), logs);
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);

        var first = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8, 8), "secret-name.png");
        var firstReference = first.Body.GetProperty("avatarReference").GetString()!;
        faults.FailDelete.Add(firstReference);
        var second = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 9, 9));

        Assert.AreEqual(HttpStatusCode.OK, second.Status);
        var secondReference = second.Body.GetProperty("avatarReference").GetString()!;
        Assert.AreEqual(secondReference, await ReadReferenceAsync(client, user.UserId));
        CollectionAssert.AreEquivalent(new[] { firstReference, secondReference }, env.StoredFiles());
        var warning = logs.Messages.Single(message => message.Contains("orphan", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(warning, firstReference);
        StringAssert.Contains(warning, user.UserId.ToString());
        AssertLogsAreSafe(logs, env);
    }

    [TestMethod]
    public async Task Persistence_failure_after_the_file_is_written_removes_the_new_file_and_keeps_the_profile()
    {
        using var env = new ImageEnvironment();
        var faults = new StorageFaults();
        var logs = new CapturingLoggerProvider();
        using var factory = await env.CreateFactoryAsync(services => InjectStorage(services, faults), logs);
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);
        var good = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8, 8));
        var goodReference = good.Body.GetProperty("avatarReference").GetString()!;

        faults.FailSave = true;
        var failed = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Jpeg, 8, 8), "secret-name.jpg");
        faults.FailSave = false;

        Assert.AreEqual(HttpStatusCode.InternalServerError, failed.Status);
        Assert.IsFalse(failed.Raw.Contains("secret-name", StringComparison.Ordinal));
        Assert.IsFalse(failed.Raw.Contains(env.Root, StringComparison.Ordinal));
        Assert.AreEqual(goodReference, await ReadReferenceAsync(client, user.UserId));
        CollectionAssert.AreEqual(new[] { goodReference }, env.StoredFiles());
        Assert.AreEqual(0, Directory.GetFiles(Path.Combine(env.Root, ".staging")).Length);
        AssertLogsAreSafe(logs, env);
    }

    [TestMethod]
    public async Task Storage_promotion_failure_leaves_no_staged_file_and_no_reference_change()
    {
        using var env = new ImageEnvironment();
        var faults = new StorageFaults();
        using var factory = await env.CreateFactoryAsync(services => InjectStorage(services, faults));
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);

        faults.FailPromote = true;
        var failed = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8, 8));

        Assert.AreEqual(HttpStatusCode.InternalServerError, failed.Status);
        Assert.IsNull(await ReadReferenceAsync(client, user.UserId));
        Assert.AreEqual(0, env.StoredFiles().Length);
        Assert.AreEqual(0, Directory.GetFiles(Path.Combine(env.Root, ".staging")).Length);
    }

    [TestMethod]
    public async Task Concurrent_replacements_converge_on_one_file_matching_the_profile()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
            UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8 + index, 8))));

        Assert.IsTrue(results.All(result => result.Status == HttpStatusCode.OK), string.Join(",", results.Select(result => result.Status)));
        var reference = await ReadReferenceAsync(client, user.UserId);
        CollectionAssert.AreEqual(new[] { reference }, env.StoredFiles(), $"profile={reference}; files={string.Join(",", env.StoredFiles())}");
        Assert.AreEqual(0, Directory.GetFiles(Path.Combine(env.Root, ".staging")).Length);
    }

    [TestMethod]
    public async Task Over_dimension_images_and_write_rate_limit_are_enforced()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);

        var tooWide = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 4097, 1));
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, tooWide.Status);
        Assert.AreEqual(0, env.StoredFiles().Length);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        HttpStatusCode last = default;
        for (var attempt = 0; attempt < 12 && last != HttpStatusCode.TooManyRequests; attempt++)
        {
            using var response = await client.DeleteAsync("/me/profile/avatar");
            last = response.StatusCode;
        }

        Assert.AreEqual(HttpStatusCode.TooManyRequests, last);
    }

    [TestMethod]
    public async Task Profile_locking_requires_a_transaction_and_returns_the_current_committed_profile()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);
        var upload = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8, 8));

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => users.GetByIdForUpdateAsync(user.UserId, CancellationToken.None));
        await using var transaction = await users.BeginTransactionAsync(CancellationToken.None);
        var locked = await users.GetByIdForUpdateAsync(user.UserId, CancellationToken.None);
        Assert.AreEqual(upload.Body.GetProperty("avatarReference").GetString(), locked!.Profile.AvatarReference);
        Assert.IsNull(await users.GetByIdForUpdateAsync(Guid.NewGuid(), CancellationToken.None));
        await transaction.RollbackAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task User_requests_cannot_set_the_avatar_and_profile_updates_preserve_it()
    {
        using var env = new ImageEnvironment();
        using var factory = await env.CreateFactoryAsync();
        using var client = factory.CreateClient();
        var user = await CreateLoginAsync(client);
        var upload = await UploadAsync(client, user.AccessToken, Encode(ProfileImageFormat.Png, 8, 8));
        var reference = upload.Body.GetProperty("avatarReference").GetString()!;

        using var attempt = await client.PutAsJsonAsync($"/users/{user.UserId}/profile", new
        {
            firstName = "New", lastName = "Name", displayName = "New Name", avatarReference = "0123456789abcdef0123456789abcdef.png"
        });
        Assert.AreEqual(HttpStatusCode.BadRequest, attempt.StatusCode);
        Assert.AreEqual(reference, await ReadReferenceAsync(client, user.UserId));

        using var update = await client.PutAsJsonAsync($"/users/{user.UserId}/profile", new { firstName = "New", lastName = "Name", displayName = "New Name" });
        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.AreEqual(reference, await ReadReferenceAsync(client, user.UserId), "A profile update must not clear the avatar.");
        CollectionAssert.AreEqual(new[] { reference }, env.StoredFiles());

        using var created = await client.PostAsJsonAsync("/users", new
        {
            email = $"noavatar-{Guid.NewGuid():N}@example.test", password = "Quickstart!2026", firstName = "A", lastName = "B", displayName = "AB",
            avatarReference = "0123456789abcdef0123456789abcdef.png"
        });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.IsNull(document.RootElement.GetProperty("profile").GetProperty("avatarReference").GetString());
    }

    private static void AssertLogsAreSafe(CapturingLoggerProvider logs, ImageEnvironment env)
    {
        foreach (var message in logs.Messages)
        {
            Assert.IsFalse(message.Contains(env.Root, StringComparison.Ordinal), "Logs must not contain storage paths.");
            Assert.IsFalse(message.Contains("secret-name", StringComparison.Ordinal), "Logs must not contain original filenames.");
            Assert.IsFalse(message.Contains("Quickstart!2026", StringComparison.Ordinal), "Logs must not contain credentials.");
            Assert.IsFalse(message.Contains("Content-Disposition: form-data", StringComparison.OrdinalIgnoreCase) || message.Contains("\u0089PNG", StringComparison.Ordinal), "Logs must not contain multipart bodies.");
        }
    }

    private static void InjectStorage(IServiceCollection services, StorageFaults faults)
    {
        services.RemoveAll<IProfileImageStorage>();
        services.AddSingleton<IProfileImageStorage>(provider => new FaultInjectingStorage(
            new LocalProfileImageStorage(provider.GetRequiredService<ProfileImagesOptions>(), provider.GetRequiredService<ILogger<LocalProfileImageStorage>>()),
            faults));
        services.RemoveAll<IUserRepository>();
        services.AddScoped<IUserRepository>(provider => new FaultInjectingUserRepository(
            ActivatorUtilities.CreateInstance<UserRepository>(provider), faults));
    }

    private sealed class StorageFaults
    {
        public bool FailPromote { get; set; }

        public volatile bool FailSave;

        public HashSet<string> FailDelete { get; } = [];
    }

    private sealed class FaultInjectingStorage(IProfileImageStorage inner, StorageFaults faults) : IProfileImageStorage
    {
        public Task<StagedProfileImage> StageAsync(ProcessedProfileImage image, CancellationToken cancellationToken) => inner.StageAsync(image, cancellationToken);

        public Task PromoteAsync(StagedProfileImage staged, CancellationToken cancellationToken) =>
            faults.FailPromote ? throw new IOException("injected promotion failure") : inner.PromoteAsync(staged, cancellationToken);

        public Task<bool> DiscardStagedAsync(StagedProfileImage staged, CancellationToken cancellationToken) => inner.DiscardStagedAsync(staged, cancellationToken);

        public Task<bool> DeleteAsync(ProfileImageReference reference, CancellationToken cancellationToken) =>
            faults.FailDelete.Contains(reference.Value) ? Task.FromResult(false) : inner.DeleteAsync(reference, cancellationToken);

        public Task<ProfileImageContent?> OpenReadAsync(ProfileImageReference reference, CancellationToken cancellationToken) => inner.OpenReadAsync(reference, cancellationToken);
    }

    private sealed class FaultInjectingUserRepository(IUserRepository inner, StorageFaults faults) : IUserRepository
    {
        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => inner.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        public Task AddAsync(User user, CancellationToken cancellationToken) => inner.AddAsync(user, cancellationToken);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => inner.GetByIdAsync(id, cancellationToken);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => inner.GetByIdForUpdateAsync(id, cancellationToken);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => inner.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            faults.FailSave ? throw new InvalidOperationException("injected persistence failure") : inner.SaveChangesAsync(cancellationToken);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => inner.TrySaveChangesAsync(cancellationToken);

        public Task<IUserRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => inner.BeginTransactionAsync(cancellationToken);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> messages = new();

        public IReadOnlyCollection<string> Messages => messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(System.Collections.Concurrent.ConcurrentQueue<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                sink.Enqueue(formatter(state, exception));
        }
    }

    private sealed record UploadOutcome(HttpStatusCode Status, JsonElement Body, string Raw);

    private sealed record LoginResult(Guid UserId, string AccessToken);

    private static async Task<UploadOutcome> UploadAsync(HttpClient client, string? token, byte[] bytes, string fileName = "photo.png", string contentType = "application/octet-stream")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/me/profile/avatar") { Content = content };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        JsonElement body = default;
        if (raw.StartsWith('{'))
        {
            var document = JsonDocument.Parse(raw);
            if (response.StatusCode == HttpStatusCode.OK) body = document.RootElement.Clone();
        }

        return new UploadOutcome(response.StatusCode, body, raw);
    }

    private static async Task<string?> ReadReferenceAsync(HttpClient client, Guid userId)
    {
        using var response = await client.GetAsync($"/users/{userId}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("profile").GetProperty("avatarReference").GetString();
    }

    private static async Task<LoginResult> CreateLoginAsync(HttpClient client)
    {
        const string password = "Quickstart!2026";
        var email = $"avatar-{Guid.NewGuid():N}@example.test";
        using var created = await client.PostAsJsonAsync("/users", new { email, password, firstName = "A", lastName = "B", displayName = "AB" });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var userId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var applicationCode = $"app-{Guid.NewGuid():N}";
        using var application = await client.PostAsJsonAsync("/applications", new { code = applicationCode, name = "Application" });
        var applicationId = JsonDocument.Parse(await application.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        using var membership = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        using var login = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
        return new LoginResult(userId, token);
    }

    private sealed class ImageEnvironment : IDisposable
    {
        private readonly string? previous = Environment.GetEnvironmentVariable("ProfileImages__RootPath");

        public ImageEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), "gaussauth-avatar-tests-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("ProfileImages__RootPath", Root);
        }

        public string Root { get; }

        public async Task<WebApplicationFactory<Program>> CreateFactoryAsync(Action<IServiceCollection>? configure = null, ILoggerProvider? logs = null)
        {
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services => configure?.Invoke(services));
                if (logs is not null) builder.ConfigureServices(services => services.AddLogging(logging => logging.AddProvider(logs)));
            });
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
            return factory;
        }

        public string[] StoredFiles() => Directory.Exists(Path.Combine(Root, "avatars"))
            ? Directory.GetFiles(Path.Combine(Root, "avatars")).Select(Path.GetFileName).Where(name => ProfileImageReference.TryParse(name, out _)).OrderBy(name => name).ToArray()!
            : [];

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("ProfileImages__RootPath", previous);
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private static ProfileImagesOptions Load(Dictionary<string, string?> values, bool production)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = new FakeEnvironment(production ? "Production" : "Development");
        return ProfileImagesOptions.Load(configuration, environment);
    }

    private sealed class FakeEnvironment(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static byte[] Encode(ProfileImageFormat format, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(10, 120, 200));
        using var image = SKImage.FromBitmap(bitmap);
        var skiaFormat = format switch
        {
            ProfileImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ProfileImageFormat.Png => SKEncodedImageFormat.Png,
            _ => SKEncodedImageFormat.Webp
        };
        using var data = image.Encode(skiaFormat, 90);
        return data.ToArray();
    }

    private static byte[] WithExifSegment(byte[] jpeg, string text)
    {
        var payload = System.Text.Encoding.ASCII.GetBytes("Exif\0\0" + text);
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF) }.Concat(payload);
        return jpeg[..2].Concat(segment).Concat(jpeg[2..]).ToArray();
    }

    private static ProfileImageFormat? DetectFormat(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return codec?.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => ProfileImageFormat.Jpeg,
            SKEncodedImageFormat.Png => ProfileImageFormat.Png,
            SKEncodedImageFormat.Webp => ProfileImageFormat.WebP,
            _ => null
        };
    }

    private static bool Contains(byte[] haystack, ReadOnlySpan<byte> needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
