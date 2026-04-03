using Dockit.Internal;
using Mono.Cecil;
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
            Assert.That(rendered, Does.Contain("[VisibilityContainer](#visibilitycontainer-class)"));
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

        Assert.That(rendered, Is.EqualTo(" [Name](#name-property) "));
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
            Is.EqualTo($" [implicit operator string](#{identities[DotNetXmlNaming.GetDotNetXmlName(implicitOperator)]}) "));
    }

    [Test]
    public void RenderReference_resolves_binary_operator_cref_to_local_anchor()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var additionOperator = genericType.Methods.Single(method => method.Name == "op_Addition");
        var identities = WriterUtilities.GeneratePandocFormedHashReferenceIdentities(assembly);

        var rendered = WriterUtilities.RenderReference(
            new XElement(
                "seealso",
                new XAttribute("cref", $"M:{DotNetXmlNaming.GetDotNetXmlName(additionOperator)}")),
            identities,
            "fixture.md");

        Assert.That(
            rendered,
            Is.EqualTo($" [operator +](#{identities[DotNetXmlNaming.GetDotNetXmlName(additionOperator)]}) "));
    }

    [Test]
    public void GeneratePandocFormedHashReferenceIdentities_creates_entries_for_xml_names_and_unique_overloads()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var visibilityType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "VisibilityContainer");
        var overloads = genericType.Methods.Where(method => method.Name == "Overload").ToArray();
        var varArgOverloads = visibilityType.Methods.Where(method => method.Name == "AcceptVarArgs").ToArray();

        var identities = WriterUtilities.GeneratePandocFormedHashReferenceIdentities(assembly);
        var overloadIdentities = overloads.
            Select(method => identities[FullNaming.GetFullSignaturedName(method)]).
            ToArray();
        var varArgOverloadIdentities = varArgOverloads.
            Select(method => identities[FullNaming.GetFullSignaturedName(method)]).
            ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(identities, Contains.Key(FullNaming.GetFullName(genericType)));
            Assert.That(identities, Contains.Key(DotNetXmlNaming.GetDotNetXmlName(genericType)));
            Assert.That(overloadIdentities.Distinct().ToArray(), Has.Length.EqualTo(2));
            Assert.That(varArgOverloadIdentities.Distinct().ToArray(), Has.Length.EqualTo(2));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(varArgOverloads.Single(method => method.CallingConvention == MethodCallingConvention.VarArg)),
                Does.EndWith("AcceptVarArgs(System.Int32,)"));
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

    [Test]
    public void GetCustomAttributeDeclarationWithTarget_inserts_return_target()
    {
        Assert.That(
            WriterUtilities.GetCustomAttributeDeclarationWithTarget("[MaybeNull]", "return"),
            Is.EqualTo("[return: MaybeNull]"));
    }

    [Test]
    public void GetPrettyPrintValue_formats_enum_values_symbolically()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var enumType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "SampleState");

        Assert.That(
            WriterUtilities.GetPrettyPrintValue(1, enumType),
            Is.EqualTo("SampleState.Started"));
    }
}
