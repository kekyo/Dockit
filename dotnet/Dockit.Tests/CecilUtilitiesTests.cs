using Dockit.Internal;
using Mono.Cecil;
using NUnit.Framework;
using System;
using System.ComponentModel;
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
            Assert.That(fieldNames, Is.EquivalentTo(new[] { "AdvancedEditorBrowsableField", "ProtectedField", "VisibleField" }));
            Assert.That(propertyNames, Is.EquivalentTo(new[] { "ProtectedProperty", "VisibleProperty" }));
            Assert.That(eventNames, Is.EquivalentTo(new[] { "VisibleEvent" }));
            Assert.That(methodNames, Is.EquivalentTo(new[] { "AcceptVarArgs", "AcceptVarArgs", "OnVisibleEvent", "ProtectedMethod", "VisibleMethod" }));
        });
    }

    [Test]
    public void GetVisibleMembers_honors_accessibility_thresholds()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();
        var type = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "VisibilityContainer");

        var internalOptions = new DocumentationVisibilityOptions(
            DocumentationAccessibility.Internal,
            DocumentationEditorBrowsableVisibility.Advanced);
        var privateProtectedOptions = new DocumentationVisibilityOptions(
            DocumentationAccessibility.PrivateProtected,
            DocumentationEditorBrowsableVisibility.Advanced);
        var privateOptions = new DocumentationVisibilityOptions(
            DocumentationAccessibility.Private,
            DocumentationEditorBrowsableVisibility.Advanced);

        var internalFieldNames = CecilUtilities.GetFields(type, internalOptions).Select(field => field.Name).ToArray();
        var internalPropertyNames = CecilUtilities.GetProperties(type, internalOptions).Select(property => property.Name).ToArray();
        var internalEventNames = CecilUtilities.GetEvents(type, internalOptions).Select(@event => @event.Name).ToArray();
        var internalMethodNames = CecilUtilities.GetMethods(type, internalOptions).
            Where(method => !method.IsConstructor && !method.IsSpecialName).
            Select(method => method.Name).
            ToArray();

        var privateProtectedFieldNames = CecilUtilities.GetFields(type, privateProtectedOptions).Select(field => field.Name).ToArray();
        var privateProtectedPropertyNames = CecilUtilities.GetProperties(type, privateProtectedOptions).Select(property => property.Name).ToArray();
        var privateProtectedEventNames = CecilUtilities.GetEvents(type, privateProtectedOptions).Select(@event => @event.Name).ToArray();
        var privateProtectedMethodNames = CecilUtilities.GetMethods(type, privateProtectedOptions).
            Where(method => !method.IsConstructor && !method.IsSpecialName).
            Select(method => method.Name).
            ToArray();

        var privateFieldNames = CecilUtilities.GetFields(type, privateOptions).Select(field => field.Name).ToArray();
        var privatePropertyNames = CecilUtilities.GetProperties(type, privateOptions).Select(property => property.Name).ToArray();
        var privateEventNames = CecilUtilities.GetEvents(type, privateOptions).Select(@event => @event.Name).ToArray();
        var privateMethodNames = CecilUtilities.GetMethods(type, privateOptions).
            Where(method => !method.IsConstructor && !method.IsSpecialName).
            Select(method => method.Name).
            ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(internalFieldNames, Is.EquivalentTo(new[] { "AdvancedEditorBrowsableField", "HiddenField", "ProtectedField", "VisibleField" }));
            Assert.That(internalPropertyNames, Is.EquivalentTo(new[] { "HiddenProperty", "ProtectedProperty", "VisibleProperty" }));
            Assert.That(internalEventNames, Is.EquivalentTo(new[] { "HiddenEvent", "VisibleEvent" }));
            Assert.That(internalMethodNames, Is.EquivalentTo(new[] { "AcceptVarArgs", "AcceptVarArgs", "HiddenMethod", "OnHiddenEvent", "OnVisibleEvent", "ProtectedMethod", "VisibleMethod" }));

            Assert.That(privateProtectedFieldNames, Is.EquivalentTo(new[] { "AdvancedEditorBrowsableField", "HiddenField", "PrivateProtectedField", "ProtectedField", "VisibleField" }));
            Assert.That(privateProtectedPropertyNames, Is.EquivalentTo(new[] { "HiddenProperty", "PrivateProtectedProperty", "ProtectedProperty", "VisibleProperty" }));
            Assert.That(privateProtectedEventNames, Is.EquivalentTo(new[] { "HiddenEvent", "PrivateProtectedEvent", "VisibleEvent" }));
            Assert.That(privateProtectedMethodNames, Is.EquivalentTo(new[] { "AcceptVarArgs", "AcceptVarArgs", "HiddenMethod", "OnHiddenEvent", "OnPrivateProtectedEvent", "OnVisibleEvent", "PrivateProtectedMethod", "ProtectedMethod", "VisibleMethod" }));

            Assert.That(privateFieldNames, Is.EquivalentTo(new[] { "AdvancedEditorBrowsableField", "HiddenField", "PrivateField", "PrivateProtectedField", "ProtectedField", "VisibleField" }));
            Assert.That(privatePropertyNames, Is.EquivalentTo(new[] { "HiddenProperty", "PrivateProperty", "PrivateProtectedProperty", "ProtectedProperty", "VisibleProperty" }));
            Assert.That(privateEventNames, Is.EquivalentTo(new[] { "HiddenEvent", "PrivateEvent", "PrivateProtectedEvent", "VisibleEvent" }));
            Assert.That(privateMethodNames, Is.EquivalentTo(new[] { "AcceptVarArgs", "AcceptVarArgs", "HiddenMethod", "OnHiddenEvent", "OnPrivateEvent", "OnPrivateProtectedEvent", "OnVisibleEvent", "PrivateMethod", "PrivateProtectedMethod", "ProtectedMethod", "VisibleMethod" }));
        });
    }

    [Test]
    public void GetTypes_honors_internal_accessibility_threshold()
    {
        using var assembly = FixtureArtifacts.ReadAssembly();

        var defaultTypeNames = CecilUtilities.GetTypes(assembly.MainModule).Select(type => type.Name).ToArray();
        var internalTypeNames = CecilUtilities.GetTypes(
            assembly.MainModule,
            new(
                DocumentationAccessibility.Internal,
                DocumentationEditorBrowsableVisibility.Advanced)).
            Select(type => type.Name).
            ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(defaultTypeNames, Does.Not.Contain("InternalOnlyType"));
            Assert.That(internalTypeNames, Does.Contain("InternalOnlyType"));
        });
    }

    [Test]
    public void EditorBrowsable_visibility_falls_back_from_member_to_type_module_and_assembly()
    {
        using var assembly = CreateEditorBrowsableAssembly(
            EditorBrowsableState.Never,
            EditorBrowsableState.Advanced);
        var module = assembly.MainModule;

        var moduleInheritedType = AddPublicType(module, "ModuleInheritedType");
        var moduleInheritedField = AddPublicField(moduleInheritedType, "ModuleInheritedField");

        var typeNeverContainer = AddPublicType(module, "TypeNeverContainer");
        AddEditorBrowsable(typeNeverContainer, module, EditorBrowsableState.Never);
        var inheritedNeverField = AddPublicField(typeNeverContainer, "InheritedNeverField");
        var alwaysField = AddPublicField(typeNeverContainer, "AlwaysField");
        AddEditorBrowsable(alwaysField, module, EditorBrowsableState.Always);

        var typeAlwaysContainer = AddPublicType(module, "TypeAlwaysContainer");
        AddEditorBrowsable(typeAlwaysContainer, module, EditorBrowsableState.Always);
        var inheritedAlwaysField = AddPublicField(typeAlwaysContainer, "InheritedAlwaysField");

        var normalOptions = new DocumentationVisibilityOptions(
            DocumentationAccessibility.Protected,
            DocumentationEditorBrowsableVisibility.Normal);
        var advancedOptions = new DocumentationVisibilityOptions(
            DocumentationAccessibility.Protected,
            DocumentationEditorBrowsableVisibility.Advanced);

        Assert.Multiple(() =>
        {
            Assert.That(CecilUtilities.IsVisible(moduleInheritedType, normalOptions), Is.False);
            Assert.That(CecilUtilities.IsVisible(moduleInheritedType, advancedOptions), Is.True);
            Assert.That(CecilUtilities.IsVisible(moduleInheritedField, normalOptions), Is.False);
            Assert.That(CecilUtilities.IsVisible(moduleInheritedField, advancedOptions), Is.True);

            Assert.That(CecilUtilities.IsVisible(typeNeverContainer, advancedOptions), Is.False);
            Assert.That(CecilUtilities.IsVisible(inheritedNeverField, advancedOptions), Is.False);
            Assert.That(CecilUtilities.IsVisible(alwaysField, normalOptions), Is.True);

            Assert.That(CecilUtilities.IsVisible(typeAlwaysContainer, normalOptions), Is.True);
            Assert.That(CecilUtilities.IsVisible(inheritedAlwaysField, normalOptions), Is.True);
        });
    }

    [Test]
    public void EditorBrowsable_visibility_falls_back_to_assembly_and_can_be_ignored()
    {
        using var assembly = CreateEditorBrowsableAssembly(
            EditorBrowsableState.Never,
            null);
        var module = assembly.MainModule;

        var assemblyInheritedType = AddPublicType(module, "AssemblyInheritedType");
        var assemblyInheritedField = AddPublicField(assemblyInheritedType, "AssemblyInheritedField");

        var explicitAlwaysType = AddPublicType(module, "ExplicitAlwaysType");
        AddEditorBrowsable(explicitAlwaysType, module, EditorBrowsableState.Always);

        var advancedOptions = new DocumentationVisibilityOptions(
            DocumentationAccessibility.Protected,
            DocumentationEditorBrowsableVisibility.Advanced);
        var alwaysOptions = new DocumentationVisibilityOptions(
            DocumentationAccessibility.Protected,
            DocumentationEditorBrowsableVisibility.Always);

        Assert.Multiple(() =>
        {
            Assert.That(CecilUtilities.IsVisible(assemblyInheritedType, advancedOptions), Is.False);
            Assert.That(CecilUtilities.IsVisible(assemblyInheritedField, advancedOptions), Is.False);
            Assert.That(CecilUtilities.IsVisible(explicitAlwaysType, advancedOptions), Is.True);

            Assert.That(CecilUtilities.IsVisible(assemblyInheritedType, alwaysOptions), Is.True);
            Assert.That(CecilUtilities.IsVisible(assemblyInheritedField, alwaysOptions), Is.True);
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
        var nativeMethodsType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "NativeMethods");
        var extensionType = FixtureArtifacts.GetTopLevelType(assembly, "Fixture.Root", "GenericSampleExtensions");

        var indexer = genericType.Properties.Single(property => property.Name == "Item");
        var nameProperty = genericType.Properties.Single(property => property.Name == "Name");
        var changedEvent = genericType.Events.Single(@event => @event.Name == "Changed");
        var extensionMethod = extensionType.Methods.Single(method => method.Name == "Extend");
        var externMethod = nativeMethodsType.Methods.Single(method => method.Name == "MessageBeep");
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
            Assert.That(CecilUtilities.GetModifierKeywordString(externMethod), Is.EqualTo("public static extern"));
            Assert.That(CecilUtilities.GetAggregatedPropertyModifierKeywordString(nameProperty), Is.EqualTo("public"));
            Assert.That(CecilUtilities.GetAggregatedEventModifierKeywordString(changedEvent), Is.EqualTo("public"));

            Assert.That(referenceMethod.Parameters[0].Name, Is.EqualTo("item"));
            Assert.That(CecilUtilities.GetParameterModifier(referenceMethod.Parameters[0]), Is.EqualTo(ParameterModifierCandidates.In));
            Assert.That(CecilUtilities.GetParameterModifier(referenceMethod.Parameters[1]), Is.EqualTo(ParameterModifierCandidates.Out));
            Assert.That(CecilUtilities.GetParameterModifier(referenceMethod.Parameters[2]), Is.EqualTo(ParameterModifierCandidates.Ref));

            Assert.That(genericType.GenericParameters.Select(parameter => parameter.Name), Is.EqualTo(new[] { "TItem", "TValue" }));
            Assert.That(genericType.Methods.Single(method => method.Name == "Transform").GenericParameters.Select(parameter => parameter.Name), Is.EqualTo(new[] { "TResult" }));
        });
    }

    private static AssemblyDefinition CreateEditorBrowsableAssembly(
        EditorBrowsableState? assemblyState,
        EditorBrowsableState? moduleState)
    {
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition($"EditorBrowsableFixture.{Guid.NewGuid():N}", new Version(1, 0, 0, 0)),
            $"EditorBrowsableFixture.{Guid.NewGuid():N}",
            ModuleKind.Dll);

        if (assemblyState is { } assemblyVisibility)
        {
            AddEditorBrowsable(assembly, assembly.MainModule, assemblyVisibility);
        }

        if (moduleState is { } moduleVisibility)
        {
            AddEditorBrowsable(assembly.MainModule, assembly.MainModule, moduleVisibility);
        }

        return assembly;
    }

    private static TypeDefinition AddPublicType(
        ModuleDefinition module,
        string name)
    {
        var type = new TypeDefinition(
            "Fixture.Visibility",
            name,
            Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public,
            module.TypeSystem.Object);
        module.Types.Add(type);
        return type;
    }

    private static FieldDefinition AddPublicField(
        TypeDefinition type,
        string name)
    {
        var field = new FieldDefinition(
            name,
            Mono.Cecil.FieldAttributes.Public,
            type.Module.TypeSystem.Int32);
        type.Fields.Add(field);
        return field;
    }

    private static void AddEditorBrowsable(
        ICustomAttributeProvider provider,
        ModuleDefinition module,
        EditorBrowsableState state)
    {
        var attribute = new CustomAttribute(
            module.ImportReference(
                typeof(EditorBrowsableAttribute).GetConstructor(new[] { typeof(EditorBrowsableState) })!));
        attribute.ConstructorArguments.Add(
            new CustomAttributeArgument(
                module.ImportReference(typeof(EditorBrowsableState)),
                (int)state));
        provider.CustomAttributes.Add(attribute);
    }
}
