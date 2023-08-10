/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dockit.Internal;

// Imported from chibias project.
// https://github.com/kekyo/chibias-cil

internal static class Utilities
{
    internal static readonly Dictionary<string, string> csharpKeywords = new()
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

    public static IEnumerable<T> LastSkipWhile<T>(
        this IEnumerable<T> enumerable,
        Func<T, bool> predicate)
    {
        var q = new Queue<T>();
        foreach (var item in enumerable)
        {
            if (predicate(item))
            {
                q.Enqueue(item);
            }
            else
            {
                while (q.Count >= 1)
                {
                    yield return q.Dequeue();
                }
                yield return item;
            }
        }
    }

    public static string GetDirectoryPath(string path) =>
        Path.GetDirectoryName(path) is { } d ?
            Path.GetFullPath(string.IsNullOrWhiteSpace(d) ? "." : d) :
            Path.DirectorySeparatorChar.ToString();

    public static string GetSectionString(int level, string text) =>
        $"{new string('#', level)} {text}";

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

    public static TypeDefinition[] GetTypes(ModuleDefinition module) =>
        module.Types.
        Where(t =>
            (t.IsPublic || t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly) &&
            IsVisibleByEditorBrowsable(t)).
        OrderBy(t => Naming.GetName(t)).
        ToArray();

    public static FieldDefinition[] GetFields(TypeDefinition type) =>
        type.Fields.
        Where(f => f.IsPublic && IsVisibleByEditorBrowsable(f)).
        OrderBy(Naming.GetName).
        ToArray();

    public static bool IsVisible(MethodDefinition method) =>
        (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly) &&
        IsVisibleByEditorBrowsable(method);

    public static bool IsExtensionMethod(MethodReference method) =>
        !method.HasThis && method.HasParameters &&
        method.Resolve() is { } m &&
        m.IsStatic && m.CustomAttributes.Any(ca =>
            ca.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");

    public static string GetExtensionMethodPreSignature(
        MethodReference method, int index) =>
        (index == 0 && IsExtensionMethod(method)) ? "this " : string.Empty;

    public static PropertyDefinition[] GetProperties(TypeDefinition type) =>
        type.Properties.
        Where(p => p.GetMethod is { } gm && IsVisible(gm) || p.SetMethod is { } sm && IsVisible(sm)).
        OrderBy(Naming.GetName).
        ToArray();

    public static EventDefinition[] GetEvents(TypeDefinition type) =>
        type.Events.
        Where(e => e.AddMethod is { } am && IsVisible(am) || e.RemoveMethod is { } rm && IsVisible(rm)).
        OrderBy(Naming.GetName).
        ToArray();

    public static MethodDefinition[] GetMethods(TypeDefinition type) =>
        type.Methods.
        Where(IsVisible).
        OrderBy(m => m.IsConstructor).
        ThenBy(m => Naming.GetName(m, true)).
        ToArray();

    public static string TrimGenericArguments(string name) =>
        name.IndexOf('`') is { } index && index >= 0 ?
            name.Substring(0, index) : name;

    public static ParameterModifierCandidates GetParameterModifier(ParameterDefinition parameter) =>
        parameter.IsIn ? ParameterModifierCandidates.In :
        parameter.IsOut ? ParameterModifierCandidates.Out :
        ParameterModifierCandidates.Ref;

    private static async Task WriteSignatureBodyAsync(
        TextWriter tw, MethodReference method)
    {
        if (method.Parameters.Count == 0)
        {
            await tw.WriteLineAsync(")");
        }
        else
        {
            await tw.WriteLineAsync();
            for (var index = 0; index < method.Parameters.Count; index++)
            {
                var p = method.Parameters[index];
                if (index < method.Parameters.Count - 1)
                {
                    await tw.WriteLineAsync(
                        $"    {GetExtensionMethodPreSignature(method, p.Index)}{Naming.GetName(p.ParameterType, GetParameterModifier(p))} {Naming.GetName(p)},");
                }
                else
                {
                    await tw.WriteLineAsync(
                        $"    {GetExtensionMethodPreSignature(method, p.Index)}{Naming.GetName(p.ParameterType, GetParameterModifier(p))} {Naming.GetName(p)});");
                }
            }
        }
    }

    public static async Task WriteSignatureAsync(
        TextWriter tw, MethodReference method)
    {
        if (method.Resolve().IsConstructor)
        {
            await tw.WriteAsync(
                $"{GetModifier(method.Resolve())} {Naming.GetName(method.DeclaringType)}(");
        }
        else
        {
            await tw.WriteAsync(
                $"{GetModifier(method.Resolve())} {Naming.GetName(method.ReturnType)} {Naming.GetName(method)}(");
        }
        await WriteSignatureBodyAsync(tw, method);
    }

    public static async Task WriteDelegateSignatureAsync(
        TextWriter tw, TypeDefinition delegateType)
    {
        var m = delegateType.Methods.First(m => m.Name.StartsWith("Invoke"));

        await tw.WriteAsync(
            $"{GetModifier(delegateType, false)} {Naming.GetName(m.ReturnType)} {Naming.GetName(delegateType)}(");
        await WriteSignatureBodyAsync(tw, m);
    }

    public static async Task WriteEnumValuesAsync(
        TextWriter tw, TypeDefinition enumType)
    {
        await tw.WriteLineAsync(
            $"{GetModifier(enumType, false)} {Naming.GetName(enumType)} : {Naming.GetName(enumType.GetEnumUnderlyingType())}");
        await tw.WriteLineAsync(
            "{");

        var fields = enumType.Fields.
            Where(f => f.IsPublic && f.IsLiteral).
            ToArray();

        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (index < fields.Length - 1)
            {
                await tw.WriteLineAsync(
                    $"    {Naming.GetName(field)} = {field.Constant},");
            }
            else
            {
                await tw.WriteLineAsync(
                    $"    {Naming.GetName(field)} = {field.Constant}");
            }
        }

        await tw.WriteLineAsync(
            "}");
    }

