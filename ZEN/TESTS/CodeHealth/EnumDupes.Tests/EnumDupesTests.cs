using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using Xunit;

namespace RichMove.SmartPay.CodeHealth.Tests;

public sealed class EnumDupesTests
{
    private static readonly string[] _excludedDirs = new[]
    {
        "bin", "obj", ".git", ".github", "Migrations", "ANALYZERS"
    };

    [Fact]
    public async Task NoDuplicateEnumNamesAcrossRepository()
    {
        // Locate repo root heuristically (works in CI and local).
        var start = AppContext.BaseDirectory;
        var root = AscendUntil(start, d => Directory.Exists(Path.Combine(d, "ZEN")));
        root.Should().NotBeNull("tests must run from within the repository tree");
        var zen = Path.Combine(root!, "ZEN");
        var sourceRoot = Path.Combine(zen, "SOURCE");
        Directory.Exists(sourceRoot).Should().BeTrue($"expected folder at {sourceRoot}");

        var allowlist = await LoadAllowlistAsync();

        var enumMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in EnumerateCsFiles(sourceRoot))
        {
            var text = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(text);
            var rootNode = await tree.GetRootAsync().ConfigureAwait(true);
            var enums = rootNode.DescendantNodes().OfType<EnumDeclarationSyntax>();
            foreach (var en in enums)
            {
                var name = en.Identifier.Text;
                if (!enumMap.TryGetValue(name, out var list))
                    enumMap[name] = list = new List<string>();
                list.Add(file);
            }
        }

        var dupes = enumMap
            .Where(kv => kv.Value.Count > 1 && !allowlist.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (dupes.Count > 0)
        {
            var report = string.Join(Environment.NewLine, dupes.Select(kv =>
                $"  {kv.Key} -> {string.Join(", ", kv.Value.Select(Shorten))}"));
            Assert.Fail("Duplicate enum names detected (disallowed):" + Environment.NewLine + report);
        }
    }

    private static IEnumerable<string> EnumerateCsFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (_excludedDirs.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                stack.Push(sub);
            }
            foreach (var cs in Directory.EnumerateFiles(dir, "*.cs"))
                yield return cs;
        }
    }

    private static string? AscendUntil(string start, Func<string, bool> predicate, int max = 10)
    {
        var dir = new DirectoryInfo(start);
        for (int i = 0; i < max && dir != null; i++, dir = dir.Parent)
        {
            if (predicate(dir.FullName)) return dir.FullName;
        }
        return null;
    }

    private static async Task<HashSet<string>> LoadAllowlistAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "EnumDupes.Allowlist.json");
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.Ordinal);
        using var s = File.OpenRead(path);
        var doc = await JsonDocument.ParseAsync(s).ConfigureAwait(true);
        if (!doc.RootElement.TryGetProperty("Allowed", out var arr))
            return new HashSet<string>(StringComparer.Ordinal);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String) set.Add(item.GetString()!);
        }
        return set;
    }

    private static string Shorten(string path)
    {
        try
        {
            var zenIdx = path.IndexOf("ZEN", StringComparison.OrdinalIgnoreCase);
            return zenIdx >= 0 ? path[zenIdx..].Replace('\\', '/') : Path.GetFileName(path);
        }
        catch (ArgumentException) { return path; }
        catch (IndexOutOfRangeException) { return path; }
    }
}