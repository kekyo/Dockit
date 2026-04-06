/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
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
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dockit.Internal;

internal static class WriterUtilities
{
    private const int unmanagedMethodImplFlag = 0x0004;
    private const int noInliningMethodImplFlag = 0x0008;
    private const int forwardRefMethodImplFlag = 0x0010;
    private const int synchronizedMethodImplFlag = 0x0020;
    private const int noOptimizationMethodImplFlag = 0x0040;
    private const int preserveSigMethodImplFlag = 0x0080;
    private const int aggressiveInliningMethodImplFlag = 0x0100;
    private const int aggressiveOptimizationMethodImplFlag = 0x0200;
    private const int internalCallMethodImplFlag = 0x1000;

    public static string GetSectionString(int level, string text) =>
        $"{new string('#', level)} {text}";

    public static string GetAnchorString(string anchor) =>
        $"<a name=\"{EscapeSpecialCharacters(anchor)}\"></a>";

    public static string GetAnchorHref(string markdownFileName, string anchor)
    {
        _ = markdownFileName;
        return $"#{EscapeSpecialCharacters(anchor)}";
    }

    public static async Task WriteObsoleteDetailAsync(
        TextWriter tw, ICustomAttributeProvider member, CancellationToken ct)
    {
        if (CecilUtilities.GetObsoleteDescription(member) is { } od)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("|Obsoleted detail|");
            await tw.WriteLineAsync("|:----|");
            await tw.WriteLineAsync($"| {EscapeSpecialCharacters(od)} |");
        }
    }

    public static string[] GetCustomAttributeDeclarations(
        ICustomAttributeProvider member) =>
        member.CustomAttributes.Collect(ca =>
        {
            // Could not refer this member.
            if (member is MemberReference mr &&
                !CecilUtilities.IsVisible(mr))
            {
                return null;
            }

            switch (FullNaming.GetFullName(ca.AttributeType))
            {
                // Hides these attributes.
                case "System.ParamArrayAttribute":
                case "System.Runtime.CompilerServices.ExtensionAttribute":
                case "System.Runtime.CompilerServices.CompilerGeneratedAttribute":
                case "System.Runtime.CompilerServices.IsByRefLikeAttribute":
                case "System.Runtime.CompilerServices.IsReadOnlyAttribute":
                case "System.Runtime.CompilerServices.AsyncStateMachineAttribute":
                case "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute":
                case "System.Runtime.CompilerServices.NullableAttribute":
                case "System.Runtime.CompilerServices.NullableContextAttribute":
                case "System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute":
                case "System.Runtime.CompilerServices.EnumeratorCancellationAttribute":
                    return null;

                // Fixed declaration.
                case "System.ObsoleteAttribute":
                    return "[Obsolete]";   // Obsolete details are shown in a separated table.
            }

            // Trimmed attribute naming.
            var name = Naming.GetName(ca.AttributeType);
            if (name.EndsWith("Attribute"))
            {
                name = name.Substring(0, name.Length - "Attribute".Length);
            }

            static string GetPrettyPrintValue(CustomAttributeArgument caa) =>
                caa.Value switch
                {
                    // Unfolds params array
                    CustomAttributeArgument[] children =>
                        string.Join(",", children.Select(GetPrettyPrintValue)),
                    _ => WriterUtilities.GetPrettyPrintValue(caa.Value, caa.Type),
                };

            var arguments = string.Join(", ", ca.ConstructorArguments.
                Select(GetPrettyPrintValue).
                Concat(ca.Fields.Concat(ca.Properties).
                Select(cana => $"{cana.Name}={GetPrettyPrintValue(cana.Argument)}")).
                ToArray());

            return $"[{name}{(arguments.Length >= 1 ? $"({arguments})" : "")}]";
        }).
        Concat(GetSyntheticCustomAttributeDeclarations(member)).
        Distinct().
        ToArray();

    private static string[] GetSyntheticCustomAttributeDeclarations(
        ICustomAttributeProvider member)
    {
        if (member is not MethodDefinition method)
        {
            return Utilities.Empty<string>();
        }

        var optionNames = new List<string>();
        var implAttributes = (int)method.ImplAttributes;

        static void AddIfPresent(List<string> optionNames, int implAttributes, int flag, string name)
        {
            if ((implAttributes & flag) == flag)
            {
                optionNames.Add(name);
            }
        }

        AddIfPresent(optionNames, implAttributes, unmanagedMethodImplFlag, "Unmanaged");
        AddIfPresent(optionNames, implAttributes, noInliningMethodImplFlag, "NoInlining");
        AddIfPresent(optionNames, implAttributes, forwardRefMethodImplFlag, "ForwardRef");
        AddIfPresent(optionNames, implAttributes, synchronizedMethodImplFlag, "Synchronized");
        AddIfPresent(optionNames, implAttributes, noOptimizationMethodImplFlag, "NoOptimization");
        if (!method.IsPInvokeImpl)
        {
            AddIfPresent(optionNames, implAttributes, preserveSigMethodImplFlag, "PreserveSig");
        }
        AddIfPresent(optionNames, implAttributes, aggressiveInliningMethodImplFlag, "AggressiveInlining");
        AddIfPresent(optionNames, implAttributes, aggressiveOptimizationMethodImplFlag, "AggressiveOptimization");
        AddIfPresent(optionNames, implAttributes, internalCallMethodImplFlag, "InternalCall");

        return optionNames.Count >= 1 ?
            new[] { $"[MethodImpl({string.Join(" | ", optionNames.Select(optionName => $"MethodImplOptions.{optionName}"))})]" } :
            Utilities.Empty<string>();
    }

    public static bool HasVisibleCustomAttributes(
        ICustomAttributeProvider member) =>
        GetCustomAttributeDeclarations(member).Length >= 1;

    public static string GetCustomAttributeDeclarationWithTarget(
        string declaration,
        string target) =>
        declaration.StartsWith("[") ?
            $"[{target}: {declaration.Substring(1)}" :
            declaration;

    public static async Task WriteCustomAttributesAsync(
        TextWriter tw, ICustomAttributeProvider member, int indent, CancellationToken ct)
    {
        var indentString = new string(' ', indent);
        foreach (var declaration in
            GetCustomAttributeDeclarations(member))
        {
            await tw.WriteLineAsync(indentString + declaration);
        }
    }

    public static async Task WriteCustomAttributesAsync(
        TextWriter tw,
        ICustomAttributeProvider member,
        int indent,
        string target,
        CancellationToken ct)
    {
        var indentString = new string(' ', indent);
        foreach (var declaration in
            GetCustomAttributeDeclarations(member).
            Select(declaration => GetCustomAttributeDeclarationWithTarget(declaration, target)))
        {
            await tw.WriteLineAsync(indentString + declaration);
        }
    }

    public static string[] GetGenericConstraintClauses(
        IGenericParameterProvider provider) =>
        provider.GenericParameters.
        Select(genericParameter =>
        {
            var constraints = new List<string>();

            if (genericParameter.HasNotNullableValueTypeConstraint)
            {
                constraints.Add("struct");
            }
            else if (genericParameter.HasReferenceTypeConstraint)
            {
                constraints.Add("class");
            }

            constraints.AddRange(genericParameter.Constraints.
                Select(constraint => Naming.GetName(constraint.ConstraintType)).
                Where(constraint => constraint != "ValueType"));

            if (genericParameter.HasDefaultConstructorConstraint &&
                !genericParameter.HasNotNullableValueTypeConstraint)
            {
                constraints.Add("new()");
            }

            return constraints.Count >= 1 ?
                $"where {genericParameter.Name} : {string.Join(", ", constraints)}" :
                null;
        }).
        Where(clause => clause is not null).
        Cast<string>().
        ToArray();

    public static async Task WriteGenericConstraintClausesAsync(
        TextWriter tw,
        IGenericParameterProvider provider,
        int indent,
        string terminator,
        CancellationToken ct)
    {
        var clauses = GetGenericConstraintClauses(provider);
        if (clauses.Length == 0)
        {
            await tw.WriteLineAsync(terminator);
            return;
        }

        await tw.WriteLineAsync();

        var indentString = new string(' ', indent);
        for (var index = 0; index < clauses.Length; index++)
        {
            var clause = clauses[index];
            if (index < clauses.Length - 1)
            {
                await tw.WriteLineAsync(indentString + clause);
            }
            else
            {
                await tw.WriteLineAsync(indentString + clause + terminator);
            }
        }
    }

    private static async Task WriteSignatureParameterListAsync(
        TextWriter tw, MethodReference method, CancellationToken ct)
    {
        var hasVarArg = CecilUtilities.IsVarArgMethod(method);

        if (method.Parameters.Count == 0)
        {
            await tw.WriteAsync(hasVarArg ? "__arglist)" : ")");
        }
        else
        {
            await tw.WriteLineAsync();

            for (var index = 0; index < method.Parameters.Count; index++)
            {
                var parameter = method.Parameters[index];

                var preSignature = CecilUtilities.IsParamArray(parameter) ? "params " :
                    CecilUtilities.GetMethodParameterPreSignature(method, parameter.Index);
                var typeName = NullableReferenceTypes.GetName(
                    parameter.ParameterType,
                    NullableReferenceTypes.CreateParameterContext(method.Resolve(), parameter),
                    CecilUtilities.GetParameterModifier(parameter));
                var parameterName = Naming.GetName(parameter);
                var defaultValue = parameter.HasConstant ?
                    $" = {GetPrettyPrintValue(parameter.Constant, parameter.ParameterType)}" : "";

                await WriteCustomAttributesAsync(tw, parameter, 4, ct);

                if (index < method.Parameters.Count - 1 || hasVarArg)
                {
                    await tw.WriteLineAsync(
                        $"    {preSignature}{typeName} {parameterName}{defaultValue},");
                }
                else
                {
                    await tw.WriteAsync(
                        $"    {preSignature}{typeName} {parameterName}{defaultValue})");
                }
            }

            if (hasVarArg)
            {
                await tw.WriteAsync("    __arglist)");
            }
        }
    }

    public static async Task WriteSignatureAsync(
        TextWriter tw, MethodReference method, CancellationToken ct)
    {
        var m = method.Resolve();
        await WriteCustomAttributesAsync(tw, m, 0, ct);
        await WriteCustomAttributesAsync(tw, m.MethodReturnType, 0, "return", ct);

        if (m.IsConstructor)
        {
            await tw.WriteAsync(
                $"{CecilUtilities.GetModifierKeywordString(m)} {Naming.GetName(m.DeclaringType)}(");
        }
        else
        {
            var returnTypeName = NullableReferenceTypes.GetName(
                m.ReturnType,
                NullableReferenceTypes.CreateMethodReturnContext(m));
            var methodName =
                Naming.OperatorFormats.TryGetValue(m.Name, out var operatorFormat) ?
                operatorFormat.IsRequiredPostFix ?
                    $"{operatorFormat.Name} {returnTypeName}(" :
                    $"{returnTypeName} {operatorFormat.Name}(" :
                $"{returnTypeName} {Naming.GetName(m, MethodForms.WithPreBrace)}";
            await tw.WriteAsync(
                $"{CecilUtilities.GetModifierKeywordString(m)} {methodName}");
        }

        await WriteSignatureParameterListAsync(tw, m, ct);
        await WriteGenericConstraintClausesAsync(tw, m, 4, ";", ct);
    }

    public static async Task WriteDelegateSignatureAsync(
        TextWriter tw, TypeDefinition delegateType, CancellationToken ct)
    {
        var m = delegateType.Methods.First(m => m.Name.StartsWith("Invoke"));

        await WriteCustomAttributesAsync(tw, delegateType, 0, ct);
        await WriteCustomAttributesAsync(tw, m.MethodReturnType, 0, "return", ct);
        await tw.WriteAsync(
            $"{CecilUtilities.GetModifierKeywordString(delegateType, false)} {NullableReferenceTypes.GetName(m.ReturnType, NullableReferenceTypes.CreateMethodReturnContext(m))} {Naming.GetName(delegateType)}(");
        await WriteSignatureParameterListAsync(tw, m, ct);
        await WriteGenericConstraintClausesAsync(tw, delegateType, 4, ";", ct);
    }

    public static async Task WriteEnumValuesAsync(
        TextWriter tw, TypeDefinition enumType, CancellationToken ct)
    {
        await WriteCustomAttributesAsync(tw, enumType, 0, ct);
        await tw.WriteLineAsync(
            $"{CecilUtilities.GetModifierKeywordString(enumType, false)} {Naming.GetName(enumType)} : {Naming.GetName(enumType.GetEnumUnderlyingType())}");
        await tw.WriteLineAsync(
            "{");

        var fields = CecilUtilities.GetFields(enumType).
            Where(f => f.IsLiteral).
            OrderBy(f => f.Constant).
            ToArray();

        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (index < fields.Length - 1)
            {
                await WriteCustomAttributesAsync(tw, field, 4, ct);
                await tw.WriteLineAsync(
                    $"    {Naming.GetName(field)} = {GetPrettyPrintValue(field.Constant, enumType.GetEnumUnderlyingType())},");
            }
            else
            {
                await WriteCustomAttributesAsync(tw, field, 4, ct);
                await tw.WriteLineAsync(
                    $"    {Naming.GetName(field)} = {GetPrettyPrintValue(field.Constant, enumType.GetEnumUnderlyingType())}");
            }
        }

        await tw.WriteLineAsync(
            "}");
    }

    public static string GetPrettyPrintValue(
        object? value, TypeReference type) =>
        value switch
        {
            _ when CecilUtilities.IsEnumType(type) =>
                type.Resolve().Fields.
                Where(field => field.IsLiteral && field.Constant is not null).
                FirstOrDefault(field => System.Convert.ToDecimal(field.Constant) == System.Convert.ToDecimal(value)) is { } matchedField ?
                    $"{Naming.GetName(type)}.{Naming.GetName(matchedField)}" :
                    value?.ToString() ?? type.Name,
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
            TypeReference t => $"typeof({Naming.GetName(t)})",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? value.GetType().Name,
            _ => value.ToString() ?? value.GetType().Name,
        };

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
        AssemblyDefinition assembly) =>
        GeneratePandocFormedHashReferenceIdentities(
            assembly,
            DocumentationVisibilityOptions.Default);

    public static IReadOnlyDictionary<string, string> GeneratePandocFormedHashReferenceIdentities(
        AssemblyDefinition assembly,
        DocumentationVisibilityOptions options)
    {
        var results = new Dictionary<string, string>();
        var producedCount = new Dictionary<string, int>();

        void AddHashReference(
            string fullName,
            string? xmlName,
            string candidateIdentity)
        {
            var hashReferenceId =
                EscapePandocFormedHashReferenceIdentity(candidateIdentity).
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
            CecilUtilities.GetTypes(assembly.MainModule, options).
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

            var types = CecilUtilities.GetTypes(assembly.MainModule, options).
                Where(t => t.Namespace == @namespace).
                ToArray();

            foreach (var type in types)
            {
                AddHashReference(
                    FullNaming.GetFullName(type),
                    DotNetXmlNaming.GetDotNetXmlName(type),
                    $"{Naming.GetName(type)} {CecilUtilities.GetTypeKeywordString(type)}");

                foreach (var field in CecilUtilities.GetFields(type, options))
                {
                    AddHashReference(
                        FullNaming.GetFullName(field),
                        DotNetXmlNaming.GetDotNetXmlName(field),
                        $"{Naming.GetName(field)} field");
                }

                foreach (var property in CecilUtilities.GetProperties(type, options))
                {
                    AddHashReference(
                        FullNaming.GetFullName(property),
                        DotNetXmlNaming.GetDotNetXmlName(property),
                        $"{Naming.GetName(property)} {(CecilUtilities.IsIndexer(property, options) ? "indexer" : "property")}");
                }

                foreach (var @event in CecilUtilities.GetEvents(type, options))
                {
                    var dotNetXmlEventName = DotNetXmlNaming.GetDotNetXmlName(@event);
                    AddHashReference(
                        FullNaming.GetFullName(@event),
                        DotNetXmlNaming.GetDotNetXmlName(@event),
                        $"{Naming.GetName(@event)} event");
                }

                foreach (var method in CecilUtilities.GetMethods(type, options))
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
            sb!.Append(sb2.ToString().Trim());
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

    private static string[] SplitTopLevelArguments(string text)
    {
        var arguments = new List<string>();
        var braceDepth = 0;
        var bracketDepth = 0;
        var startIndex = 0;

        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '{':
                    braceDepth++;
                    break;

                case '}':
                    braceDepth--;
                    break;

                case '[':
                    bracketDepth++;
                    break;

                case ']':
                    bracketDepth--;
                    break;

                case ',' when braceDepth == 0 && bracketDepth == 0:
                    arguments.Add(text.Substring(startIndex, index - startIndex).Trim());
                    startIndex = index + 1;
                    break;
            }
        }

        var lastArgument = text.Substring(startIndex).Trim();
        if (lastArgument.Length >= 1)
        {
            arguments.Add(lastArgument);
        }

        return arguments.ToArray();
    }

    private static string SimplifyXmlDocTypeLabel(string label)
    {
        if (label.EndsWith("@"))
        {
            return $"ref {SimplifyXmlDocTypeLabel(label.Substring(0, label.Length - 1))}";
        }
        else if (label.EndsWith("*"))
        {
            return $"{SimplifyXmlDocTypeLabel(label.Substring(0, label.Length - 1))}*";
        }
        else if (label.EndsWith("[]"))
        {
            return $"{SimplifyXmlDocTypeLabel(label.Substring(0, label.Length - 2))}[]";
        }
        else if (label.EndsWith("]"))
        {
            var arrayIndex = label.LastIndexOf('[');
            if (arrayIndex >= 0)
            {
                var dimensions = label.Substring(
                    arrayIndex + 1,
                    label.Length - arrayIndex - 2);
                var dimensionParts = dimensions.Split(',');
                if (dimensionParts.Length >= 1 &&
                    dimensionParts.All(dimension => dimension == "0:"))
                {
                    return $"{SimplifyXmlDocTypeLabel(label.Substring(0, arrayIndex))}[{new string(',', dimensionParts.Length - 1)}]";
                }
            }
        }

        var genericIndex = label.IndexOf('{');
        if (genericIndex >= 0 &&
            label.EndsWith("}"))
        {
            var typeName = label.Substring(0, genericIndex);
            var argumentText = label.Substring(genericIndex + 1, label.Length - genericIndex - 2);
            return $"{SimplifyXmlDocTypeLabel(typeName)}<{string.Join(", ", SplitTopLevelArguments(argumentText).Select(SimplifyXmlDocTypeLabel))}>";
        }

        if (Naming.CSharpKeywords.TryGetValue(label, out var keyword))
        {
            return keyword;
        }

        if (label.StartsWith("``") ||
            label.StartsWith("`"))
        {
            return label;
        }

        var lastDotIndex = label.LastIndexOf('.');
        var simplifiedLabel = lastDotIndex >= 0 ?
            label.Substring(lastDotIndex + 1) :
            label;

        var genericArityIndex = simplifiedLabel.IndexOf('`');
        return genericArityIndex >= 0 ?
            simplifiedLabel.Substring(0, genericArityIndex) :
            simplifiedLabel;
    }

    private static string SimplifyReferenceLabel(string label)
    {
        var separatorIndex = label.IndexOf(':');
        if (separatorIndex >= 0)
        {
            label = label.Substring(separatorIndex + 1);
        }

        string? returnTypeLabel = null;
        var returnTypeIndex = label.IndexOf('~');
        if (returnTypeIndex >= 0)
        {
            returnTypeLabel = label.Substring(returnTypeIndex + 1);
            label = label.Substring(0, returnTypeIndex);
        }

        var parameterIndex = label.IndexOf('(');
        if (parameterIndex >= 0)
        {
            label = label.Substring(0, parameterIndex);
        }

        var lastDotIndex = label.LastIndexOf('.');
        if (lastDotIndex >= 0)
        {
            label = label.Substring(lastDotIndex + 1);
        }

        if (Naming.OperatorFormats.TryGetValue(label, out var operatorFormat))
        {
            var operatorLabel =
                operatorFormat.IsRequiredPostFix &&
                !string.IsNullOrWhiteSpace(returnTypeLabel) ?
                $"{operatorFormat.Name} {SimplifyXmlDocTypeLabel(returnTypeLabel!)}" :
                operatorFormat.Name;
            return EscapeSpecialCharacters(operatorLabel);
        }

        var genericIndex = label.IndexOf('`');
        if (genericIndex >= 0)
        {
            label = label.Substring(0, genericIndex);
        }

        label = label switch
        {
            "#ctor" => "Constructor",
            "#cctor" => "Static constructor",
            _ => label,
        };

        return EscapeSpecialCharacters(label);
    }

    public static string RenderReference(
        XElement element,
        IReadOnlyDictionary<string, string> hri,
        string markdownFileName)
    {
        var cref = element.Attribute("cref")?.Value?.Trim();
        var identity = EscapeSpecialCharacters(string.Join(" ",
            element.Value.Replace("\r", string.Empty).Split('\n').Select(c => c.Trim())));

        static string? NormalizeReferenceKey(string? cref)
        {
            if (string.IsNullOrWhiteSpace(cref))
            {
                return null;
            }

            var nonEmptyCref = cref!;
            return nonEmptyCref.Length >= 3 && nonEmptyCref[1] == ':' ?
                nonEmptyCref.Substring(2) :
                nonEmptyCref;
        }

        string? referenceKey = null;
        if (!string.IsNullOrWhiteSpace(cref))
        {
            var nonEmptyCref = cref!;
            referenceKey = hri.ContainsKey(nonEmptyCref) ?
                nonEmptyCref :
                NormalizeReferenceKey(nonEmptyCref);
        }

        var resolvedIdentity =
            string.IsNullOrWhiteSpace(identity) &&
            cref is { Length: >= 1 } ?
            SimplifyReferenceLabel(cref) :
            identity;

        return string.IsNullOrWhiteSpace(cref) ?
            $" {identity} " :
            referenceKey is { Length: >= 1 } && hri.TryGetValue(referenceKey, out var anchor) ?
            string.IsNullOrWhiteSpace(resolvedIdentity) ?
                $" {cref} " :
                $" [{resolvedIdentity}]({GetAnchorHref(markdownFileName, anchor)}) " :
            string.IsNullOrWhiteSpace(resolvedIdentity) ?
                $" {cref} " :
                $" [{resolvedIdentity}]({cref}) ";
    }

    private static void TraverseAndRender(
        StringBuilder sb, XElement element, bool isInline, bool trim,
        IReadOnlyDictionary<string, string> hri,
        string markdownFileName)
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
                        TraverseAndRender(sb, childElement, isInline, false, hri, markdownFileName);
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
                    sb.Append(RenderReference(childElement, hri, markdownFileName));
                    break;

                case XElement childElement:
                    var attributes = string.Join(" ", childElement.
                        Attributes().
                        Select(a => $"{a.Name}=\"{EscapeSpecialCharacters(a.Value)}\""));
                    attributes = attributes.Length >= 1 ? (" " + attributes) : attributes;
                    if (childElement.Nodes().Any())
                    {
                        sb.Append($"<{childElement.Name}{attributes}>");
                        TraverseAndRender(sb, childElement, isInline, false, hri, markdownFileName);
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
        IReadOnlyDictionary<string, string> hri,
        string markdownFileName)
    {
        var sb = new StringBuilder();
        TraverseAndRender(sb, element, isInline, true, hri, markdownFileName);
        return sb.ToString();
    }
}
