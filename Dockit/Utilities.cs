/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dockit;

// Imported from chibias project.
// https://github.com/kekyo/chibias-cil

internal static class Utilities
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

    public static string GetDirectoryPath(string path) =>
        Path.GetDirectoryName(path) is { } d ?
            Path.GetFullPath(string.IsNullOrWhiteSpace(d) ? "." : d) :
            Path.DirectorySeparatorChar.ToString();

    public enum ParameterModifierCandidates
    {
        In,
        Out,
        Ref,
    }

    public static string GetFullName(TypeReference type, ParameterModifierCandidates pm = ParameterModifierCandidates.Ref)
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
        else if (type is GenericParameter genericParameterType)
        {
            return genericParameterType.Name;
        }
        else if (csharpKeywords.TryGetValue(type.FullName, out var name))
        {
            return name;
        }
        else if (type.DeclaringType is { } declaringType)
        {
            return $"{type.Namespace}.{GetFullName(declaringType)}.{type.Name}";
        }
        else
        {
            return $"{type.Namespace}.{type.Name}";
        }
    }

    public static string GetFullName(FieldReference field) =>
        $"{GetFullName(field.DeclaringType)}.{field.Name}";

    public static string GetFullName(PropertyReference property) =>
        $"{GetFullName(property.DeclaringType)}.{property.Name}";

    public static string GetFullName(EventReference @event) =>
        $"{GetFullName(@event.DeclaringType)}.{@event.Name}";

    private static ParameterModifierCandidates GetParameterModifier(ParameterDefinition parameter) =>
        parameter.IsIn ? ParameterModifierCandidates.In :
        parameter.IsOut ? ParameterModifierCandidates.Out :
        ParameterModifierCandidates.Ref;

    public static string GetMethodSignature(MethodReference method) =>
        string.Join(",", method.Parameters.
            Select(p => $"{GetFullName(p.ParameterType, GetParameterModifier(p))} {p.Name}"));

    public static string GetFullName(MethodReference method) =>
        $"{GetFullName(method.ReturnType)} {GetFullName(method.DeclaringType)}.{method.Name}({GetMethodSignature(method)})";

    public static async Task WriteSignatureBodyAsync(
        TextWriter tw, MethodReference method)
    {
        for (var index = 0; index < method.Parameters.Count; index++)
        {
            var p = method.Parameters[index];
            if (index < method.Parameters.Count - 1)
            {
                await tw.WriteLineAsync($"    {GetFullName(p.ParameterType, GetParameterModifier(p))} {p.Name},");
            }
            else
            {
                await tw.WriteLineAsync($"    {GetFullName(p.ParameterType, GetParameterModifier(p))} {p.Name})");
            }
        }
    }

    public static async Task WriteSignatureAsync(
        TextWriter tw, MethodReference method)
    {
        await tw.WriteLineAsync(
            $"{GetModifier(method.Resolve())} {GetFullName(method.ReturnType)} {method.Name}(");
        await WriteSignatureBodyAsync(tw, method);
    }

    public static async Task WriteSignatureAsync(
        TextWriter tw, TypeDefinition delegateType)
    {
        var m = delegateType.Methods.First(m => m.Name.StartsWith("Invoke"));

        await tw.WriteLineAsync(
            $"{GetModifier(delegateType, false)} {GetFullName(m.ReturnType)} {delegateType.Name}(");
        await WriteSignatureBodyAsync(tw, m);
    }

    public static bool IsObjectType(TypeReference type) =>
        type.Resolve().FullName == "System.Object";

    public static bool IsDelegateType(TypeReference type) =>
        type.Resolve().BaseType?.FullName == "System.MulticastDelegate";

    public static string GetModifier(
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
            else if (type.IsAbstract)
            {
                sb.Append(" abstract");
            }
            else if (type.IsSealed)
            {
                sb.Append(" sealed");
            }
        }

        if (type.IsClass)
        {
            if (IsDelegateType(type))
            {
                sb.Append(" delegate");
            }
            else
            {
                sb.Append(" class");
            }
        }
        else if (type.IsEnum)
        {
            sb.Append(" enum");
        }
        else if (type.IsValueType)
        {
            sb.Append(" struct");
        }

        return sb.ToString();
    }

    public static string GetModifier(FieldDefinition field)
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

    public static string GetPropertyEventModifier(MethodDefinition method)
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

    public static string GetModifier(MethodDefinition method)
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
        if (method.IsFinal)
        {
            sb.Append(" sealed");
        }

        return sb.ToString();
    }

    public static string RenderDotNetXmlElement(XElement element)
    {
        return element.Value.Trim();
    }
}
