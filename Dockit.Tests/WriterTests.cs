using Dockit.Internal;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dockit.Tests;

[TestFixture]
public sealed class WriterTests
{
    [Test]
    public async Task WriteMarkdownAsync_writes_assembly_metadata_and_namespace_index()
    {
        var markdown = await RenderMarkdownAsync();

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("# Dockit.TestAssembly assembly"));
            Assert.That(markdown, Does.Contain("| `AssemblyVersion` | &quot;1.2.3.4&quot; |"));
            Assert.That(markdown, Does.Contain("| `AssemblyFileVersion` | &quot;4.3.2.1&quot; |"));
            Assert.That(markdown, Does.Contain("| `AssemblyInformationalVersion` | &quot;1.2.3-test+metadata&quot; |"));
            Assert.That(markdown, Does.Contain("| [ `Fixture.Root` ](#fixture.root-namespace) |"));
            Assert.That(markdown, Does.Contain("| [ `Fixture.Secondary` ](#fixture.secondary-namespace) |"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_writes_generic_type_sections_and_xml_comment_content()
    {
        var markdown = await RenderMarkdownAsync();

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("### GenericSample<TItem,TValue> class"));
            Assert.That(markdown, Does.Contain("Represents a generic sample type."));
            Assert.That(markdown, Does.Contain("| `TItem` | Primary item type. |"));
            Assert.That(markdown, Does.Contain("| `TValue` | Secondary value type. |"));
            Assert.That(markdown, Does.Contain("Type remarks with"));
            Assert.That(markdown, Does.Contain("var sample = new GenericSample<int, string>();"));

            Assert.That(markdown, Does.Contain("#### Constructor"));
            Assert.That(markdown, Does.Contain("Initializes a new instance."));

            Assert.That(markdown, Does.Contain("#### this[int index] indexer"));
            Assert.That(markdown, Does.Contain("Gets or sets an indexed item."));

            Assert.That(markdown, Does.Contain("#### Transform() method"));
            Assert.That(markdown, Does.Contain("| `TResult` | Result type. |"));
            Assert.That(markdown, Does.Contain("| `item` | Item parameter. |"));
            Assert.That(markdown, Does.Contain("| `values` | Values parameter. |"));
            Assert.That(markdown, Does.Contain("| Transformation result. |"));

            Assert.That(markdown, Does.Contain("#### Extend() extension method"));
            Assert.That(markdown, Does.Contain("Extends a sample."));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_writes_event_indexes_and_enum_tables()
    {
        var markdown = await RenderMarkdownAsync();

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("|Event| [ `Changed` ]("));
            Assert.That(markdown, Does.Contain("|Event| [ `VisibleEvent` ]("));
            Assert.That(markdown, Does.Contain("### SampleState enum"));
            Assert.That(markdown, Does.Contain("|Enum value|Description|"));
            Assert.That(markdown, Does.Contain("| `Started` | Started state. Used while processing is active. |"));
            Assert.That(markdown, Does.Contain("### Transformer<TInput,TOutput> delegate"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_returns_without_output_when_assembly_name_does_not_match()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var document = await FixtureArtifacts.LoadDocumentFromStringAsync(
            """
            <doc>
              <assembly>
                <name>Other.Assembly</name>
              </assembly>
              <members />
            </doc>
            """);

        var path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"writer-mismatch-{Guid.NewGuid():N}.md");

        try
        {
            await Writer.WriteMarkdownAsync(path, assembly, document, 1, CancellationToken.None);
            Assert.That(File.Exists(path), Is.False);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static async Task<string> RenderMarkdownAsync()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var document = await FixtureArtifacts.LoadDocumentAsync();

        var path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"writer-{Guid.NewGuid():N}.md");

        try
        {
            await Writer.WriteMarkdownAsync(path, assembly, document, 1, CancellationToken.None);
            return await File.ReadAllTextAsync(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
