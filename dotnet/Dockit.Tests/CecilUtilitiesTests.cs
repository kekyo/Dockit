using Dockit.Internal;
using NUnit.Framework;
using System.Linq;

namespace Dockit.Tests;

[TestFixture]
public sealed class CecilUtilitiesTests
{
    [Test]
    public void GetVisibleMembers_filters_non_public_and_editor_browsable_members()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var type = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "VisibilityContainer");

        var fieldNames = CecilUtilities.GetFields(type).Select(field => field.Name).ToArray();
        var propertyNames = CecilUtilities.GetProperties(type).Select(property => property.Name).ToArray();
        var eventNames = CecilUtilities.GetEvents(type).Select(@event => @event.Name).ToArray();
        var methodNames = CecilUtilities.GetMethods(type).
            Where(method => !method.IsConstructor && !method.IsSpecialName).
            Select(method => method.Name).
            ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(fieldNames, Is.EquivalentTo(new[] { "ProtectedField", "VisibleField" }));
            Assert.That(propertyNames, Is.EquivalentTo(new[] { "ProtectedProperty", "VisibleProperty" }));
            Assert.That(eventNames, Is.EquivalentTo(new[] { "VisibleEvent" }));
            Assert.That(methodNames, Is.EquivalentTo(new[] { "OnVisibleEvent", "ProtectedMethod", "VisibleMethod" }));
        });
    }

    [Test]
    public void Recognizes_special_member_shapes_and_type_kinds()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();

        var genericType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSample`2");
        var delegateType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "Transformer`2");
        var enumType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "SampleState");
        var refStructType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "BufferSlice");
        var recordClassType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "NameRecord");
        var recordStructType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "ValueRecord");
        var extensionType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSampleExtensions");

        var indexer = genericType.Properties.Single(property => property.Name == "Item");
        var extensionMethod = extensionType.Methods.Single(method => method.Name == "Extend");
        var referenceMethod = genericType.Methods.Single(method => method.Name == "ConsumeReferences");

        Assert.Multiple(() =>
        {
            Assert.That(CecilUtilities.IsIndexer(indexer), Is.True);
            Assert.That(CecilUtilities.IsDelegateType(delegateType), Is.True);
            Assert.That(CecilUtilities.IsEnumType(enumType), Is.True);
            Assert.That(CecilUtilities.IsRefStructType(refStructType), Is.True);
            Assert.That(CecilUtilities.IsRecordClassType(recordClassType), Is.True);
            Assert.That(CecilUtilities.IsRecordStructType(recordStructType), Is.True);
            Assert.That(CecilUtilities.IsExtensionMethod(extensionMethod), Is.True);
            Assert.That(CecilUtilities.GetTypeKeywordString(refStructType), Is.EqualTo("ref struct"));
            Assert.That(CecilUtilities.GetTypeKeywordString(recordClassType), Is.EqualTo("record"));
            Assert.That(CecilUtilities.GetTypeKeywordString(recordStructType), Is.EqualTo("record struct"));

            Assert.That(referenceMethod.Parameters[0].Name, Is.EqualTo("item"));
            Assert.That(CecilUtilities.GetParameterModifier(referenceMethod.Parameters[0]), Is.EqualTo(ParameterModifierCandidates.In));
            Assert.That(CecilUtilities.GetParameterModifier(referenceMethod.Parameters[1]), Is.EqualTo(ParameterModifierCandidates.Out));
            Assert.That(CecilUtilities.GetParameterModifier(referenceMethod.Parameters[2]), Is.EqualTo(ParameterModifierCandidates.Ref));

            Assert.That(genericType.GenericParameters.Select(parameter => parameter.Name), Is.EqualTo(new[] { "TItem", "TValue" }));
            Assert.That(genericType.Methods.Single(method => method.Name == "Transform").GenericParameters.Select(parameter => parameter.Name), Is.EqualTo(new[] { "TResult" }));
        });
    }
}
