/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Dockit.Internal;
using Mono.Cecil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dockit;

internal static class Writer
{
    private static async Task WriteFieldsAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        FieldDefinition field,
        int initialLevel,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(Utilities.GetSectionString(
            initialLevel + 3,
            $"{Utilities.EscapeSpecialCharacters(Naming.GetName(field))} field"));

        var dotNetXmlFieldName = DotNetXmlNaming.GetDotNetXmlName(field);
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Field, dotNetXmlFieldName),
            out var dotNetXmlField))
        {
            if (dotNetXmlField.Summary is { } memberSummary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary, false));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await tw.WriteLineAsync($"{Utilities.GetModifier(field)} {Naming.GetName(field.FieldType)} {Naming.GetName(field)};");
        await tw.WriteLineAsync("```");

        if (dotNetXmlField?.Remarks is { } memberRemarks)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks, false));
        }

        if (dotNetXmlField?.Example is { } memberExamples)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberExamples, false));
        }
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WritePropertyAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        PropertyDefinition property,
        int initialLevel,
        HashSet<MethodReference> excludeMethods,
        CancellationToken ct)
    {
        // TODO: indexer

        await tw.WriteLineAsync(Utilities.GetSectionString(
            initialLevel + 3,
            $"{Utilities.EscapeSpecialCharacters(Naming.GetName(property))} property"));

        var dotNetXmlPropertyName = DotNetXmlNaming.GetDotNetXmlName(property);
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Property, dotNetXmlPropertyName),
            out var dotNetXmlProperty))
        {
            if (dotNetXmlProperty.Summary is { } memberSummary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary, false));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await tw.WriteLineAsync($"{Naming.GetName(property.PropertyType)} {Naming.GetName(property)}");
        await tw.WriteLineAsync("{");
        if (property.GetMethod is { } gm && Utilities.IsVisible(gm))
        {
            excludeMethods.Add(gm);
            await tw.WriteLineAsync($"    {Utilities.GetPropertyEventModifier(gm)} get;");
        }
        if (property.SetMethod is { } sm && Utilities.IsVisible(sm))
        {
            excludeMethods.Add(sm);
            await tw.WriteLineAsync($"    {Utilities.GetPropertyEventModifier(sm)} set;");
        }
        await tw.WriteLineAsync("}");
        await tw.WriteLineAsync("```");

        if (dotNetXmlProperty?.Remarks is { } memberRemarks)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks, false));
        }

        if (dotNetXmlProperty?.Example is { } memberExamples)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberExamples, false));
        }
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WriteEventAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        EventDefinition @event,
        int initialLevel,
        HashSet<MethodReference> excludeMethods,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(Utilities.GetSectionString(
            initialLevel + 3,
            $"{Utilities.EscapeSpecialCharacters(Naming.GetName(@event))} event"));

        var dotNetXmlEventName = DotNetXmlNaming.GetDotNetXmlName(@event);
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Event, dotNetXmlEventName),
            out var dotNetXmlEvent))
        {
            if (dotNetXmlEvent.Summary is { } memberSummary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary, false));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await tw.WriteLineAsync($"event {Naming.GetName(@event.EventType)} {Naming.GetName(@event)}");
        await tw.WriteLineAsync("{");
        if (@event.AddMethod is { } am && Utilities.IsVisible(am))
        {
            excludeMethods.Add(am);
            await tw.WriteLineAsync($"    {Utilities.GetPropertyEventModifier(am)} add;");
        }
        if (@event.RemoveMethod is { } rm && Utilities.IsVisible(rm))
        {
            excludeMethods.Add(rm);
            await tw.WriteLineAsync($"    {Utilities.GetPropertyEventModifier(rm)} remove;");
        }
        await tw.WriteLineAsync("}");
        await tw.WriteLineAsync("```");

        if (dotNetXmlEvent?.Remarks is { } memberRemarks)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks, false));
        }

        if (dotNetXmlEvent?.Example is { } memberExamples)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberExamples, false));
        }
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WriteMethodAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        MethodDefinition method,
        int initialLevel,
        CancellationToken ct)
    {
        var title = method.IsConstructor ?
            "Constructor" :
            $"{Utilities.EscapeSpecialCharacters(Naming.GetName(method))}{(Utilities.IsExtensionMethod(method) ? " extension" : "")} method";

        await tw.WriteLineAsync(Utilities.GetSectionString(
            initialLevel + 3, title));

        var dotNetXmlMethodName = DotNetXmlNaming.GetDotNetXmlName(method);
        DotNetXmlMember? dotNetXmlMethod = null;
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Method, dotNetXmlMethodName),
            out dotNetXmlMethod))
        {
            if (dotNetXmlMethod.Summary is { } memberSummary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary, false));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await Utilities.WriteSignatureAsync(tw, method);
        await tw.WriteLineAsync("```");

        /////////////////////////////////////////////////////////
        // Type parameter table.

        if (method.HasGenericParameters ||
            (((MethodReference)method) is GenericInstanceMethod gim && gim.GenericArguments.Count >= 1))
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("|Type parameter|Description|");
            await tw.WriteLineAsync("|:----|:----|");

            if (((MethodReference)method) is GenericInstanceMethod gim2)
            {
                foreach (var ga in gim2.GenericArguments)
                {
                    var gan = Naming.GetName(ga);
                    if (dotNetXmlMethod?.TypeParameters.FirstOrDefault(p => p.Name == gan) is { } dotNetParameter &&
                        dotNetParameter.Description is { } description)
                    {
                        await tw.WriteLineAsync($"|`{gan}`|{Utilities.RenderDotNetXmlElement(description, true)}|");
                    }
                    else
                    {
                        await tw.WriteLineAsync($"|`{gan}`| |");
                    }
                }
            }

            foreach (var gp in method.GenericParameters)
            {
                var gpn = Naming.GetName(gp);
                if (dotNetXmlMethod?.TypeParameters.FirstOrDefault(p => p.Name == gpn) is { } dotNetParameter &&
                    dotNetParameter.Description is { } description)
                {
                    await tw.WriteLineAsync($"|`{gpn}`|{Utilities.RenderDotNetXmlElement(description, true)}|");
                }
                else
                {
                    await tw.WriteLineAsync($"|`{gpn}`| |");
                }
            }
        }

        /////////////////////////////////////////////////////////
        // Parameter table.

        if (method.Parameters.Count >= 1)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("|Parameter|Description|");
            await tw.WriteLineAsync("|:----|:----|");

            foreach (var parameter in method.Parameters)
            {
                if (dotNetXmlMethod?.Parameters.FirstOrDefault(p => p.Name == parameter.Name) is { } dotNetParameter &&
                    dotNetParameter.Description is { } description)
                {
                    await tw.WriteLineAsync($"|`{parameter.Name}`|{Utilities.RenderDotNetXmlElement(description, true)}|");
                }
                else
                {
                    await tw.WriteLineAsync($"|`{parameter.Name}`| |");
                }
            }
        }

        if (dotNetXmlMethod?.Remarks is { } memberRemarks)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks, false));
        }

        if (dotNetXmlMethod?.Example is { } memberExamples)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberExamples, false));
        }
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WriteTypeAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        TypeDefinition type,
        int initialLevel,
        IReadOnlyDictionary<TypeReference, int> membersByType,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(Utilities.GetSectionString(
            initialLevel + 2,
            $"{Utilities.EscapeSpecialCharacters(Naming.GetName(type))} {Utilities.GetTypeString(type)}"));

        var dotNetXmlTypeName = DotNetXmlNaming.GetDotNetXmlName(type, false);
        DotNetXmlMember? dotNetXmlType = null;
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Type, dotNetXmlTypeName),
            out dotNetXmlType))
        {
            if (dotNetXmlType.Summary is { } summary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(summary, false));
            }
        }

        var isDelegateType = Utilities.IsDelegateType(type);
        var isEnumType = Utilities.IsEnumType(type);

        if (isDelegateType)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("```csharp");
            await tw.WriteLineAsync($"namespace {type.Namespace};");
            await tw.WriteLineAsync();
            await Utilities.WriteDelegateSignatureAsync(tw, type);
            await tw.WriteLineAsync("```");
        }
        else if (isEnumType)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("```csharp");
            await tw.WriteLineAsync($"namespace {type.Namespace};");
            await tw.WriteLineAsync();
            await Utilities.WriteEnumValuesAsync(tw, type);
            await tw.WriteLineAsync("```");
        }
        else
        {
            var hasImplementedTypes =
                (type.BaseType != null && !Utilities.IsObjectType(type.BaseType!)) || type.Interfaces.Count >= 1;

            await tw.WriteLineAsync();
            await tw.WriteLineAsync("```csharp");
            await tw.WriteLineAsync($"namespace {type.Namespace};");
            await tw.WriteLineAsync();
            await tw.WriteLineAsync($"{Utilities.GetModifier(type)} {Naming.GetName(type)}{(hasImplementedTypes ? " :" : "")}");

            if (type.BaseType is { } baseType && !Utilities.IsObjectType(baseType))
            {
                if (type.Interfaces.Count >= 1)
                {
                    await tw.WriteLineAsync($"    {Naming.GetName(baseType)},");
                }
                else
                {
                    await tw.WriteLineAsync($"    {Naming.GetName(baseType)}");
                }
            }
            for (var ifsIndex = 0; ifsIndex < type.Interfaces.Count; ifsIndex++)
            {
                var ifs = type.Interfaces[ifsIndex];
                if (ifsIndex < type.Interfaces.Count - 1)
                {
                    await tw.WriteLineAsync($"    {Naming.GetName(ifs.InterfaceType)},");
                }
                else
                {
                    await tw.WriteLineAsync($"    {Naming.GetName(ifs.InterfaceType)}");
                }
            }

            await tw.WriteLineAsync("{");
            await tw.WriteLineAsync($"    // Total members: {(membersByType.TryGetValue(type, out var count) ? count : 0)}");
            await tw.WriteLineAsync("}");
            await tw.WriteLineAsync("```");
        }

        /////////////////////////////////////////////////////////
        // Type parameter table.

        if (type.HasGenericParameters ||
            (((TypeReference)type) is GenericInstanceType git && git.GenericArguments.Count >= 1))
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("|Type parameter|Description|");
            await tw.WriteLineAsync("|:----|:----|");

            if (((TypeReference)type) is GenericInstanceType git2)
            {
                foreach (var ga in git2.GenericArguments)
                {
                    var gan = Naming.GetName(ga);
                    if (dotNetXmlType?.TypeParameters.FirstOrDefault(p => p.Name == gan) is { } dotNetParameter &&
                        dotNetParameter.Description is { } description)
                    {
                        await tw.WriteLineAsync($"|`{gan}`|{Utilities.RenderDotNetXmlElement(description, true)}|");
                    }
                    else
                    {
                        await tw.WriteLineAsync($"|`{gan}`| |");
                    }
                }
            }

            foreach (var gp in type.GenericParameters)
            {
                var gpn = Naming.GetName(gp);
                if (dotNetXmlType?.TypeParameters.FirstOrDefault(p => p.Name == gpn) is { } dotNetParameter &&
                    dotNetParameter.Description is { } description)
                {
                    await tw.WriteLineAsync($"|`{gpn}`|{Utilities.RenderDotNetXmlElement(description, true)}|");
                }
                else
                {
                    await tw.WriteLineAsync($"|`{gpn}`| |");
                }
            }
        }

        if (dotNetXmlType?.Remarks is { } remarks)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(remarks, false));
        }

        if (dotNetXmlType?.Example is { } examples)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(examples, false));
        }

        if (!isDelegateType && !isEnumType)
        {
            /////////////////////////////////////////////////////////
            // Fields.

            foreach (var field in Utilities.GetFields(type))
            {
                await tw.WriteLineAsync();
                await WriteFieldsAsync(tw, dotNetDocument, field, initialLevel, ct);
            }

            /////////////////////////////////////////////////////////
            // Properties.

            var excludeMethods = new HashSet<MethodReference>();

            foreach (var property in Utilities.GetProperties(type))
            {
                await tw.WriteLineAsync();
                await WritePropertyAsync(tw, dotNetDocument, property, initialLevel, excludeMethods, ct);
            }

            /////////////////////////////////////////////////////////
            // Events.

            foreach (var @event in Utilities.GetEvents(type))
            {
                await tw.WriteLineAsync();
                await WriteEventAsync(tw, dotNetDocument, @event, initialLevel, excludeMethods, ct);
            }

            /////////////////////////////////////////////////////////
            // Methods.

            foreach (var method in Utilities.GetMethods(type).
                Where(m => !excludeMethods.Contains(m)))
            {
                await tw.WriteLineAsync();
                await WriteMethodAsync(tw, dotNetDocument, method, initialLevel, ct);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WriteNamespaceAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        AssemblyDefinition assembly,
        string @namespace,
        int initialLevel,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(Utilities.GetSectionString(
            initialLevel + 1,
            $"{Utilities.EscapeSpecialCharacters(@namespace)} namespace"));

        /////////////////////////////////////////////////////////
        // Type index table.

        var types = Utilities.GetTypes(assembly.MainModule).
            Where(t => t.Namespace == @namespace).
            ToArray();

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("|Type|Members|");
        await tw.WriteLineAsync("|:----|:----|");

        var membersByType = new Dictionary<TypeReference, int>();
        foreach (var type in types)
        {
            var ems = new HashSet<MethodReference>();

            var fields = Utilities.GetFields(type);

            var properties = Utilities.GetProperties(type);
            foreach (var property in properties)
            {
                if (property.GetMethod is { } gm)
                {
                    ems.Add(gm);
                }
                if (property.SetMethod is { } sm)
                {
                    ems.Add(sm);
                }
            }

            var events = Utilities.GetEvents(type);
            foreach (var @event in events)
            {
                if (@event.AddMethod is { } am)
                {
                    ems.Add(am);
                }
                if (@event.RemoveMethod is { } rm)
                {
                    ems.Add(rm);
                }
            }

            var methods = Utilities.GetMethods(type).
                Where(m => !ems.Contains(m)).
                ToArray();

            var members = fields.
                Concat<MemberReference>(properties).
                Concat(events).
                Concat(methods).
                OrderBy(m => Naming.GetName(m, true)).
                ToArray();

            var memberList = string.Join(",", members.
                Select(m => $"`{Naming.GetName(m, true)}`").
                Distinct());  // Because all overload methods are same signature, with only "()" argument bracket.
            if (memberList.Length >= 1)
            {
                await tw.WriteLineAsync($"|`{Naming.GetName(type)}`|{memberList}|");
            }

            membersByType[type] = members.Length;
        }

        /////////////////////////////////////////////////////////
        // Types.

        foreach (var type in types)
        {
            await tw.WriteLineAsync();
            await WriteTypeAsync(tw, dotNetDocument, type, initialLevel, membersByType, ct);
        }
    }

    //////////////////////////////////////////////////////////////////////////

    public static async Task WriteMarkdownAsync(
        string markdownPath,
        AssemblyDefinition assembly,
        DotNetXmlDocument dotNetDocument,
        int initialLevel,
        CancellationToken ct)
    {
        if (assembly.Name.Name != dotNetDocument.AssemblyName)
        {
            return;
        }

        using var fs = new FileStream(
            markdownPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536, true);
        var tw = new StreamWriter(fs, Encoding.UTF8);

        /////////////////////////////////////////////////////////
        // An assembly.

        await tw.WriteLineAsync(
            Utilities.GetSectionString(
                initialLevel,
                $"{Utilities.EscapeSpecialCharacters(dotNetDocument.AssemblyName)} assembly"));

        /////////////////////////////////////////////////////////
        // Examine namespaces.

        var namespaces =
            Utilities.GetTypes(assembly.MainModule).
            Select(t => t.Namespace).
            Distinct().
            OrderBy(ns => ns).
            ToArray();

        /////////////////////////////////////////////////////////
        // Namespace index.

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("|Namespace|Types|");
        await tw.WriteLineAsync("|:----|:----|");

        foreach (var @namespace in namespaces)
        {
            var types = string.Join(",",
                Utilities.GetTypes(assembly.MainModule).
                Where(t => t.Namespace == @namespace).
                Select(t => $"`{Naming.GetName(t)}`").
                OrderBy(t => t));
            await tw.WriteLineAsync($"|`{@namespace}`|{types}|");
        }

        /////////////////////////////////////////////////////////
        // Namespaces.

        foreach (var @namespace in namespaces)
        {
            await tw.WriteLineAsync();
            await WriteNamespaceAsync(tw, dotNetDocument, assembly, @namespace, initialLevel, ct);
        }

        await tw.FlushAsync();
    }
}
