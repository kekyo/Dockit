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

// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/?redirectedfrom=MSDN#id-strings

internal static class DotNetXmlNaming
{
    private static GenericParameter[] GetDeclaredGenericParameters(TypeReference type)
    {
        var inheritedCount = type.DeclaringType?.GenericParameters.Count ?? 0;
        var declaredCount = Math.Max(0, type.GenericParameters.Count - inheritedCount);
        return type.GenericParameters.
            Skip(Math.Max(0, type.GenericParameters.Count - declaredCount)).
            ToArray();
    }

    private static TypeReference[] GetDeclaredGenericArguments(GenericInstanceType git)
    {
        var declaredCount = GetDeclaredGenericParameters(git.ElementType).Length;
        return git.GenericArguments.
            Skip(Math.Max(0, git.GenericArguments.Count - declaredCount)).
            ToArray();
    }

    public static string GetDotNetXmlName(
        TypeReference type, bool parameterDeclaration = false)
    {
        if (type is ByReferenceType byrefType)
        {
            return $"{GetDotNetXmlName(byrefType.ElementType, parameterDeclaration)}@";
        }
        else if (type is ArrayType arrayType)
        {
            if (!arrayType.IsVector)
            {
                return $"{GetDotNetXmlName(arrayType.ElementType, parameterDeclaration)}[{string.Join(",", arrayType.Dimensions.Select(_ => "0:"))}]";
            }
            else
            {
                return $"{GetDotNetXmlName(arrayType.ElementType, parameterDeclaration)}[]";
            }
        }
        else if (type is PointerType pointerType)
        {
            return $"{GetDotNetXmlName(pointerType.ElementType, parameterDeclaration)}*";
        }
        else if (type is GenericParameter gp)
        {
            return gp.DeclaringMethod is { } ? $"``{gp.Position}" : $"`{gp.Position}";
        }

        var genericTypeParameters = "";
        if (type is GenericInstanceType git)
        {
            var declaredArguments = GetDeclaredGenericArguments(git);
            if (parameterDeclaration)
            {
                if (declaredArguments.Length >= 1)
                {
                    genericTypeParameters = $"{{{string.Join(",",
                        declaredArguments.Select(gat => GetDotNetXmlName(gat, parameterDeclaration)))}}}";
                }
            }
            else
            {
                if (declaredArguments.Length >= 1)
                {
                    genericTypeParameters = $"`{declaredArguments.Length}";
                }
            }
        }
        else
        {
            var declaredParameters = GetDeclaredGenericParameters(type);
            if (parameterDeclaration)
            {
                if (declaredParameters.Length >= 1)
                {
                    genericTypeParameters = $"{{{string.Join(",",
                        declaredParameters.Select(gp => GetDotNetXmlName(gp, parameterDeclaration)))}}}";
                }
            }
            else
            {
                if (declaredParameters.Length >= 1)
                {
                    genericTypeParameters = $"`{declaredParameters.Length}";
                }
            }
        }

        if (type.DeclaringType is { } declaringType)
        {
            return $"{GetDotNetXmlName(declaringType)}.{Naming.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
        else
        {
            var prefix = string.IsNullOrWhiteSpace(type.Namespace) ? string.Empty : $"{type.Namespace}.";
            return $"{prefix}{Naming.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
    }

    public static string GetDotNetXmlName(FieldReference field) =>
        $"{GetDotNetXmlName(field.DeclaringType, false)}.{field.Name}";

    public static string GetDotNetXmlName(PropertyReference property)
    {
        var signature = string.Join(",",
            CecilUtilities.GetIndexerParameters(property.Resolve()).
                Select(p => GetDotNetXmlName(p.ParameterType, true)));

        return signature.Length >= 1 ?
            $"{GetDotNetXmlName(property.DeclaringType, false)}.{property.Name}({signature})" :
            $"{GetDotNetXmlName(property.DeclaringType, false)}.{property.Name}";
    }

    public static string GetDotNetXmlName(EventReference @event) =>
        $"{GetDotNetXmlName(@event.DeclaringType, false)}.{@event.Name}";

    private static string GetDotNetXmlMethodSignature(MethodReference method) =>
        string.Join(",", method.Parameters.
            Select(p => GetDotNetXmlName(p.ParameterType, true)));

    public static string GetDotNetXmlName(MethodReference method)
    {
        var genericParameterCount = "";
        if (method is GenericInstanceMethod gim)
        {
            genericParameterCount = $"``{gim.GenericArguments.Count}";
        }
        else if (method.HasGenericParameters)
        {
            genericParameterCount = $"``{method.GenericParameters.Count}";
        }

        var methodName = method.Name switch
        {
            ".ctor" => "#ctor",
            ".cctor" => "#cctor",
            _ => Naming.TrimGenericArguments(method.Name),
        };
        var signature = GetDotNetXmlMethodSignature(method);
        var signaturePart = signature.Length >= 1 ? $"({signature})" : string.Empty;
        var returnTypeSuffix =
            method.Name == "op_Implicit" || method.Name == "op_Explicit" ?
            $"~{GetDotNetXmlName(method.ReturnType, true)}" :
            string.Empty;

        return $"{GetDotNetXmlName(method.DeclaringType, false)}.{methodName}{genericParameterCount}{signaturePart}{returnTypeSuffix}";
    }
}
