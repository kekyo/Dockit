/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dockit.Internal;

// Imported from chibias project.
// https://github.com/kekyo/chibias-cil

internal enum ParameterModifierCandidates
{
    In,
    Out,
    Ref,
}

[Flags]
internal enum MethodForms
{
    NameOnly = 0x00,
    WithPreBrace = 0x01,
    WithBraces = 0x03,
    WithReturnType = 0x04,
}

internal readonly struct OperatorFormat
{
    public readonly string Name;
    public readonly bool IsRequiredPostFix;

    public OperatorFormat(string name, bool isRequiredPostFix)
    {
        this.Name = name;
        this.IsRequiredPostFix = isRequiredPostFix;
    }
}

internal static class Naming
{
    public static readonly IReadOnlyDictionary<string, string> CSharpKeywords =
        new Dictionary<string, string>()
        {
            { "System.Void", "void" },
            { "System.Byte", "byte" },
            { "System.SByte", "sbyte" },
            { "System.Int16", "short" },
            { "System.UInt16", "ushort" },
            { "System.Int32", "int" },
            { "System.UInt32", "uint" },
            { "System.Int64", "long" },
            { "System.UInt64", "ulong" },
            { "System.Single", "float" },
            { "System.Double", "double" },
            { "System.Boolean", "bool" },
            { "System.Char", "char" },
            { "System.String", "string" },
            { "System.Decimal", "decimal" },
            { "System.Object", "object" },
            { "System.IntPtr", "nint" },
            { "System.UIntPtr", "nuint" },
        };

    public static readonly IReadOnlyDictionary<string, OperatorFormat> OperatorFormats =
        new Dictionary<string, OperatorFormat>()
        {
            { "op_Implicit", new("implicit operator", true) },
            { "op_Explicit", new("explicit operator", true) },
            { "op_Equality", new("operator ==", false) },
            { "op_Inequality", new("operator !=", false) },
            { "op_Addition", new("operator +", false) },
            { "op_Subtraction", new("operator -", false) },
            { "op_Multiply", new("operator *", false) },
            { "op_Division", new("operator /", false) },
            { "op_Modulus", new("operator %", false) },
            { "op_LessThan", new("operator <", false) },
            { "op_GreaterThan", new("operator >", false) },
            { "op_LessThanOrEqual", new("operator <=", false) },
            { "op_GreaterThanOrEqual", new("operator >=", false) },
            { "op_UnaryPlus", new("operator +", false) },
            { "op_UnaryNegation", new("operator -", false) },
        };

    public static string TrimGenericArguments(string name) =>
        name.IndexOf('`') is { } index && index >= 0 ?
            name.Substring(0, index) : name;

