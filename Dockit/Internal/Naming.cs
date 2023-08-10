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

internal static class Naming
{
    private static readonly Dictionary<string, string> csharpKeywords = new()
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

    public static string GetName(
        TypeReference type,
        ParameterModifierCandidates pm = ParameterModifierCandidates.Ref)
    {
        if (type is ByReferenceType byrefType)
        {
            var tpm = pm switch
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
        else if (csharpKeywords.TryGetValue(type.FullName, out var name))
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
            return $"{GetName(declaringType)}.{Utilities.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
        else
        {
            return $"{Utilities.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
    }

    public static string GetName(FieldReference field) =>
        field.Name;

    public static string GetName(PropertyReference property) =>
        property.Name;

    public static string GetName(EventReference @event) =>
        @event.Name;

    public static string GetName(ParameterReference parameter) =>
        parameter.Name ?? $"arg{parameter.Index}";

    public static string GetName(MethodReference method, bool withMethodSignature = false)
    {
        if (method.Resolve().IsConstructor)
        {
            return $"{GetName(method.DeclaringType)}{(withMethodSignature ? $"()" : "")}";
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

        //return $"{TrimGenericArguments(method.Name)}{genericTypeParameters}{(withMethodSignature ? $"({GetMethodSignature(method)})" : "")}";
        return $"{Utilities.TrimGenericArguments(method.Name)}{genericTypeParameters}{(withMethodSignature ? $"()" : "")}";
    }

    private static string GetMethodSignature(MethodReference method) =>
        string.Join(",", method.Parameters.
            Select(p => $"{Utilities.GetExtensionMethodPreSignature(method, p.Index)}{GetName(p.ParameterType, Utilities.GetParameterModifier(p))} {Naming.GetName(p)}"));

    public static string GetSignatureName(MethodReference method)
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

        return $"{GetName(method.ReturnType)} {GetName(method.DeclaringType)}.{Naming.GetName(method)}{genericTypeParameters}({GetMethodSignature(method)})";
    }

    public static string GetName(MemberReference member, bool withMethodSignature = false) =>
        member switch
        {
            TypeReference t => GetName(t),
            FieldReference f => GetName(f),
            PropertyReference p => GetName(p),
            EventReference e => GetName(e),
            MethodReference m => GetName(m, withMethodSignature),
            _ => throw new ArgumentException()
        };
}
