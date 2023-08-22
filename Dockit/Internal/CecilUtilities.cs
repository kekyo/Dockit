/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Dockit.Internal;

// Imported from chibias project.
// https://github.com/kekyo/chibias-cil

internal static class CecilUtilities
{
    private static bool IsVisibleByEditorBrowsable(ICustomAttributeProvider member)
    {
        foreach (var ca in member.CustomAttributes)
        {
            if (ca.AttributeType.FullName == "System.ComponentModel.EditorBrowsableAttribute")
            {
                if (ca.ConstructorArguments.FirstOrDefault() is { } caa0 &&
                    caa0.Value is { } caa0v)
                {
                    if (caa0v.Equals(EditorBrowsableState.Never) || caa0v.Equals(1))
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    public static bool IsVisible(TypeDefinition type) =>
        (type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly) &&
        IsVisibleByEditorBrowsable(type);

    public static bool IsVisible(FieldDefinition field) =>
        (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) &&
        IsVisibleByEditorBrowsable(field);

    public static bool IsVisible(PropertyDefinition property) =>
        (GetGetter(property) is { } || GetSetter(property) is { }) &&
        IsVisibleByEditorBrowsable(property);

    public static bool IsVisible(MethodDefinition method) =>
        (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly) &&
        IsVisibleByEditorBrowsable(method);

    public static TypeDefinition[] GetTypes(ModuleDefinition module) =>
        module.Types.
        Where(IsVisible).
        OrderBy(t => Naming.GetName(t)).
        ToArray();

    public static FieldDefinition[] GetFields(TypeDefinition type) =>
        type.Fields.
        Where(IsVisible).
        OrderBy(Naming.GetName).
        ToArray();

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
        property.GetMethod is { } gm && IsVisible(gm) ? gm : null;

    public static MethodDefinition? GetSetter(PropertyDefinition property) =>
        property.SetMethod is { } sm && IsVisible(sm) ? sm : null;

    public static PropertyDefinition[] GetProperties(TypeDefinition type) =>
        type.Properties.
        Where(IsVisible).
        OrderBy(p => Naming.GetName(p)).
        ToArray();

    public static bool IsIndexer(PropertyDefinition property)
    {
        var gm = GetGetter(property);
        var sm = GetSetter(property);
        return gm?.Parameters.Count >= 1 || sm?.Parameters.Count >= 2;
    }

    public static ParameterDefinition[] GetIndexerParameters(PropertyDefinition property)
    {
        var gm = GetGetter(property);
        var sm = GetSetter(property);
        return (gm?.Parameters.ToArray() ??
                sm?.Parameters.Skip(1).ToArray() ??
                Utilities.Empty<ParameterDefinition>()).
                ToArray();
    }

    public static MethodDefinition? GetAdd(EventDefinition @event) =>
       @event.AddMethod is { } am && IsVisible(am) ? am : null;

    public static MethodDefinition? GetRemove(EventDefinition @event) =>
        @event.RemoveMethod is { } rm && IsVisible(rm) ? rm : null;

    public static EventDefinition[] GetEvents(TypeDefinition type) =>
        type.Events.
        Where(e => GetAdd(e) is { } || GetRemove(e) is { }).
        OrderBy(Naming.GetName).
        ToArray();

    public static MethodDefinition[] GetMethods(TypeDefinition type) =>
        type.Methods.
        Where(IsVisible).
        OrderBy(m => m.IsConstructor).
        ThenBy(Naming.GetSignaturedName).
        ToArray();

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

        if (type.IsPublic || type.IsNestedPublic)
        {
            sb.Append("public");
        }
        else if (type.IsNotPublic || type.IsNestedAssembly)
        {
            sb.Append("internal");
        }
        else if (type.IsNestedFamily)
        {
            sb.Append("protected");
        }
        else if (type.IsNestedFamilyOrAssembly)
        {
            sb.Append("protected internal");
        }
        else if (type.IsNestedFamilyAndAssembly)
        {
            sb.Append("private protected");
        }
        else if (type.IsNestedPrivate)
        {
            sb.Append("private");
        }

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
            else if (type.IsSealed)
            {
                sb.Append(" sealed");
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

        if (field.IsPublic)
        {
            sb.Append("public");
        }
        else if (field.IsAssembly)
        {
            sb.Append("internal");
        }
        else if (field.IsFamily)
        {
            sb.Append("protected");
        }
        else if (field.IsFamilyOrAssembly)
        {
            sb.Append("protected internal");
        }
        else if (field.IsFamilyAndAssembly)
        {
            sb.Append("private protected");
        }
        else
        {
            sb.Append("private");
        }

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
    {
        var sb = new StringBuilder();

        if (method.IsPublic)
        {
            sb.Append("public");
        }
        else if (method.IsAssembly)
        {
            sb.Append("internal");
        }
        else if (method.IsFamily)
        {
            sb.Append("protected");
        }
        else if (method.IsFamilyOrAssembly)
        {
            sb.Append("protected internal");
        }
        else if (method.IsFamilyAndAssembly)
        {
            sb.Append("private protected");
        }
        else
        {
            sb.Append("private");
        }

        return sb.ToString();
    }

    public static string GetModifierKeywordString(
        MethodDefinition method)
    {
        var sb = new StringBuilder();

        if (method.IsPublic)
        {
            sb.Append("public");
        }
        else if (method.IsAssembly)
        {
            sb.Append("internal");
        }
        else if (method.IsFamily)
        {
            sb.Append("protected");
        }
        else if (method.IsFamilyOrAssembly)
        {
            sb.Append("protected internal");
        }
        else if (method.IsFamilyAndAssembly)
        {
            sb.Append("private protected");
        }
        else
        {
            sb.Append("private");
        }

        if (method.IsStatic)
        {
            sb.Append(" static");
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