    public static bool IsObjectType(TypeReference type) =>
        !type.IsValueType && type.FullName == "System.Object";

    public static bool IsDelegateType(TypeReference type) =>
        !type.IsValueType && type.Resolve().BaseType?.FullName == "System.MulticastDelegate";

    public static bool IsEnumType(TypeReference type) =>
        type.IsValueType && type.Resolve().IsEnum;

    public static string GetTypeString(TypeReference type)
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
        sb.Append(GetTypeString(type));

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

    public static string EscapeSpecialCharacters(string str)
    {
        var sb = new StringBuilder(str);
        sb.Replace("&", "&amp;");
        sb.Replace("<", "&lt;");
        sb.Replace(">", "&gt;");
        sb.Replace("\"", "&quot;");
        return sb.ToString();
    }

    private static void Append(
        StringBuilder sb, string text, bool isInline, bool trim)
    {
        // Will remove all CR/LFs.
        if (isInline)
        {
            var sb2 = new StringBuilder(text);
            sb2.Replace("\r", string.Empty);
            sb2.Replace("\n", string.Empty);
            sb!.Append(EscapeSpecialCharacters(sb2.ToString()));
        }
        // Output whitespace trimming text each lines.
        else
        {
            var tr = new StringReader(text);
            var lines = new Queue<string>();
            while (true)
            {
                var line = tr.ReadLine();
                if (line == null)
                {
                    break;
                }
                line = trim ? line.Trim() : line;
                if (!trim || line.Length >= 1)
                {
                    lines.Enqueue(line);
                }
            }
            while (lines.Count >= 1)
            {
                var line = lines.Dequeue();

                // Avoid include CR/LF at last line.
                if (lines.Count >= 1)
                {
                    sb.AppendLine(line);
                }
                else
                {
                    sb.Append(line);
                }
            }
        }
    }

    public static string RenderDotNetXmlElement(
        XElement element, bool isInline)
    {
        static void Traverse(
            StringBuilder sb, XElement element, bool isInline, bool trim)
        {
            foreach (var node in element.Nodes())
            {
                switch (node)
                {
                    case XElement childElement when childElement.Name == "code":
                        sb.AppendLine("```csharp");
                        var lines = childElement.Value.Replace("\r", string.Empty).Split('\n');
                        var indentLength = lines.Min(line =>
                            string.IsNullOrWhiteSpace(line) ?
                                int.MaxValue :
                                line.TakeWhile(char.IsWhiteSpace).Count());
                        lines = lines.
                            Select(line => line.Substring(Math.Min(indentLength, line.Length)).TrimEnd()).
                            SkipWhile(line => string.IsNullOrWhiteSpace(line)).
                            LastSkipWhile(line => string.IsNullOrWhiteSpace(line)).
                            ToArray();
                        foreach (var line in lines)
                        {
                            sb.AppendLine(line);
                        }
                        sb.Append("```");
                        break;
                    case XElement childElement:
                        var attributes = string.Join(" ", childElement.
                            Attributes().
                            Select(a => $"{a.Name}=\"{EscapeSpecialCharacters(a.Value)}\""));
                        attributes = attributes.Length >= 1 ? (" " + attributes) : attributes;
                        if (childElement.Nodes().Any())
                        {
                            sb.Append($"<{childElement.Name}{attributes}>");
                            Traverse(sb, childElement, isInline, false);
                            sb.Append($"</{childElement.Name}>");
                        }
                        else
                        {
                            sb.Append($"<{childElement.Name}{attributes} />");
                        }
                        break;
                    case XText text:
                        Append(sb, EscapeSpecialCharacters(text.Value), isInline, trim);
                        break;
                    default:
                        Append(sb, EscapeSpecialCharacters(node.ToString()), isInline, trim);
                        break;
                }
            }
        }

        var sb = new StringBuilder();
        Traverse(sb, element, isInline, true);
        return sb.ToString();
    }
}
