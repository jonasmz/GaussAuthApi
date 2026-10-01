using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ArchitectureTests
{
    [TestMethod]
    public void Inner_projects_keep_the_required_dependency_direction()
    {
        var root = FindRepositoryRoot();
        var domainProject = XDocument.Load(Path.Combine(root, "src/GaussAuth.Domain/GaussAuth.Domain.csproj"));
        var applicationProject = XDocument.Load(Path.Combine(root, "src/GaussAuth.Application/GaussAuth.Application.csproj"));

        Assert.IsEmpty(domainProject.Descendants("ProjectReference"));
        Assert.IsEmpty(domainProject.Descendants("PackageReference"));

        var applicationReferences = applicationProject.Descendants("ProjectReference")
            .Select(reference => Path.GetFileName((string?)reference.Attribute("Include")))
            .ToArray();
        CollectionAssert.AreEquivalent(new[] { "GaussAuth.Domain.csproj" }, applicationReferences);

        var applicationPackages = applicationProject.Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .ToArray();
        CollectionAssert.AreEquivalent(new[] { "Microsoft.Extensions.Logging.Abstractions" }, applicationPackages);

        var forbidden = new[]
        {
            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql",
            "GaussAuth.Infrastructure", "GaussAuth.Api", "Docker"
        };
        foreach (var name in new[] { "GaussAuth.Domain", "GaussAuth.Application" })
        {
            var references = Assembly.Load(name).GetReferencedAssemblies()
                .Select(assembly => assembly.Name ?? string.Empty);
            foreach (var reference in references)
            {
                Assert.IsFalse(forbidden.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)),
                    $"{name} has forbidden reference {reference}.");
            }
        }
    }

    [TestMethod]
    public void Source_files_follow_the_one_type_and_category_filename_rules()
    {
        var root = FindRepositoryRoot();
        var typeDeclaration = new Regex(
            @"(?m)^\s*(?:(?:public|internal|file|sealed|static|partial|abstract|readonly)\s+)*(?:class|interface|record|enum|struct|delegate)\s+[A-Za-z_]\w*",
            RegexOptions.CultureInvariant);
        var filename = new Regex(@"^[^.]+\.[A-Za-z]+\.cs$", RegexOptions.CultureInvariant);

        foreach (var tree in new[] { "src", "tests" })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, tree), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path);
                if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
                {
                    continue;
                }

                if (relative == "src/GaussAuth.Api/Program.cs")
                {
                    Assert.AreEqual(0, typeDeclaration.Matches(File.ReadAllText(path)).Count);
                    continue;
                }

                Assert.IsTrue(filename.IsMatch(Path.GetFileName(path)), $"Invalid source filename: {relative}");
                Assert.AreEqual(1, typeDeclaration.Matches(File.ReadAllText(path)).Count,
                    $"Expected one declared type in {relative}");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GaussAuth.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the solution root.");
    }
}
