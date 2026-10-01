using Microsoft.Extensions.Configuration;
using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Domain.Users;
using GaussAuth.Infrastructure.ProfileImages;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

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
        var processor = new ImageSharpProfileImageProcessor(SmallLimits);

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
        var processor = new ImageSharpProfileImageProcessor(SmallLimits);
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        var truncated = Encode(ProfileImageFormat.Png, 32, 32)[..40];

        Assert.AreEqual(ProfileImageRejection.UnsupportedType, (await processor.ProcessAsync(new MemoryStream(svg), CancellationToken.None)).Rejection);
        Assert.AreEqual(ProfileImageRejection.UnsupportedType, (await processor.ProcessAsync(new MemoryStream("GIF89a not really"u8.ToArray()), CancellationToken.None)).Rejection);
        Assert.AreEqual(ProfileImageRejection.Empty, (await processor.ProcessAsync(new MemoryStream(), CancellationToken.None)).Rejection);
        Assert.IsFalse((await processor.ProcessAsync(new MemoryStream(truncated), CancellationToken.None)).IsSuccess);
    }

    [TestMethod]
    public async Task Gif_is_rejected_even_though_the_decoder_supports_it()
    {
        using var image = new Image<Rgba32>(8, 8);
        using var buffer = new MemoryStream();
        await image.SaveAsGifAsync(buffer);
        var processor = new ImageSharpProfileImageProcessor(SmallLimits);

        var result = await processor.ProcessAsync(new MemoryStream(buffer.ToArray()), CancellationToken.None);

        Assert.AreEqual(ProfileImageRejection.UnsupportedType, result.Rejection);
    }

    [TestMethod]
    public async Task Configured_byte_and_dimension_limits_are_enforced()
    {
        var processor = new ImageSharpProfileImageProcessor(new ProfileImageLimits(2048, 64));

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
        var processor = new ImageSharpProfileImageProcessor(SmallLimits);
        using var image = new Image<Rgb24>(16, 16);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Software, "secret-tool-name");
        image.Metadata.ExifProfile.SetValue(ExifTag.Artist, "private-person");
        using var buffer = new MemoryStream();
        await image.SaveAsync(buffer, new JpegEncoder());
        var original = buffer.ToArray();
        Assert.IsTrue(Contains(original, "private-person"u8), "Fixture must carry metadata.");

        var result = await processor.ProcessAsync(new MemoryStream(original), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(Contains(result.Image!.Content, "private-person"u8));
        Assert.IsFalse(Contains(result.Image.Content, "secret-tool-name"u8));
        using var reloaded = Image.Load(result.Image.Content);
        Assert.IsNull(reloaded.Metadata.ExifProfile);
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
        var profile = UserProfile.Create(Guid.NewGuid(), "A", "B", "AB", null, null, now);
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
        var forbidden = new[] { "SixLabors", "System.IO.File", "System.IO.Directory", "FileStream", "Microsoft.AspNetCore", "IFormFile" };
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
        using var image = new Image<Rgb24>(width, height, new Rgb24(10, 120, 200));
        using var buffer = new MemoryStream();
        switch (format)
        {
            case ProfileImageFormat.Jpeg: image.Save(buffer, new JpegEncoder()); break;
            case ProfileImageFormat.Png: image.Save(buffer, new PngEncoder()); break;
            default: image.Save(buffer, new WebpEncoder()); break;
        }

        return buffer.ToArray();
    }

    private static ProfileImageFormat? DetectFormat(byte[] bytes) => Image.DetectFormat(bytes).Name switch
    {
        "JPEG" => ProfileImageFormat.Jpeg,
        "PNG" => ProfileImageFormat.Png,
        "WEBP" => ProfileImageFormat.WebP,
        _ => null
    };

    private static bool Contains(byte[] haystack, ReadOnlySpan<byte> needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
