using Dockit.Internal;
using Mono.Cecil;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace Dockit.Tests;

[TestFixture]
public sealed class DotNetXmlNamingTests
{
    [Test]
    public async Task GetDotNetXmlName_matches_compiler_xml_for_generic_and_nested_types()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var document = await FixtureArtifacts.LoadDocumentAsync();

        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var nestedType = FixtureArtifacts.GetNestedType(assembly, "Fixture.Root", "Outer`1", "Inner`1");

        Assert.Multiple(() =>
        {
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(genericType),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Type, "Represents a generic sample type.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(nestedType),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Type, "Represents a nested generic type.")));
        });
    }

    [Test]
    public async Task GetDotNetXmlName_matches_compiler_xml_for_members_with_generic_arguments_and_special_parameters()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var document = await FixtureArtifacts.LoadDocumentAsync();

        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var visibilityType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "VisibilityContainer");
        var extensionType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSampleExtensions");

        var constructor = genericType.Methods.Single(method => method.IsConstructor && !method.IsStatic);
        var indexer = genericType.Properties.Single(property => property.Name == "Item");
        var changedEvent = genericType.Events.Single(@event => @event.Name == "Changed");
        var transformMethod = genericType.Methods.Single(method => method.Name == "Transform");
        var consumeReferencesMethod = genericType.Methods.Single(method => method.Name == "ConsumeReferences");
        var rewriteMapMethod = genericType.Methods.Single(method => method.Name == "RewriteMap");
        var handleMatrixMethod = genericType.Methods.Single(method => method.Name == "HandleMatrix");
        var usePointerMethod = genericType.Methods.Single(method => method.Name == "UsePointer");
        var acceptVarArgsMethod = visibilityType.Methods.Single(method =>
            method.Name == "AcceptVarArgs" &&
            method.CallingConvention == MethodCallingConvention.VarArg);
        var implicitOperatorMethod = genericType.Methods.Single(method => method.Name == "op_Implicit");
        var onChangedMethod = genericType.Methods.Single(method => method.Name == "OnChanged");
        var extensionMethod = extensionType.Methods.Single(method => method.Name == "Extend");
        var echoMethod = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Secondary", "SecondaryType").
            Methods.Single(method => method.Name == "Echo");

        Assert.Multiple(() =>
        {
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(constructor),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Initializes a new instance.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(indexer),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Property, "Gets or sets an indexed item.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(changedEvent),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Event, "Raised when the sample changes.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(transformMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Transforms the supplied data.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(consumeReferencesMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Consumes references.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(rewriteMapMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Rewrites a map.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(handleMatrixMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Handles a matrix.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(usePointerMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Uses a pointer.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(acceptVarArgsMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Consumes variable arguments.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(implicitOperatorMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Converts a sample to a string.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(onChangedMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Raises the changed event.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(extensionMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Extends a sample.")));
            Assert.That(
                DotNetXmlNaming.GetDotNetXmlName(echoMethod),
                Is.EqualTo(FixtureArtifacts.GetXmlNameBySummary(document, DotNetXmlMemberTypes.Method, "Returns text.")));
        });
    }
}
