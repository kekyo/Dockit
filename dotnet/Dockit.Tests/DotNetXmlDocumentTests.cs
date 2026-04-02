using Dockit.Internal;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Dockit.Tests;

[TestFixture]
public sealed class DotNetXmlDocumentTests
{
    [Test]
    public async Task LoadAsync_parses_all_supported_member_kinds_and_xml_elements()
    {
        const string xml = """
            <doc>
              <assembly>
                <name>Sample.Assembly</name>
              </assembly>
              <members>
                <member name="N:Sample.Namespace">
                  <summary>Namespace summary.</summary>
                  <remarks>Namespace remarks.</remarks>
                </member>
                <member name="T:Sample.Namespace.Container`1">
                  <summary>Type summary.</summary>
                  <typeparam name="T">Type parameter summary.</typeparam>
                  <remarks>Type remarks.</remarks>
                  <example><code>var value = 1;</code></example>
                  <seealso cref="T:Sample.Namespace.Other">Other type</seealso>
                </member>
                <member name="F:Sample.Namespace.Container`1.Field">
                  <summary>Field summary.</summary>
                </member>
                <member name="P:Sample.Namespace.Container`1.Value">
                  <summary>Property summary.</summary>
                  <value>Property value summary.</value>
                </member>
                <member name="E:Sample.Namespace.Container`1.Changed">
                  <summary>Event summary.</summary>
                </member>
                <member name="M:Sample.Namespace.Container`1.Transform``1(`0,System.String)">
                  <summary>Method summary.</summary>
                  <typeparam name="TResult">Result type summary.</typeparam>
                  <param name="item">Item parameter summary.</param>
                  <param name="text">Text parameter summary.</param>
                  <returns>Return summary.</returns>
                  <remarks>Method remarks.</remarks>
                  <example><code>return text;</code></example>
                  <seealso cref="P:Sample.Namespace.Container`1.Value">Value property</seealso>
                </member>
              </members>
            </doc>
            """;

        var document = await FixtureArtifacts.LoadDocumentFromStringAsync(xml);

        var namespaceMember = document.Members[new(DotNetXmlMemberTypes.Namespace, "Sample.Namespace")];
        var typeMember = document.Members[new(DotNetXmlMemberTypes.Type, "Sample.Namespace.Container`1")];
        var fieldMember = document.Members[new(DotNetXmlMemberTypes.Field, "Sample.Namespace.Container`1.Field")];
        var propertyMember = document.Members[new(DotNetXmlMemberTypes.Property, "Sample.Namespace.Container`1.Value")];
        var eventMember = document.Members[new(DotNetXmlMemberTypes.Event, "Sample.Namespace.Container`1.Changed")];
        var methodMember = document.Members[new(DotNetXmlMemberTypes.Method, "Sample.Namespace.Container`1.Transform``1(`0,System.String)")];

        Assert.Multiple(() =>
        {
            Assert.That(document.AssemblyName, Is.EqualTo("Sample.Assembly"));
            Assert.That(document.Members, Has.Count.EqualTo(6));

            Assert.That(FixtureArtifacts.Normalize(namespaceMember.Summary?.Value), Is.EqualTo("Namespace summary."));
            Assert.That(FixtureArtifacts.Normalize(namespaceMember.Remarks?.Value), Is.EqualTo("Namespace remarks."));

            Assert.That(FixtureArtifacts.Normalize(typeMember.Summary?.Value), Is.EqualTo("Type summary."));
            Assert.That(typeMember.TypeParameters, Has.Length.EqualTo(1));
            Assert.That(typeMember.TypeParameters[0].Name, Is.EqualTo("T"));
            Assert.That(FixtureArtifacts.Normalize(typeMember.TypeParameters[0].Description?.Value), Is.EqualTo("Type parameter summary."));
            Assert.That(FixtureArtifacts.Normalize(typeMember.Remarks?.Value), Is.EqualTo("Type remarks."));
            Assert.That(FixtureArtifacts.Normalize(typeMember.Example?.Value), Is.EqualTo("var value = 1;"));
            Assert.That(typeMember.SeeAlso?.Attribute("cref")?.Value, Is.EqualTo("T:Sample.Namespace.Other"));

            Assert.That(FixtureArtifacts.Normalize(fieldMember.Summary?.Value), Is.EqualTo("Field summary."));

            Assert.That(FixtureArtifacts.Normalize(propertyMember.Summary?.Value), Is.EqualTo("Property summary."));
            Assert.That(FixtureArtifacts.Normalize(propertyMember.Returns?.Value), Is.EqualTo("Property value summary."));

            Assert.That(FixtureArtifacts.Normalize(eventMember.Summary?.Value), Is.EqualTo("Event summary."));

            Assert.That(FixtureArtifacts.Normalize(methodMember.Summary?.Value), Is.EqualTo("Method summary."));
            Assert.That(methodMember.TypeParameters, Has.Length.EqualTo(1));
            Assert.That(methodMember.TypeParameters[0].Name, Is.EqualTo("TResult"));
            Assert.That(FixtureArtifacts.Normalize(methodMember.TypeParameters[0].Description?.Value), Is.EqualTo("Result type summary."));
            Assert.That(methodMember.Parameters, Has.Length.EqualTo(2));
            Assert.That(methodMember.Parameters[0].Name, Is.EqualTo("item"));
            Assert.That(FixtureArtifacts.Normalize(methodMember.Parameters[0].Description?.Value), Is.EqualTo("Item parameter summary."));
            Assert.That(methodMember.Parameters[1].Name, Is.EqualTo("text"));
            Assert.That(FixtureArtifacts.Normalize(methodMember.Parameters[1].Description?.Value), Is.EqualTo("Text parameter summary."));
            Assert.That(FixtureArtifacts.Normalize(methodMember.Returns?.Value), Is.EqualTo("Return summary."));
            Assert.That(FixtureArtifacts.Normalize(methodMember.Remarks?.Value), Is.EqualTo("Method remarks."));
            Assert.That(FixtureArtifacts.Normalize(methodMember.Example?.Value), Is.EqualTo("return text;"));
            Assert.That(methodMember.SeeAlso?.Attribute("cref")?.Value, Is.EqualTo("P:Sample.Namespace.Container`1.Value"));
        });
    }
}
