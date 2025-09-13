using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace RichMove.SmartPay.Core.Tests.Contracts;

#pragma warning disable CA1707

public class AudEurContractsTests
{
    private static readonly Regex Iso4217 = new("^[A-Z]{3}$");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "DOCS")))
            dir = dir.Parent;
        if (dir == null) throw new DirectoryNotFoundException("Could not locate repo root (missing DOCS folder).");
        return dir.FullName;
    }

    [Fact]
    public void FxQuoteRequest_AUD_EUR_Example_IsValidShape()
    {
        var path = Path.Combine(RepoRoot(), "DOCS", "API", "examples", "FxQuoteRequest.AUD_EUR.example.json");
        Assert.True(File.Exists(path), $"Missing example file: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("baseCurrency", out var baseCcy));
        Assert.True(root.TryGetProperty("quoteCurrency", out var quoteCcy));
        Assert.True(root.TryGetProperty("amount", out var amount));

        Assert.Matches(Iso4217, baseCcy.GetString() ?? string.Empty);
        Assert.Matches(Iso4217, quoteCcy.GetString() ?? string.Empty);
        Assert.Equal("AUD", baseCcy.GetString());
        Assert.Equal("EUR", quoteCcy.GetString());
        Assert.True(amount.GetDouble() > 0);
    }

    [Fact]
    public void FxQuoteResult_AUD_EUR_Example_IsValidShape()
    {
        var path = Path.Combine(RepoRoot(), "DOCS", "API", "examples", "FxQuoteResult.AUD_EUR.example.json");
        Assert.True(File.Exists(path), $"Missing example file: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        string[] required = { "rate", "baseCurrency", "quoteCurrency", "amount", "expiresAtUtc" };
        foreach (var key in required)
            Assert.True(root.TryGetProperty(key, out _), $"Missing property {key}");

        Assert.Equal("AUD", root.GetProperty("baseCurrency").GetString());
        Assert.Equal("EUR", root.GetProperty("quoteCurrency").GetString());
        Assert.Matches(Iso4217, root.GetProperty("baseCurrency").GetString() ?? string.Empty);
        Assert.Matches(Iso4217, root.GetProperty("quoteCurrency").GetString() ?? string.Empty);

        var rate = root.GetProperty("rate").GetDouble();
        var amount = root.GetProperty("amount").GetDouble();
        Assert.True(rate >= 0);
        Assert.True(amount >= 0);
    }
}

#pragma warning restore CA1707