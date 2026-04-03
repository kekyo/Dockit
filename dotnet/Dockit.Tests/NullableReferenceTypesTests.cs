using Dockit.Internal;
using NUnit.Framework;
using System.Linq;

namespace Dockit.Tests;

[TestFixture]
public sealed class NullableReferenceTypesTests
{
    [Test]
    public void GetName_formats_nullable_reference_type_members()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var nullableType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "NullableContainer");
        var property = nullableType.Properties.Single(candidate => candidate.Name == "OptionalName");
        var method = nullableType.Methods.Single(candidate => candidate.Name == "CreateNullableMap");

        Assert.Multiple(() =>
        {
            Assert.That(
                NullableReferenceTypes.GetName(
                    property.PropertyType,
                    NullableReferenceTypes.CreatePropertyContext(property)),
                Is.EqualTo("string?"));
            Assert.That(
                NullableReferenceTypes.GetName(
                    method.ReturnType,
                    NullableReferenceTypes.CreateMethodReturnContext(method)),
                Is.EqualTo("Dictionary<string,string?>?"));
            Assert.That(
                NullableReferenceTypes.GetName(
                    method.Parameters[0].ParameterType,
                    NullableReferenceTypes.CreateParameterContext(method, method.Parameters[0]),
                    CecilUtilities.GetParameterModifier(method.Parameters[0])),
                Is.EqualTo("string?"));
            Assert.That(
                NullableReferenceTypes.GetName(
                    method.Parameters[1].ParameterType,
                    NullableReferenceTypes.CreateParameterContext(method, method.Parameters[1]),
                    CecilUtilities.GetParameterModifier(method.Parameters[1])),
                Is.EqualTo("List<string?>?"));
        });
    }
}
