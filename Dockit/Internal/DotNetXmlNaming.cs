/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.Linq;

namespace Dockit.Internal;

internal static class DotNetXmlNaming
{
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/?redirectedfrom=MSDN#id-strings

    public static string GetDotNetXmlName(
        TypeReference type, bool parameterDeclaration = false)
    {
        if (type is ByReferenceType byrefType)
        {
            return $"{GetDotNetXmlName(byrefType.ElementType)}@";
        }
        else if (type is ArrayType arrayType)
        {
            if (arrayType.Dimensions.Count >= 2)
            {
                // TODO: bounds
                return $"{GetDotNetXmlName(arrayType.ElementType, parameterDeclaration)}[{string.Join(",", arrayType.Dimensions.Select(d => ""))}]";
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
            if (parameterDeclaration)
            {
                genericTypeParameters = $"{{{string.Join(",",
                    git.GenericArguments.Select(gat => GetDotNetXmlName(gat, parameterDeclaration)))}}}";
            }
            else
            {
                genericTypeParameters = $"`{git.GenericArguments.Count}";
            }
        }
        else if (type.HasGenericParameters)
        {
            if (parameterDeclaration)
            {
                genericTypeParameters = $"{{{string.Join(",",
                    type.GenericParameters.Select(gp => GetDotNetXmlName(gp, parameterDeclaration)))}}}";
            }
            else
            {
                genericTypeParameters = $"`{type.GenericParameters.Count}";
            }
        }

        if (type.DeclaringType is { } declaringType)
        {
            return $"{type.Namespace}.{GetDotNetXmlName(declaringType)}.{Utilities.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
        else
        {
            return $"{type.Namespace}.{Utilities.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }
    }

    public static string GetDotNetXmlName(FieldReference field) =>
        $"{GetDotNetXmlName(field.DeclaringType, false)}.{field.Name}";

    public static string GetDotNetXmlName(PropertyReference property) =>
        $"{GetDotNetXmlName(property.DeclaringType, false)}.{property.Name}";

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

        return $"{GetDotNetXmlName(method.DeclaringType, false)}.{Utilities.TrimGenericArguments(method.Name)}{genericParameterCount}({GetDotNetXmlMethodSignature(method)})";
    }
}
