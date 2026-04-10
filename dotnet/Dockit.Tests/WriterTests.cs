using Dockit.Internal;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
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

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("# Dockit.TestAssembly assembly"));
            Assert.That(markdown, Does.Contain("| `AssemblyVersion` | &quot;1.2.3.4&quot; |"));
            Assert.That(markdown, Does.Contain("| `AssemblyFileVersion` | &quot;4.3.2.1&quot; |"));
            Assert.That(markdown, Does.Contain("| `AssemblyInformationalVersion` | &quot;1.2.3-test+metadata&quot; |"));
            Assert.That(markdown, Does.Contain("[CLSCompliant(false)]"));
            Assert.That(markdown, Does.Contain("| [ `Fixture.Root` ](#fixture.root-namespace) |"));
            Assert.That(markdown, Does.Contain("| [ `Fixture.Secondary` ](#fixture.secondary-namespace) |"));
            Assert.That(markdown, Does.Not.Contain("<a id=\""));
            Assert.That(
                markdown,
                Does.Contain(
                    $"<a name=\"fixture.root-namespace\"></a>{Environment.NewLine}{Environment.NewLine}## Fixture.Root namespace"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_omits_assembly_attribute_code_block_when_disabled()
    {
        var result = await RenderMarkdownAsync(
            DocumentationVisibilityOptions.Default,
            false,
            true);
        var markdown = result.Markdown;

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("| `AssemblyVersion` | &quot;1.2.3.4&quot; |"));
            Assert.That(markdown, Does.Contain("| `AssemblyInformationalVersion` | &quot;1.2.3-test+metadata&quot; |"));
            Assert.That(markdown, Does.Not.Contain("[CLSCompliant(false)]"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_omits_hash_links_when_disabled()
    {
        var result = await RenderMarkdownAsync(
            DocumentationVisibilityOptions.Default,
            true,
            false);
        var markdown = result.Markdown;

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Not.Contain("](#"));
            Assert.That(markdown, Does.Contain("| `Fixture.Root` |"));
            Assert.That(markdown, Does.Contain("|Event| `Changed` |"));
            Assert.That(markdown, Does.Contain("|Event| `VisibleEvent` |"));
            Assert.That(markdown, Does.Contain("Type remarks with VisibilityContainer"));
            Assert.That(markdown, Does.Contain("See also: VisibilityContainer"));
            Assert.That(markdown, Does.Contain("See also: Name"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_writes_generic_type_sections_and_xml_comment_content()
    {
        var result = await RenderMarkdownAsync();
        var markdown = result.Markdown;
        using var assembly = FixtureArtifacts.ReadAssembly();
        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var additionOperator = genericType.Methods.Single(method => method.Name == "op_Addition");
        var identities = WriterUtilities.GeneratePandocFormedHashReferenceIdentities(assembly);
        var additionOperatorAnchor = identities[DotNetXmlNaming.GetDotNetXmlName(additionOperator)];

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("### GenericSample&lt;TItem,TValue&gt; class"));
            Assert.That(
                markdown,
                Does.Contain(
                    $"<a name=\"genericsampletitemtvalue-class\"></a>{Environment.NewLine}{Environment.NewLine}### GenericSample&lt;TItem,TValue&gt; class"));
            Assert.That(markdown, Does.Contain("Represents a generic sample type."));
            Assert.That(markdown, Does.Contain("| `TItem` | Primary item type. |"));
            Assert.That(markdown, Does.Contain("| `TValue` | Secondary value type. |"));
            Assert.That(markdown, Does.Contain("Type remarks with [VisibilityContainer](#visibilitycontainer-class)"));
            Assert.That(markdown, Does.Contain("var sample = new GenericSample<int, string>();"));
            Assert.That(markdown, Does.Contain("See also: [VisibilityContainer](#visibilitycontainer-class)"));
            Assert.That(markdown, Does.Contain("public const SampleState InitialState = SampleState.Started;"));

            Assert.That(markdown, Does.Contain("#### Constructor"));
            Assert.That(markdown, Does.Contain("Initializes a new instance."));

            Assert.That(markdown, Does.Contain("#### this[int] indexer"));
            Assert.That(markdown, Does.Contain("Gets or sets an indexed item."));

            Assert.That(markdown, Does.Contain("#### Transform&lt;TResult&gt;() method"));
            Assert.That(markdown, Does.Contain("| `TResult` | Result type. |"));
            Assert.That(markdown, Does.Contain("| `item` | Item parameter. |"));
            Assert.That(markdown, Does.Contain("| `values` | Values parameter. |"));
            Assert.That(markdown, Does.Contain("| Transformation result. |"));
            Assert.That(markdown, Does.Contain("See also: [Name](#name-property)"));
            Assert.That(markdown, Does.Contain("#### CreateConstrained&lt;TResult&gt;() method"));
            Assert.That(markdown, Does.Contain("where TResult : BaseType, IMarker, new();"));
            Assert.That(markdown, Does.Contain("#### HandleMatrix() method"));
            Assert.That(markdown, Does.Contain("int[,] matrix"));
            Assert.That(markdown, Does.Contain("#### AcceptVarArgs() method"));
            Assert.That(markdown, Does.Contain("Consumes variable arguments."));
            Assert.That(markdown, Does.Contain("    int value,"));
            Assert.That(markdown, Does.Contain("    __arglist);"));
            Assert.That(markdown, Does.Contain("#### operator +() method"));
            Assert.That(markdown, Does.Contain("Combines two samples."));
            Assert.That(markdown, Does.Contain("public static GenericSample<TItem,TValue> operator +("));

            Assert.That(markdown, Does.Contain("Converts a sample to a string."));
            Assert.That(markdown, Does.Contain($"See also: [operator +](#{additionOperatorAnchor})"));
            Assert.That(markdown, Does.Contain("Raises the changed event."));
            Assert.That(markdown, Does.Contain("#### Extend&lt;TItem,TValue,TResult&gt;() extension method"));
            Assert.That(markdown, Does.Contain("Extends a sample."));
            Assert.That(markdown, Does.Contain("public string Name"));
            Assert.That(markdown, Does.Contain("    get;"));
            Assert.That(markdown, Does.Contain("    protected set;"));
            Assert.That(markdown, Does.Not.Contain("    public get;"));
            Assert.That(markdown, Does.Contain("public event EventHandler? Changed;"));
            Assert.That(markdown, Does.Contain("public event EventHandler? VisibleEvent;"));
            Assert.That(markdown, Does.Not.Contain("    public add;"));
            Assert.That(markdown, Does.Not.Contain("    public remove;"));
            Assert.That(markdown, Does.Not.Contain("void?"));
            Assert.That(markdown, Does.Contain("### BufferSlice ref struct"));
            Assert.That(markdown, Does.Contain("public readonly ref struct BufferSlice"));
            Assert.That(markdown, Does.Contain("### NameRecord record"));
            Assert.That(markdown, Does.Contain("public record NameRecord"));
            Assert.That(markdown, Does.Contain("### ValueRecord record struct"));
            Assert.That(markdown, Does.Contain("public readonly record struct ValueRecord"));
            Assert.That(markdown, Does.Contain("### NativeMethods class"));
            Assert.That(markdown, Does.Contain("public static extern bool MessageBeep("));
            Assert.That(markdown, Does.Contain("### ConstrainedContainer&lt;TValue&gt; class"));
            Assert.That(markdown, Does.Contain("where TValue : BaseType, IMarker, new()"));
            Assert.That(markdown, Does.Contain("### NullableContainer class"));
            Assert.That(markdown, Does.Contain("string? OptionalName"));
            Assert.That(markdown, Does.Contain("    get;"));
            Assert.That(markdown, Does.Contain("    set;"));
            Assert.That(markdown, Does.Contain("Dictionary<string,string?>? CreateNullableMap("));
            Assert.That(markdown, Does.Contain("string? prefix"));
            Assert.That(markdown, Does.Contain("List<string?>? values"));
            Assert.That(markdown, Does.Contain("[return: MaybeNull]"));
            Assert.That(markdown, Does.Contain("string ReturnMaybeNull("));
            Assert.That(markdown, Does.Contain("[EditorBrowsable(EditorBrowsableState.Always)]"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_writes_event_indexes_and_enum_tables()
    {
        var result = await RenderMarkdownAsync();
        var markdown = result.Markdown;

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("|Event| [ `Changed` ](#changed-event)"));
            Assert.That(markdown, Does.Contain("|Event| [ `VisibleEvent` ](#visibleevent-event)"));
            Assert.That(markdown, Does.Contain("### SampleState enum"));
            Assert.That(markdown, Does.Contain("|Enum value|Description|"));
            Assert.That(markdown, Does.Contain("| `Started` | Started state. Used while processing is active. |"));
            Assert.That(markdown, Does.Contain("### Transformer&lt;TInput,TOutput&gt; delegate"));
            Assert.That(markdown, Does.Contain("| `TInput` | Delegate input type. |"));
            Assert.That(markdown, Does.Contain("| `TOutput` | Delegate output type. |"));
            Assert.That(markdown, Does.Contain("[MethodImpl(MethodImplOptions.AggressiveInlining)]"));
            Assert.That(markdown, Does.Not.Contain("MethodImplOptions.PreserveSig"));
            Assert.That(markdown, Does.Contain("Returns text."));
            Assert.That(markdown, Does.Contain("| Static text. |"));
        });
    }

    [Test]
    public async Task WriteMarkdownAsync_honors_visibility_options()
    {
        var internalResult = await RenderMarkdownAsync(
            new(
                DocumentationAccessibility.Internal,
                DocumentationEditorBrowsableVisibility.Advanced));
        var normalResult = await RenderMarkdownAsync(
            new(
                DocumentationAccessibility.Internal,
                DocumentationEditorBrowsableVisibility.Normal));
        var alwaysResult = await RenderMarkdownAsync(
            new(
                DocumentationAccessibility.Internal,
                DocumentationEditorBrowsableVisibility.Always));

        Assert.Multiple(() =>
        {
            Assert.That(internalResult.Markdown, Does.Contain("### InternalOnlyType class"));
            Assert.That(internalResult.Markdown, Does.Contain("#### HiddenMethod() method"));
            Assert.That(internalResult.Markdown, Does.Not.Contain("HiddenByEditorBrowsableField"));
            Assert.That(internalResult.Markdown, Does.Contain("AdvancedEditorBrowsableField"));

            Assert.That(normalResult.Markdown, Does.Not.Contain("AdvancedEditorBrowsableField"));

            Assert.That(alwaysResult.Markdown, Does.Contain("#### HiddenByEditorBrowsableField field"));
            Assert.That(alwaysResult.Markdown, Does.Contain("internal int HiddenField"));
            Assert.That(alwaysResult.Markdown, Does.Contain("| [ `InternalOnlyType` ](#internalonlytype-class) |"));
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

    private static Task<(string Markdown, string MarkdownFileName)> RenderMarkdownAsync() =>
        RenderMarkdownAsync(DocumentationVisibilityOptions.Default, true, true);

    private static Task<(string Markdown, string MarkdownFileName)> RenderMarkdownAsync(
        DocumentationVisibilityOptions visibilityOptions) =>
        RenderMarkdownAsync(visibilityOptions, true, true);

    private static async Task<(string Markdown, string MarkdownFileName)> RenderMarkdownAsync(
        DocumentationVisibilityOptions visibilityOptions,
        bool includeAssemblyAttributes,
        bool includeHashLinks)
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
            await Writer.WriteMarkdownAsync(
                path,
                assembly,
                document,
                1,
                visibilityOptions,
                includeAssemblyAttributes,
                includeHashLinks,
                CancellationToken.None);
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
