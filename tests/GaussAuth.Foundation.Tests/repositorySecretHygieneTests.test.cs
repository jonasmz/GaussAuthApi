namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class RepositorySecretHygieneTests
{
    [TestMethod]
    public void Tracked_configuration_files_contain_no_private_key_or_non_placeholder_connection_password()
    {
        var root = FindRoot();
        var candidates = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains("/.git/") && (path.EndsWith(".json") || path.EndsWith(".yml") || path.EndsWith(".yaml") ||
                Path.GetFileName(path).StartsWith(".env") || Path.GetFileName(path).Contains("compose", StringComparison.OrdinalIgnoreCase)));
        foreach (var path in candidates)
        {
            var text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal), Path.GetRelativePath(root, path));
            var password = System.Text.RegularExpressions.Regex.Match(text, "(?:Host|Server)=[^;\\s]+.*(?:Password|Pwd)=[^;\\s]+");
            if (password.Success)
                Assert.IsTrue(password.Value.Contains("replace_with_", StringComparison.OrdinalIgnoreCase) || password.Value.Contains("not_a_secret", StringComparison.OrdinalIgnoreCase) || password.Value.Contains("${", StringComparison.Ordinal), Path.GetRelativePath(root, path));
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory.Parent is not null && !File.Exists(Path.Combine(directory.FullName, "GaussAuth.slnx"))) directory = directory.Parent;
        return directory.FullName;
    }
}
