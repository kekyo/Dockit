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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dockit.Internal;

internal static class WriterUtilities
{
    public static string GetSectionString(int level, string text) =>
        $"{new string('#', level)} {text}";

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

                var preSignature = CecilUtilities.IsParamArray(p) ? "params " :
                    CecilUtilities.GetMethodParameterPreSignature(method, p.Index);
                var typeName = Naming.GetName(
                    p.ParameterType, CecilUtilities.GetParameterModifier(p));
                var parameterName = Naming.GetName(p);
                var defaultValue = p.HasConstant ?
                    $" = {(p.Constant?.ToString() ?? "null")}" : "";

                if (index < method.Parameters.Count - 1)
                {
                    await tw.WriteLineAsync(
                        $"    {preSignature}{typeName} {parameterName}{defaultValue},");
                }
                else
                {
                    await tw.WriteLineAsync(
                        $"    {preSignature}{typeName} {parameterName}{defaultValue});");
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
                $"{CecilUtilities.GetModifierKeywordString(method.Resolve())} {Naming.GetName(method.DeclaringType)}(");
        }
        else
        {
            await tw.WriteAsync(
                $"{CecilUtilities.GetModifierKeywordString(method.Resolve())} {Naming.GetName(method, MethodForms.WithPreBrace | MethodForms.WithReturnType)}");
        }
        await WriteSignatureBodyAsync(tw, method);
    }

    public static async Task WriteDelegateSignatureAsync(
        TextWriter tw, TypeDefinition delegateType)
    {
        var m = delegateType.Methods.First(m => m.Name.StartsWith("Invoke"));

        await tw.WriteAsync(
            $"{CecilUtilities.GetModifierKeywordString(delegateType, false)} {Naming.GetName(m.ReturnType)} {Naming.GetName(delegateType)}(");
        await WriteSignatureBodyAsync(tw, m);
    }

    public static async Task WriteEnumValuesAsync(
        TextWriter tw, TypeDefinition enumType)
    {
        await tw.WriteLineAsync(
            $"{CecilUtilities.GetModifierKeywordString(enumType, false)} {Naming.GetName(enumType)} : {Naming.GetName(enumType.GetEnumUnderlyingType())}");
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

    public static string GetPrettyPrintValue(object? value) =>
        EscapeSpecialCharacters(value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            long l => $"{l}L",
            uint ui => $"{ui}U",
            ulong ul => $"{ul}UL",
            float f => $"{f}f",
            double d => $"{d}d",
            decimal m => $"{m}m",
            string str => $"\"{str}\"",
            char ch => $"'{ch}'",
            Enum e => $"{e.GetType().Name}.{e}",
            TypeReference t => $"typeof({Naming.GetName(t)})",
            Type t => $"typeof({t.Name})",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? value.GetType().Name,
            _ => value.ToString() ?? value.GetType().Name,
        });

    public static string EscapeSpecialCharacters(string str)
    {
        var sb = new StringBuilder(str);
        sb.Replace("&", "&amp;");
        sb.Replace("<", "&lt;");
        sb.Replace(">", "&gt;");
        sb.Replace("\"", "&quot;");
        return sb.ToString();
    }

    public static IReadOnlyDictionary<string, string> GeneratePandocFormedHashReferenceIdentities(
        AssemblyDefinition assembly)
    {
        var results = new Dictionary<string, string>();
        var producedCount = new Dictionary<string, int>();

        void AddHashReference(
            string fullName,
            string? xmlName,
            string candidateIdentity)
        {
            var hashReferenceId =
                WriterUtilities.EscapePandocFormedHashReferenceIdentity(candidateIdentity).
                ToLowerInvariant();

            // Avoid duplicated identity by Pandoc formed.
            if (producedCount!.TryGetValue(hashReferenceId, out var count))
            {
                count++;
                producedCount[hashReferenceId] = count;

                var cid = $"{hashReferenceId}-{count}";
                results!.Add(fullName, cid);
                if (xmlName is { } &&
                    !results!.ContainsKey(xmlName))
                {
                    results!.Add(xmlName, cid);
                }
            }
            else
            {
                producedCount[hashReferenceId] = 0;

                results!.Add(fullName, hashReferenceId);
                if (xmlName is { } &&
                    !results!.ContainsKey(xmlName))
                {
                    results!.Add(xmlName, hashReferenceId);
                }
            }
        }

        // Sequential scan flow and the order are important.

        var namespaces =
            CecilUtilities.GetTypes(assembly.MainModule).
            Select(t => t.Namespace).
            Distinct().
            OrderBy(ns => ns).
            ToArray();

        foreach (var @namespace in namespaces)
        {
            AddHashReference(
                @namespace,
                null,
                $"{@namespace} namespace");

            var types = CecilUtilities.GetTypes(assembly.MainModule).
                Where(t => t.Namespace == @namespace).
                ToArray();

            foreach (var type in types)
            {
                AddHashReference(
                    FullNaming.GetFullName(type),
                    DotNetXmlNaming.GetDotNetXmlName(type),
                    $"{Naming.GetName(type)} {CecilUtilities.GetTypeKeywordString(type)}");

                foreach (var field in CecilUtilities.GetFields(type))
                {
                    AddHashReference(
                        FullNaming.GetFullName(field),
                        DotNetXmlNaming.GetDotNetXmlName(field),
                        $"{Naming.GetName(field)} field");
                }

                foreach (var property in CecilUtilities.GetProperties(type))
                {
                    AddHashReference(
                        FullNaming.GetFullName(property),
                        DotNetXmlNaming.GetDotNetXmlName(property),
                        $"{Naming.GetName(property)} {(CecilUtilities.IsIndexer(property) ? "indexer" : "property")}");
                }

                foreach (var @event in CecilUtilities.GetEvents(type))
                {
                    var dotNetXmlEventName = DotNetXmlNaming.GetDotNetXmlName(@event);
                    AddHashReference(
                        FullNaming.GetFullName(@event),
                        DotNetXmlNaming.GetDotNetXmlName(@event),
                        $"{Naming.GetName(@event)} event");
                }

                foreach (var method in CecilUtilities.GetMethods(type))
                {
                    var dotNetXmlMethodName = DotNetXmlNaming.GetDotNetXmlName(method);
                    AddHashReference(
                        FullNaming.GetFullSignaturedName(method),
                        DotNetXmlNaming.GetDotNetXmlName(method),
                        method.IsConstructor ?
                            "Constructor" :
                            $"{Naming.GetName(method)}(){(CecilUtilities.IsExtensionMethod(method) ? " extension" : "")} method");
                }
            }
        }

        return results;
    }

    public static string EscapePandocFormedHashReferenceIdentity(string str)
    {
        var sb = new StringBuilder(str.Length);
        var isLastWhitespace = false;
        foreach (var ch in str)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!isLastWhitespace)
                {
                    sb.Append('-');
                    isLastWhitespace = true;
                }
            }
            else if (ch == '.' || ch == '-' || char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                isLastWhitespace = false;
            }
        }
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

    public static string RenderReference(XElement element,
        IReadOnlyDictionary<string, string> hri)
    {
        var cref = element.Attribute("cref")?.Value?.Trim();
        var identity = EscapeSpecialCharacters(string.Join(" ",
            element.Value.Replace("\r", string.Empty).Split('\n').Select(c => c.Trim())));

        return string.IsNullOrWhiteSpace(cref) ?
            $" {identity} " :
            string.IsNullOrWhiteSpace(identity) ?
            $" {cref} " :
            $" [{identity}]({cref}) ";   // TODO: hri
    }

    private static void TraverseAndRender(
        StringBuilder sb, XElement element, bool isInline, bool trim,
        IReadOnlyDictionary<string, string> hri)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement childElement when
                        childElement.Name == "para":
                    sb.AppendLine();
                    if (childElement.Nodes().Any())
                    {
                        TraverseAndRender(sb, childElement, isInline, false, hri);
                    }
                    break;

                case XElement childElement when
                        childElement.Name == "code":
                    sb.AppendLine();
                    sb.AppendLine("```csharp");
                    var lines = childElement.Value.
                        Replace("\r", string.Empty).
                        Split('\n');
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
                    sb.AppendLine("```");
                    break;

                case XElement childElement when
                        childElement.Name == "c":
                    sb.Append(" `");
                    var code = string.Join(" ",
                        childElement.Value.
                        Replace("\r", string.Empty).
                        Split('\n').
                        Select(c => c.Trim()));
                    sb.Append(code);
                    sb.Append("` ");
                    break;

                case XElement childElement when
                        childElement.Name == "see":
                    sb.Append(RenderReference(childElement, hri));
                    break;

                case XElement childElement:
                    var attributes = string.Join(" ", childElement.
                        Attributes().
                        Select(a => $"{a.Name}=\"{EscapeSpecialCharacters(a.Value)}\""));
                    attributes = attributes.Length >= 1 ? (" " + attributes) : attributes;
                    if (childElement.Nodes().Any())
                    {
                        sb.Append($"<{childElement.Name}{attributes}>");
                        TraverseAndRender(sb, childElement, isInline, false, hri);
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

    public static string RenderDotNetXmlElement(
        XElement element,
        bool isInline,
        IReadOnlyDictionary<string, string> hri)
    {
        var sb = new StringBuilder();
        TraverseAndRender(sb, element, isInline, true, hri);
        return sb.ToString();
    }
}