    public static string GetName(
        TypeReference type,
        ParameterModifierCandidates pmc = ParameterModifierCandidates.Ref)
    {
        if (type is ByReferenceType byrefType)
        {
            var tpm = pmc switch
            {
                ParameterModifierCandidates.In => "in",
                ParameterModifierCandidates.Out => "out",
                _ => "ref",
            };
            return $"{tpm} {GetName(byrefType.ElementType)}";
        }
        else if (type is ArrayType arrayType)
        {
            return $"{GetName(arrayType.ElementType)}[]";
        }
        else if (type is PointerType pointerType)
        {
            return $"{GetName(pointerType.ElementType)}*";
        }
        else if (type is GenericParameter gp)
        {
            return $"{(gp.IsCovariant ? "out" : gp.IsContravariant ? "in" : "")}{gp.Name}";
        }
        else if (CSharpKeywords.TryGetValue(type.FullName, out var name))
        {
            return name;
        }

        var genericTypeParameters = "";
        if (type is GenericInstanceType git)
        {
            genericTypeParameters = $"<{string.Join(",",
                git.GenericArguments.Select(gat => GetName(gat)))}>";
        }
        else if (type.HasGenericParameters)
        {
            genericTypeParameters = $"<{string.Join(",",
                type.GenericParameters.Select(gp => GetName(gp)))}>";
        }

        if (type.DeclaringType is { } declaringType)
        {
            return $"{GetName(declaringType)}.{TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
        else
        {
            return $"{TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
    }

    public static string GetName(FieldReference field) =>
        field.Name;

    private static string GetPropertySignature(
        PropertyDefinition property, bool includeIndexerParameterNames)
    {
        var parameters = CecilUtilities.GetIndexerParameters(property);
        return string.Join(",", parameters.
            Select(p => includeIndexerParameterNames ?
                $"{GetName(p.ParameterType, CecilUtilities.GetParameterModifier(p))} {GetName(p)}" :
                GetName(p.ParameterType, CecilUtilities.GetParameterModifier(p))));
    }

    public static string GetName(
        PropertyReference property, bool includeIndexerParameterNames = false)
    {
        var indexerParameters = GetPropertySignature(
            property.Resolve(), includeIndexerParameterNames);
        return indexerParameters.Length >= 1 ?
            $"this[{indexerParameters}]" :
            property.Name;
    }

    public static string GetName(EventReference @event) =>
        @event.Name;

    public static string GetName(ParameterReference parameter) =>
        parameter.Name ??
        $"arg{parameter.Index}";

    public static string GetName(
        MethodReference method, MethodForms methodForm = MethodForms.NameOnly)
    {
        var signatureBlaces =
            ((methodForm & MethodForms.WithBraces) == MethodForms.WithBraces) ? "()" :
            ((methodForm & MethodForms.WithPreBrace) == MethodForms.WithPreBrace) ? "(" :
            "";

        if (method.Resolve().IsConstructor)
        {
            return $"{GetName(method.DeclaringType)}{signatureBlaces}";
        }
        else if (OperatorFormats.TryGetValue(method.Name, out var of))
        {
            return of.IsRequiredPostFix ?
                    $"{of.Name} {GetName(method.ReturnType)}{signatureBlaces}" :
                ((methodForm & MethodForms.WithReturnType) == MethodForms.WithReturnType) ?
                    $"{GetName(method.ReturnType)} {of.Name}{signatureBlaces}" :
                    $"{of.Name}{signatureBlaces}";
        }

        var genericTypeParameters = "";
        if (method is GenericInstanceMethod gim)
        {
            genericTypeParameters = $"<{string.Join(",",
                gim.GenericArguments.Select(gat => GetName(gat)))}>";
        }
        else if (method.HasGenericParameters)
        {
            genericTypeParameters = $"<{string.Join(",",
                method.GenericParameters.Select(gp => GetName(gp)))}>";
        }

        return ((methodForm & MethodForms.WithReturnType) == MethodForms.WithReturnType) ?
            $"{GetName(method.ReturnType)} {TrimGenericArguments(method.Name)}{genericTypeParameters}{signatureBlaces}" :
            $"{TrimGenericArguments(method.Name)}{genericTypeParameters}{signatureBlaces}";
    }

    private static string GetMethodSignature(MethodReference method) =>
        string.Join(",", method.Parameters.
            Select(p => $"{CecilUtilities.GetMethodParameterPreSignature(method, p.Index)}{GetName(p.ParameterType, CecilUtilities.GetParameterModifier(p))} {GetName(p)}"));

    public static string GetSignaturedName(MethodReference method)
    {
        if (method.Resolve().IsConstructor)
        {
            return $"{GetName(method.DeclaringType)}({GetMethodSignature(method)})";
        }

        var genericTypeParameters = "";
        if (method is GenericInstanceMethod gim)
        {
            genericTypeParameters = $"<{string.Join(",",
                gim.GenericArguments.Select(gat => GetName(gat)))}>";
        }
        else if (method.HasGenericParameters)
        {
            genericTypeParameters = $"<{string.Join(",",
                method.GenericParameters.Select(gp => GetName(gp)))}>";
        }

        return $"{GetName(method.ReturnType)} {GetName(method.DeclaringType)}.{GetName(method)}{genericTypeParameters}({GetMethodSignature(method)})";
    }

    public static string GetName(
        MemberReference member, MethodForms methodForm = MethodForms.NameOnly) =>
        member switch
        {
            TypeReference t => GetName(t),
            FieldReference f => GetName(f),
            PropertyReference p => GetName(p),
            EventReference e => GetName(e),
            MethodReference m => GetName(m, methodForm),
            _ => throw new ArgumentException()
        };
}
