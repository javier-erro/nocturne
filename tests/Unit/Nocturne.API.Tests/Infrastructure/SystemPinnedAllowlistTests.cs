using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.API.Tests.Infrastructure;

/// <summary>
/// <see cref="INocturneDbContextFactory.CreateSystemPinnedAsync"/> reads as no reader, past every
/// share and history narrowing, so it is limited to the files listed here; its remarks give the
/// reason. A new caller is added here deliberately or not at all.
/// </summary>
[Trait("Category", "Unit")]
public class SystemPinnedAllowlistTests
{
    private const string Method = nameof(INocturneDbContextFactory.CreateSystemPinnedAsync);

    private static readonly string[] ScannedTrees = ["src"];

    private static readonly string[] Allowed =
    [
        "src/API/Nocturne.API/Middleware/TenantSetupMiddleware.cs",
        "src/API/Nocturne.API/Controllers/Authentication/PasskeyController.cs",
        "src/API/Nocturne.API/Services/Identity/InstanceSetupState.cs",
        "src/API/Nocturne.API/Services/Auth/PublicAccessCacheService.cs",
    ];

    private static readonly IReadOnlyList<string> Callers = Scan();

    [Fact]
    public void OnlyTheAllowlistCallsTheSystemPin()
    {
        Callers.Except(Allowed).Should().BeEmpty(
            $"{Method} bypasses share and history narrowing; allowlist a new caller here deliberately");
    }

    [Fact]
    public void EveryAllowlistEntryStillCallsIt()
    {
        Allowed.Except(Callers).Should().BeEmpty("a stale entry would silently cover a future call in that file");
    }

    [Theory]
    [InlineData("class C { async Task M(INocturneDbContextFactory f) => await f.CreateSystemPinnedAsync(id); }", true)]
    [InlineData("class C { async Task M(INocturneDbContextFactory? f) => await f?.CreateSystemPinnedAsync(id); }", true)]
    [InlineData("class C { Func<Guid, CancellationToken, ValueTask<NocturneDbContext>> M(INocturneDbContextFactory f) => f.CreateSystemPinnedAsync; }", true)]
    [InlineData("class C { void M() => Use(CreateSystemPinnedAsync); }", true)]
    [InlineData("class C { async Task M(INocturneDbContextFactory f) => await f.CreateForTenantAsync(id); }", false)]
    public void DetectsACall(string source, bool expected) =>
        Calls(CSharpSyntaxTree.ParseText(source)).Should().Be(expected);

    /// <summary>
    /// Any mention of the method by name: a call, a conditional call (<c>f?.Method(...)</c>) or a
    /// method group passed on.
    /// </summary>
    private static bool Calls(SyntaxTree syntax) =>
        syntax.GetRoot()
            .DescendantNodes()
            .Any(node => node switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == Method,
                MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == Method,
                IdentifierNameSyntax name => name.Identifier.ValueText == Method
                    && name.Parent is not (MemberAccessExpressionSyntax or MemberBindingExpressionSyntax),
                _ => false,
            });

    private static List<string> Scan()
    {
        var root = RepositoryRoot();
        var callers = new List<string>();

        foreach (var tree in ScannedTrees)
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, tree), "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;

                var text = File.ReadAllText(path);
                if (!text.Contains(Method, StringComparison.Ordinal))
                    continue;

                if (Calls(CSharpSyntaxTree.ParseText(text)))
                    callers.Add(Path.GetRelativePath(root, path).Replace('\\', '/'));
            }
        }

        return callers;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "tests", "Unit")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"No tests/Unit directory above {AppContext.BaseDirectory}.");
    }
}
