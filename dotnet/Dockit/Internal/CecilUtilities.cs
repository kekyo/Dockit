/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Dockit.Internal;

// Imported from chibias project.
// https://github.com/kekyo/chibias-cil

internal static class CecilUtilities
{
    private const string EditorBrowsableAttributeFullName =
        "System.ComponentModel.EditorBrowsableAttribute";
    private const string CompilerGeneratedAttributeFullName =
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    private static EditorBrowsableState? GetEditorBrowsableState(
        ICustomAttributeProvider provider)
    {
        foreach (var ca in provider.CustomAttributes)
        {
            if (ca.AttributeType.FullName == EditorBrowsableAttributeFullName &&
                ca.ConstructorArguments.FirstOrDefault() is { Value: { } value })
            {
                switch (value)
                {
                    case EditorBrowsableState state:
                        return state;
                    case int rawValue when Enum.IsDefined(typeof(EditorBrowsableState), rawValue):
                        return (EditorBrowsableState)rawValue;
                }
            }
        }

        return null;
    }

    private static IEnumerable<ICustomAttributeProvider> EnumerateTypeChain(TypeDefinition? type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            yield return current;
        }
    }

    private static IEnumerable<ICustomAttributeProvider> EnumerateEditorBrowsableProviders(
        TypeDefinition type)
    {
        foreach (var provider in EnumerateTypeChain(type))
        {
            yield return provider;
        }

        yield return type.Module;
        yield return type.Module.Assembly;
    }

    private static IEnumerable<ICustomAttributeProvider> EnumerateEditorBrowsableProviders(
        IMemberDefinition member)
    {
        yield return member;

        foreach (var provider in EnumerateTypeChain(member.DeclaringType))
        {
            yield return provider;
        }

        yield return member.DeclaringType.Module;
        yield return member.DeclaringType.Module.Assembly;
    }

    private static EditorBrowsableState? ResolveEditorBrowsableState(
        IEnumerable<ICustomAttributeProvider> providers)
    {
        foreach (var provider in providers)
        {
            if (GetEditorBrowsableState(provider) is { } state)
            {
                return state;
            }
        }

        return null;
    }

    private static bool IsVisibleByEditorBrowsable(
        EditorBrowsableState? state,
        DocumentationEditorBrowsableVisibility visibility) =>
        visibility switch
        {
            DocumentationEditorBrowsableVisibility.Always => true,
            DocumentationEditorBrowsableVisibility.Advanced =>
                state is not EditorBrowsableState.Never,
            DocumentationEditorBrowsableVisibility.Normal =>
                state is not EditorBrowsableState.Never &&
                state is not EditorBrowsableState.Advanced,
            _ => throw new InvalidOperationException(),
        };

    private static bool IsVisibleByEditorBrowsable(
        TypeDefinition type,
        DocumentationVisibilityOptions options) =>
        IsVisibleByEditorBrowsable(
            ResolveEditorBrowsableState(EnumerateEditorBrowsableProviders(type)),
            options.EditorBrowsableVisibility);

    private static bool IsVisibleByEditorBrowsable(
        IMemberDefinition member,
        DocumentationVisibilityOptions options) =>
        IsVisibleByEditorBrowsable(
            ResolveEditorBrowsableState(EnumerateEditorBrowsableProviders(member)),
            options.EditorBrowsableVisibility);

    public static DocumentationAccessibility GetAccessibility(TypeDefinition type) =>
        (type.IsPublic || type.IsNestedPublic) ? DocumentationAccessibility.Public :
        (type.IsNotPublic || type.IsNestedAssembly) ? DocumentationAccessibility.Internal :
        type.IsNestedFamily ? DocumentationAccessibility.Protected :
        type.IsNestedFamilyOrAssembly ? DocumentationAccessibility.ProtectedInternal :
        type.IsNestedFamilyAndAssembly ? DocumentationAccessibility.PrivateProtected :
        DocumentationAccessibility.Private;

    public static DocumentationAccessibility GetAccessibility(FieldDefinition field) =>
        field.IsPublic ? DocumentationAccessibility.Public :
        field.IsAssembly ? DocumentationAccessibility.Internal :
        field.IsFamily ? DocumentationAccessibility.Protected :
        field.IsFamilyOrAssembly ? DocumentationAccessibility.ProtectedInternal :
        field.IsFamilyAndAssembly ? DocumentationAccessibility.PrivateProtected :
        DocumentationAccessibility.Private;

    public static DocumentationAccessibility GetAccessibility(MethodDefinition method) =>
        method.IsPublic ? DocumentationAccessibility.Public :
        method.IsAssembly ? DocumentationAccessibility.Internal :
        method.IsFamily ? DocumentationAccessibility.Protected :
        method.IsFamilyOrAssembly ? DocumentationAccessibility.ProtectedInternal :
        method.IsFamilyAndAssembly ? DocumentationAccessibility.PrivateProtected :
        DocumentationAccessibility.Private;

    private static bool IsAccessible(
        DocumentationAccessibility accessibility,
        DocumentationVisibilityOptions options) =>
        accessibility >= options.Accessibility;

    private static MethodDefinition? GetVisibleAccessor(
        MethodDefinition? method,
        DocumentationVisibilityOptions options) =>
        method is { } candidate &&
        IsAccessible(GetAccessibility(candidate), options) ?
            candidate :
            null;

    public static bool IsVisible(TypeDefinition type) =>
        IsVisible(type, DocumentationVisibilityOptions.Default);

    public static bool IsVisible(
        TypeDefinition type,
        DocumentationVisibilityOptions options) =>
        IsAccessible(GetAccessibility(type), options) &&
        IsVisibleByEditorBrowsable(type, options);

    public static bool IsVisible(FieldDefinition field) =>
        IsVisible(field, DocumentationVisibilityOptions.Default);

    public static bool IsVisible(
        FieldDefinition field,
        DocumentationVisibilityOptions options) =>
        IsAccessible(GetAccessibility(field), options) &&
        IsVisibleByEditorBrowsable(field, options);

    public static bool IsVisible(PropertyDefinition property) =>
        IsVisible(property, DocumentationVisibilityOptions.Default);

    public static bool IsVisible(
        PropertyDefinition property,
        DocumentationVisibilityOptions options) =>
        (GetGetter(property, options) is { } || GetSetter(property, options) is { }) &&
        IsVisibleByEditorBrowsable(property, options);

    public static bool IsVisible(EventDefinition @event) =>
        IsVisible(@event, DocumentationVisibilityOptions.Default);

    public static bool IsVisible(
        EventDefinition @event,
        DocumentationVisibilityOptions options) =>
        (GetAdd(@event, options) is { } || GetRemove(@event, options) is { }) &&
        IsVisibleByEditorBrowsable(@event, options);

    public static bool IsVisible(MethodDefinition method) =>
        IsVisible(method, DocumentationVisibilityOptions.Default);

    public static bool IsVisible(
        MethodDefinition method,
        DocumentationVisibilityOptions options) =>
        IsAccessible(GetAccessibility(method), options) &&
        IsVisibleByEditorBrowsable(method, options);

    public static bool IsVisible(MemberReference member) =>
        IsVisible(member, DocumentationVisibilityOptions.Default);

    public static bool IsVisible(
        MemberReference member,
        DocumentationVisibilityOptions options) =>
        member.Resolve() switch
        {
            TypeDefinition t => IsVisible(t, options),
            FieldDefinition f => IsVisible(f, options),
            PropertyDefinition p => IsVisible(p, options),
            EventDefinition e => IsVisible(e, options),
            MethodDefinition m => IsVisible(m, options),
            { } m => throw new InvalidOperationException(),
        };

    public static TypeDefinition[] GetTypes(ModuleDefinition module) =>
        GetTypes(module, DocumentationVisibilityOptions.Default);

    public static TypeDefinition[] GetTypes(
        ModuleDefinition module,
        DocumentationVisibilityOptions options) =>
        module.Types.
        Where(type => IsVisible(type, options)).
        OrderBy(t => Naming.GetName(t)).
        ToArray();

    public static FieldDefinition[] GetFields(TypeDefinition type) =>
        GetFields(type, DocumentationVisibilityOptions.Default);

    public static FieldDefinition[] GetFields(
        TypeDefinition type,
        DocumentationVisibilityOptions options) =>
        type.Fields.
        Where(field =>
            !field.IsSpecialName &&
            !field.CustomAttributes.Any(ca =>
                ca.AttributeType.FullName == CompilerGeneratedAttributeFullName)).
        Where(field => IsVisible(field, options)).
        OrderBy(Naming.GetName).
        ToArray();

    public static string? GetObsoleteDescription(ICustomAttributeProvider member) =>
        member.CustomAttributes.Where(ca =>
            ca.AttributeType.FullName == "System.ObsoleteAttribute").
            Select(ca => ca.ConstructorArguments[0].Value?.ToString() ?? "").
            FirstOrDefault();

    public static bool IsExtensionMethod(MethodReference method) =>
        !method.HasThis && method.HasParameters &&
        method.Resolve() is { } m &&
        m.IsStatic && m.CustomAttributes.Any(ca =>
            ca.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");

    public static bool IsParamArray(ParameterDefinition parameter) =>
        parameter.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.ParamArrayAttribute");

    public static string GetMethodParameterPreSignature(
        MethodReference method, int index) =>
        (index == 0 && IsExtensionMethod(method)) ? "this " :
        string.Empty;

    public static MethodDefinition? GetGetter(PropertyDefinition property) =>
        GetGetter(property, DocumentationVisibilityOptions.Default);

    public static MethodDefinition? GetGetter(
        PropertyDefinition property,
        DocumentationVisibilityOptions options) =>
        GetVisibleAccessor(property.GetMethod, options);

    public static MethodDefinition? GetSetter(PropertyDefinition property) =>
        GetSetter(property, DocumentationVisibilityOptions.Default);

    public static MethodDefinition? GetSetter(
        PropertyDefinition property,
        DocumentationVisibilityOptions options) =>
        GetVisibleAccessor(property.SetMethod, options);

    public static PropertyDefinition[] GetProperties(TypeDefinition type) =>
        GetProperties(type, DocumentationVisibilityOptions.Default);

    public static PropertyDefinition[] GetProperties(
        TypeDefinition type,
        DocumentationVisibilityOptions options) =>
        type.Properties.
        Where(property => IsVisible(property, options)).
        OrderBy(p => Naming.GetName(p)).
        ToArray();

    public static bool IsIndexer(PropertyDefinition property) =>
        IsIndexer(property, DocumentationVisibilityOptions.Default);

    public static bool IsIndexer(
        PropertyDefinition property,
        DocumentationVisibilityOptions options)
    {
        var gm = GetGetter(property, options);
        var sm = GetSetter(property, options);
        return gm?.Parameters.Count >= 1 || sm?.Parameters.Count >= 2;
    }

    public static ParameterDefinition[] GetIndexerParameters(PropertyDefinition property) =>
        GetIndexerParameters(property, DocumentationVisibilityOptions.Default);

    public static ParameterDefinition[] GetIndexerParameters(
        PropertyDefinition property,
        DocumentationVisibilityOptions options)
    {
        var gm = GetGetter(property, options);
        var sm = GetSetter(property, options);
        return (gm?.Parameters.ToArray() ??
                sm?.Parameters.Skip(1).ToArray() ??
                Utilities.Empty<ParameterDefinition>()).
                ToArray();
    }

    public static MethodDefinition? GetAdd(EventDefinition @event) =>
        GetAdd(@event, DocumentationVisibilityOptions.Default);

    public static MethodDefinition? GetAdd(
        EventDefinition @event,
        DocumentationVisibilityOptions options) =>
        GetVisibleAccessor(@event.AddMethod, options);

    public static MethodDefinition? GetRemove(EventDefinition @event) =>
        GetRemove(@event, DocumentationVisibilityOptions.Default);

    public static MethodDefinition? GetRemove(
        EventDefinition @event,
        DocumentationVisibilityOptions options) =>
        GetVisibleAccessor(@event.RemoveMethod, options);

    public static EventDefinition[] GetEvents(TypeDefinition type) =>
        GetEvents(type, DocumentationVisibilityOptions.Default);

    public static EventDefinition[] GetEvents(
        TypeDefinition type,
        DocumentationVisibilityOptions options) =>
        type.Events.
        Where(@event => GetAdd(@event, options) is { } || GetRemove(@event, options) is { }).
        OrderBy(Naming.GetName).
        ToArray();

    public static MethodDefinition[] GetMethods(TypeDefinition type) =>
        GetMethods(type, DocumentationVisibilityOptions.Default);

    public static MethodDefinition[] GetMethods(
        TypeDefinition type,
        DocumentationVisibilityOptions options) =>
        type.Methods.
        Where(method => IsVisible(method, options)).
        OrderBy(m => m.IsConstructor).
        ThenBy(Naming.GetSignaturedName).
        ToArray();

    public static bool IsVarArgMethod(MethodReference method) =>
        method.CallingConvention == MethodCallingConvention.VarArg;

    public static ParameterModifierCandidates GetParameterModifier(ParameterDefinition parameter) =>
        parameter.IsIn ? ParameterModifierCandidates.In :
        parameter.IsOut ? ParameterModifierCandidates.Out :
        ParameterModifierCandidates.Ref;

    public static bool IsObjectType(TypeReference type) =>
        !type.IsValueType && type.FullName == "System.Object";

    public static bool IsDelegateType(TypeReference type) =>
        !type.IsValueType && type.Resolve().BaseType?.FullName == "System.MulticastDelegate";

    public static bool IsEnumType(TypeReference type) =>
        type.IsValueType && type.Resolve().IsEnum;

    public static bool IsRefStructType(TypeReference type) =>
        type.IsValueType &&
        type.Resolve().CustomAttributes.Any(ca =>
            ca.AttributeType.FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute");

    public static bool IsRecordClassType(TypeReference type)
    {
        if (type.IsValueType)
        {
            return false;
        }

        var resolvedType = type.Resolve();
        return resolvedType.Methods.Any(method => method.Name == "<Clone>$") ||
            resolvedType.Properties.Any(property =>
                property.Name == "EqualityContract" &&
                property.PropertyType.FullName == "System.Type");
    }

    public static bool IsRecordStructType(TypeReference type)
    {
        if (!type.IsValueType)
        {
            return false;
        }

        var resolvedType = type.Resolve();
        return !resolvedType.IsEnum &&
            resolvedType.Methods.Any(method =>
                method.Name == "PrintMembers" &&
                method.Parameters.Count == 1 &&
                method.Parameters[0].ParameterType.FullName == "System.Text.StringBuilder" &&
                method.ReturnType.FullName == "System.Boolean") &&
            resolvedType.Methods.Any(method =>
                method.Name == "Equals" &&
                method.Parameters.Count == 1 &&
                method.ReturnType.FullName == "System.Boolean" &&
                method.Parameters[0].ParameterType.FullName == resolvedType.FullName) &&
            resolvedType.Methods.Any(method =>
                method.Name == "GetHashCode" &&
                method.Parameters.Count == 0 &&
                method.ReturnType.FullName == "System.Int32") &&
            resolvedType.Methods.Any(method =>
                method.Name == "ToString" &&
                method.Parameters.Count == 0 &&
                method.ReturnType.FullName == "System.String") &&
            resolvedType.Methods.Any(method => method.Name == "op_Equality") &&
            resolvedType.Methods.Any(method => method.Name == "op_Inequality");
    }

    public static string GetTypeKeywordString(TypeReference type)
    {
        if (type.IsByReference)
        {
            return "byref";
        }
        else if (type.IsPointer)
        {
            return "pointer";
        }
        else if (type.IsValueType)
        {
            if (IsEnumType(type))
            {
                return "enum";
            }
            else if (IsRecordStructType(type))
            {
                return "record struct";
            }
            else if (IsRefStructType(type))
            {
                return "ref struct";
            }
            else
            {
                return "struct";
            }
        }

        var t = type.Resolve();

        if (t.IsInterface)
        {
            return "interface";
        }
        else if (t.BaseType?.FullName == "System.MulticastDelegate")
        {
            return "delegate";
        }
        else if (IsRecordClassType(type))
        {
            return "record";
        }
        else if (t.IsClass)
        {
            return "class";
        }
        else
        {
            return "type";
        }
    }

    public static string GetModifierKeywordString(
        TypeDefinition type, bool storageModifier = true)
    {
        var sb = new StringBuilder();
        sb.Append(GetAccessibilityKeyword(GetAccessibility(type)));

        if (storageModifier)
        {
            if (type.IsAbstract && type.IsSealed)
            {
                sb.Append(" static");
            }
            else if (type.IsAbstract && !type.IsInterface)
            {
                sb.Append(" abstract");
            }
            else if (type.IsSealed && !type.IsValueType)
            {
                sb.Append(" sealed");
            }
            else if (type.IsValueType &&
                type.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute"))
            {
                sb.Append(" readonly");
            }
        }

        sb.Append(' ');
        sb.Append(GetTypeKeywordString(type));

        return sb.ToString();
    }

    public static string GetModifierKeywordString(
        FieldDefinition field)
    {
        var sb = new StringBuilder();
        sb.Append(GetAccessibilityKeyword(GetAccessibility(field)));

        if (field.IsLiteral)
        {
            sb.Append(" const");
        }
        else
        {
            if (field.IsStatic)
            {
                sb.Append(" static");
            }
            if (field.IsInitOnly)
            {
                sb.Append(" readonly");
            }
        }

        return sb.ToString();
    }

    public static string GetPropertyEventModifierKeywordString(
        MethodDefinition method)
        => GetAccessibilityKeyword(GetAccessibility(method));

    private static string GetAccessibilityKeyword(
        DocumentationAccessibility accessibility) =>
        accessibility switch
        {
            DocumentationAccessibility.Public => "public",
            DocumentationAccessibility.ProtectedInternal => "protected internal",
            DocumentationAccessibility.Protected => "protected",
            DocumentationAccessibility.Internal => "internal",
            DocumentationAccessibility.PrivateProtected => "private protected",
            DocumentationAccessibility.Private => "private",
            _ => throw new InvalidOperationException(),
        };

    public static string GetAggregatedPropertyModifierKeywordString(PropertyDefinition property) =>
        GetAggregatedPropertyModifierKeywordString(
            property,
            DocumentationVisibilityOptions.Default);

    public static string GetAggregatedPropertyModifierKeywordString(
        PropertyDefinition property,
        DocumentationVisibilityOptions options) =>
        new[] { GetGetter(property, options), GetSetter(property, options) }.
        Where(method => method is not null).
        Cast<MethodDefinition>().
        OrderByDescending(GetAccessibility).
        Select(GetPropertyEventModifierKeywordString).
        First();

    public static string GetAggregatedEventModifierKeywordString(EventDefinition @event) =>
        GetAggregatedEventModifierKeywordString(
            @event,
            DocumentationVisibilityOptions.Default);

    public static string GetAggregatedEventModifierKeywordString(
        EventDefinition @event,
        DocumentationVisibilityOptions options) =>
        new[] { GetAdd(@event, options), GetRemove(@event, options) }.
        Where(method => method is not null).
        Cast<MethodDefinition>().
        OrderByDescending(GetAccessibility).
        Select(GetPropertyEventModifierKeywordString).
        First();

    public static string GetAccessorModifierKeywordString(
        string aggregatedModifier,
        MethodDefinition method)
    {
        var modifier = GetPropertyEventModifierKeywordString(method);
        return modifier == aggregatedModifier ?
            string.Empty :
            modifier + " ";
    }

    public static string GetModifierKeywordString(
        MethodDefinition method)
    {
        var sb = new StringBuilder();
        sb.Append(GetAccessibilityKeyword(GetAccessibility(method)));

        if (method.IsStatic)
        {
            sb.Append(" static");
        }

        if (method.IsPInvokeImpl || method.IsInternalCall)
        {
            sb.Append(" extern");
        }

        if (method.IsAbstract)
        {
            sb.Append(" abstract");
        }
        else if (method.IsVirtual)
        {
            if (method.IsReuseSlot)
            {
                sb.Append(" override");

                if (method.IsFinal)
                {
                    sb.Append(" sealed");
                }
            }
            else
            {
                sb.Append(" virtual");
            }
        }

        return sb.ToString();
    }
}
