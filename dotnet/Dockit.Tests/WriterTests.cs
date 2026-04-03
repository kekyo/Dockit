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
        var result = await RenderMarkdownAsync();
        var markdown = result.Markdown;
        var markdownFileName = result.MarkdownFileName;

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("# Dockit.TestAssembly assembly"));
            Assert.That(markdown, Does.Contain("| `AssemblyVersion` | &quot;1.2.3.4&quot; |"));
            Assert.That(markdown, Does.Contain("| `AssemblyFileVersion` | &quot;4.3.2.1&quot; |"));
            Assert.That(markdown, Does.Contain("| `AssemblyInformationalVersion` | &quot;1.2.3-test+metadata&quot; |"));
            Assert.That(markdown, Does.Contain($"| [ `Fixture.Root` ](./{markdownFileName}#fixture.root-namespace) |"));
            Assert.That(markdown, Does.Contain($"| [ `Fixture.Secondary` ](./{markdownFileName}#fixture.secondary-namespace) |"));
            Assert.That(markdown, Does.Contain("<a id=\"fixture.root-namespace\"></a>"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_writes_generic_type_sections_and_xml_comment_content()
    {
        var result = await RenderMarkdownAsync();
        var markdown = result.Markdown;
        var markdownFileName = result.MarkdownFileName;

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("### GenericSample&lt;TItem,TValue&gt; class"));
            Assert.That(markdown, Does.Contain("<a id=\"genericsampletitemtvalue-class\"></a>"));
            Assert.That(markdown, Does.Contain("Represents a generic sample type."));
            Assert.That(markdown, Does.Contain("| `TItem` | Primary item type. |"));
            Assert.That(markdown, Does.Contain("| `TValue` | Secondary value type. |"));
            Assert.That(markdown, Does.Contain($"Type remarks with [VisibilityContainer](./{markdownFileName}#visibilitycontainer-class)"));
            Assert.That(markdown, Does.Contain("var sample = new GenericSample<int, string>();"));
            Assert.That(markdown, Does.Contain($"See also: [VisibilityContainer](./{markdownFileName}#visibilitycontainer-class)"));

            Assert.That(markdown, Does.Contain("#### Constructor"));
            Assert.That(markdown, Does.Contain("Initializes a new instance."));

            Assert.That(markdown, Does.Contain("#### this[int] indexer"));
            Assert.That(markdown, Does.Contain("Gets or sets an indexed item."));

            Assert.That(markdown, Does.Contain("#### Transform&lt;TResult&gt;() method"));
            Assert.That(markdown, Does.Contain("| `TResult` | Result type. |"));
            Assert.That(markdown, Does.Contain("| `item` | Item parameter. |"));
            Assert.That(markdown, Does.Contain("| `values` | Values parameter. |"));
            Assert.That(markdown, Does.Contain("| Transformation result. |"));
            Assert.That(markdown, Does.Contain($"See also: [Name](./{markdownFileName}#name-property)"));
            Assert.That(markdown, Does.Contain("#### HandleMatrix() method"));
            Assert.That(markdown, Does.Contain("int[,] matrix"));

            Assert.That(markdown, Does.Contain("Converts a sample to a string."));
            Assert.That(markdown, Does.Contain("Raises the changed event."));
            Assert.That(markdown, Does.Contain("#### Extend&lt;TItem,TValue,TResult&gt;() extension method"));
            Assert.That(markdown, Does.Contain("Extends a sample."));
            Assert.That(markdown, Does.Contain("### BufferSlice ref struct"));
            Assert.That(markdown, Does.Contain("public readonly ref struct BufferSlice"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_writes_event_indexes_and_enum_tables()
    {
        var result = await RenderMarkdownAsync();
        var markdown = result.Markdown;
        var markdownFileName = result.MarkdownFileName;

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain($"|Event| [ `Changed` ](./{markdownFileName}#changed-event)"));
            Assert.That(markdown, Does.Contain($"|Event| [ `VisibleEvent` ](./{markdownFileName}#visibleevent-event)"));
            Assert.That(markdown, Does.Contain("### SampleState enum"));
            Assert.That(markdown, Does.Contain("|Enum value|Description|"));
            Assert.That(markdown, Does.Contain("| `Started` | Started state. Used while processing is active. |"));
            Assert.That(markdown, Does.Contain("### Transformer&lt;TInput,TOutput&gt; delegate"));
            Assert.That(markdown, Does.Contain("| `TInput` | Delegate input type. |"));
            Assert.That(markdown, Does.Contain("| `TOutput` | Delegate output type. |"));
            Assert.That(markdown, Does.Contain("Returns text."));
            Assert.That(markdown, Does.Contain("| Static text. |"));
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

    private static async Task<(string Markdown, string MarkdownFileName)> RenderMarkdownAsync()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var document = await FixtureArtifacts.LoadDocumentAsync();

        var directoryPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"writer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, "Dockit.TestAssembly.md");

        try
        {
            await Writer.WriteMarkdownAsync(path, assembly, document, 1, CancellationToken.None);
            return (await File.ReadAllTextAsync(path), Path.GetFileName(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
    }
}
