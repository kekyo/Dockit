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
            new Dictionary<string, string>
            {
                ["Fixture.Root.VisibilityContainer"] = "visibilitycontainer-class",
            },
            "fixture.md");

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Does.Contain("Intro &amp; detail"));
            Assert.That(rendered, Does.Contain("Paragraph with"));
            Assert.That(rendered, Does.Contain("`inline`"));
            Assert.That(rendered, Does.Contain("[VisibilityContainer](./fixture.md#visibilitycontainer-class)"));
            Assert.That(rendered, Does.Contain("```csharp"));
            Assert.That(rendered, Does.Contain("if (value < 1)"));
            Assert.That(rendered, Does.Contain("<custom attr=\"1\">custom text</custom>"));
        });
    }

    [Test]
    public void RenderReference_renders_local_cref_with_a_simplified_label()
    {
        var element = XElement.Parse("""<seealso cref="P:Fixture.Root.GenericSample`2.Name" />""");

        var rendered = WriterUtilities.RenderReference(
            element,
            new Dictionary<string, string>
            {
                ["Fixture.Root.GenericSample`2.Name"] = "name-property",
            },
            "fixture.md");

        Assert.That(rendered, Is.EqualTo(" [Name](./fixture.md#name-property) "));
    }

    [Test]
    public void RenderReference_resolves_implicit_operator_cref_to_local_anchor()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var implicitOperator = genericType.Methods.Single(method => method.Name == "op_Implicit");
        var identities = WriterUtilities.GeneratePandocFormedHashReferenceIdentities(assembly);

        var rendered = WriterUtilities.RenderReference(
            XElement.Parse("""<seealso cref="M:Fixture.Root.GenericSample`2.op_Implicit(Fixture.Root.GenericSample{`0,`1})~System.String" />"""),
            identities,
            "fixture.md");

        Assert.That(
            rendered,
            Is.EqualTo($" [op_Implicit](./fixture.md#{identities[DotNetXmlNaming.GetDotNetXmlName(implicitOperator)]}) "));
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
            Assert.That(overloadIdentities.Distinct().ToArray(), Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void GetGenericConstraintClauses_formats_type_and_method_constraints()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var constrainedType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "ConstrainedContainer`1");
        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var constrainedMethod = genericType.Methods.Single(method => method.Name == "CreateConstrained");

        Assert.Multiple(() =>
        {
            Assert.That(
                WriterUtilities.GetGenericConstraintClauses(constrainedType),
                Is.EqualTo(new[] { "where TValue : BaseType, IMarker, new()" }));
            Assert.That(
                WriterUtilities.GetGenericConstraintClauses(constrainedMethod),
                Is.EqualTo(new[] { "where TResult : BaseType, IMarker, new()" }));
        });
    }
}
