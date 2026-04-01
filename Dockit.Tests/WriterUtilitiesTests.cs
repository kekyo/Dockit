using Dockit.Internal;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dockit.Tests;

[TestFixture]
public sealed class WriterUtilitiesTests
{
    [Test]
    public void RenderDotNetXmlElement_renders_supported_elements_and_escapes_text()
    {
        var element = XElement.Parse(
            """
            <remarks>
              Intro &amp; detail
              <para>Paragraph with <c>inline</c> and <see cref="T:Fixture.Root.VisibilityContainer">VisibilityContainer</see>.</para>
              <code>
                if (value &lt; 1)
                {
                    return;
                }
              </code>
              <custom attr="1">custom text</custom>
            </remarks>
            """);

        var rendered = WriterUtilities.RenderDotNetXmlElement(
            element,
            false,
            new Dictionary<string, string>());

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Does.Contain("Intro &amp; detail"));
            Assert.That(rendered, Does.Contain("Paragraph with"));
            Assert.That(rendered, Does.Contain("`inline`"));
            Assert.That(rendered, Does.Contain("[VisibilityContainer](T:Fixture.Root.VisibilityContainer)"));
            Assert.That(rendered, Does.Contain("```csharp"));
            Assert.That(rendered, Does.Contain("if (value < 1)"));
            Assert.That(rendered, Does.Contain("<custom attr=\"1\">custom text</custom>"));
        });
    }

    [Test]
    public void GeneratePandocFormedHashReferenceIdentities_creates_entries_for_xml_names_and_unique_overloads()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var overloads = genericType.Methods.Where(method => method.Name == "Overload").ToArray();

        var identities = WriterUtilities.GeneratePandocFormedHashReferenceIdentities(assembly);
        var overloadIdentities = overloads.
            Select(method => identities[FullNaming.GetFullName(method)]).
            ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(identities, Contains.Key(FullNaming.GetFullName(genericType)));
            Assert.That(identities, Contains.Key(DotNetXmlNaming.GetDotNetXmlName(genericType)));
            Assert.That(overloadIdentities.Distinct(), Has.Count.EqualTo(2));
        });
    }
}
