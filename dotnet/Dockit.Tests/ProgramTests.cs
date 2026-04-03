using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Dockit.Tests;

[TestFixture]
public sealed class ProgramTests
{
    [Test]
    public async Task RunAsync_returns_error_and_usage_when_required_arguments_are_missing()
    {
        using var outputWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        var exitCode = await Program.RunAsync(
            Array.Empty<string>(),
            outputWriter,
            errorWriter);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(outputWriter.ToString(), Is.EqualTo(string.Empty));
            Assert.That(errorWriter.ToString(), Does.Contain("Expected <assembly-path> and <output-directory>."));
            Assert.That(errorWriter.ToString(), Does.Contain("Usage: dockit-dotnet [options] <assembly-path> <output-directory>"));
            Assert.That(errorWriter.ToString(), Does.Contain("--help"));
        });
    }

    [Test]
    public async Task RunAsync_returns_success_and_usage_when_help_is_requested()
    {
        using var outputWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        var exitCode = await Program.RunAsync(
            new[] { "--help" },
            outputWriter,
            errorWriter);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(errorWriter.ToString(), Is.EqualTo(string.Empty));
            Assert.That(outputWriter.ToString(), Does.Contain("Usage: dockit-dotnet [options] <assembly-path> <output-directory>"));
            Assert.That(outputWriter.ToString(), Does.Contain("--initial-level"));
        });
    }

    [Test]
    public async Task RunAsync_parses_initial_level_option_and_generates_markdown()
    {
        using var outputWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        var outputDirectory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"program-{Guid.NewGuid():N}");

        Directory.CreateDirectory(outputDirectory);
        try
        {
            var exitCode = await Program.RunAsync(
                new[]
                {
                    "--initial-level=2",
                    FixtureArtifacts.AssemblyPath,
                    outputDirectory,
                },
                outputWriter,
                errorWriter);

            var markdownPath = Path.Combine(
                outputDirectory,
                Path.GetFileNameWithoutExtension(FixtureArtifacts.AssemblyPath) + ".md");
            var markdown = await File.ReadAllTextAsync(markdownPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(
                    outputWriter.ToString(),
                    Does.Contain($"Input assembly: {Path.GetFullPath(FixtureArtifacts.AssemblyPath)}"));
                Assert.That(
                    outputWriter.ToString(),
                    Does.Contain($"Input XML: {Path.ChangeExtension(Path.GetFullPath(FixtureArtifacts.AssemblyPath), ".xml")}"));
                Assert.That(
                    outputWriter.ToString(),
                    Does.Contain($"Output markdown: {markdownPath}"));
                Assert.That(
                    outputWriter.ToString(),
                    Does.Match(@"(?m)^Elapsed time: \d+\.\d{3} ms$"));
                Assert.That(errorWriter.ToString(), Is.EqualTo(string.Empty));
                Assert.That(markdown, Does.StartWith("## Dockit.TestAssembly assembly"));
            });
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true);
            }
        }
    }

    [Test]
    public async Task RunAsync_returns_error_when_initial_level_is_less_than_one()
    {
        using var outputWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        var exitCode = await Program.RunAsync(
            new[]
            {
                "--initial-level=0",
                FixtureArtifacts.AssemblyPath,
                TestContext.CurrentContext.WorkDirectory,
            },
            outputWriter,
            errorWriter);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(outputWriter.ToString(), Is.EqualTo(string.Empty));
            Assert.That(errorWriter.ToString(), Does.Contain("Initial level must be 1 or greater."));
            Assert.That(errorWriter.ToString(), Does.Contain("Usage: dockit-dotnet [options] <assembly-path> <output-directory>"));
        });
    }
}
