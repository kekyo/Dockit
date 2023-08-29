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
using System.Linq;

namespace Dockit.Internal;

// Imported from chibias project.
// https://github.com/kekyo/chibias-cil

internal static class FullNaming
{
    public static string GetFullName(
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
            return $"{tpm} {GetFullName(byrefType.ElementType)}";
        }
        else if (type is ArrayType arrayType)
        {
            return $"{GetFullName(arrayType.ElementType)}[]";
        }
        else if (type is PointerType pointerType)
        {
            return $"{GetFullName(pointerType.ElementType)}*";
        }
        else if (type is GenericParameter gp)
        {
            return $"{(gp.IsCovariant ? "out" : gp.IsContravariant ? "in" : "")}{gp.Name}";
        }
        else if (Naming.CSharpKeywords.TryGetValue(type.FullName, out var name))
        {
            return name;
        }

        var genericTypeParameters = "";
        if (type is GenericInstanceType git)
        {
            genericTypeParameters = $"<{string.Join(",",
                git.GenericArguments.Select(gat => GetFullName(gat)))}>";
        }
        else if (type.HasGenericParameters)
        {
            genericTypeParameters = $"<{string.Join(",",
                type.GenericParameters.Select(gp => GetFullName(gp)))}>";
        }

        if (type.DeclaringType is { } declaringType)
        {
            return $"{type.Namespace}.{GetFullName(declaringType)}.{Naming.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
        else
        {
            return $"{type.Namespace}.{Naming.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
    }

    public static string GetFullName(FieldReference field) =>
        $"{GetFullName(field.DeclaringType)}.{Naming.GetName(field)}";

    private static string GetPropertySignature(PropertyDefinition property)
    {
        var parameters = CecilUtilities.GetIndexerParameters(property);
        return string.Join(",", parameters.
            Select(p => $"{GetFullName(p.ParameterType, CecilUtilities.GetParameterModifier(p))} {Naming.GetName(p)}"));
    }

    public static string GetFullName(PropertyReference property)
    {
        var indexerParameters = GetPropertySignature(property.Resolve());
        return indexerParameters.Length >= 1 ?
            $"{GetFullName(property.DeclaringType)}.this[{indexerParameters}]" :
            $"{GetFullName(property.DeclaringType)}.{Naming.GetName(property)}";
    }

    public static string GetFullName(EventReference @event) =>
        $"{GetFullName(@event.DeclaringType)}.{Naming.GetName(@event)}";

    private static string GetMethodSignature(MethodReference method) =>
        string.Join(",", method.Parameters.
            Select(p => $"{CecilUtilities.GetMethodParameterPreSignature(method, p.Index)}{GetFullName(p.ParameterType, CecilUtilities.GetParameterModifier(p))} {Naming.GetName(p)}"));

    public static string GetFullSignaturedName(MethodReference method)
    {
        if (method.Resolve().IsConstructor)
        {
            return $"{GetFullName(method.DeclaringType)}({GetMethodSignature(method)})";
        }

        var genericTypeParameters = "";
        if (method is GenericInstanceMethod gim)
        {
            genericTypeParameters = $"<{string.Join(",",
                gim.GenericArguments.Select(gat => GetFullName(gat)))}>";
        }
        else if (method.HasGenericParameters)
        {
            genericTypeParameters = $"<{string.Join(",",
                method.GenericParameters.Select(gp => GetFullName(gp)))}>";
        }

        if (Naming.OperatorFormats.TryGetValue(method.Name, out var of))
        {
            return of.IsRequiredPostFix ?
                $"{of.Name} {GetFullName(method.ReturnType)}{genericTypeParameters}({GetMethodSignature(method)})" :
                $"{GetFullName(method.ReturnType)} {GetFullName(method.DeclaringType)}.{of.Name}{genericTypeParameters}({GetMethodSignature(method)})";
        }
        else
        {
            return $"{GetFullName(method.ReturnType)} {GetFullName(method.DeclaringType)}.{Naming.GetName(method)}{genericTypeParameters}({GetMethodSignature(method)})";
        }
    }

    public static string GetFullName(MemberReference member) =>
        member switch
        {
            TypeReference t => GetFullName(t),
            FieldReference f => GetFullName(f),
            PropertyReference p => GetFullName(p),
            EventReference e => GetFullName(e),
            MethodReference m => GetFullSignaturedName(m),
            _ => throw new ArgumentException()
        };
}
