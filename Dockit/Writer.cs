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
    private static readonly HashSet<string> validAttributes = new()
    {
        "System.Reflection.AssemblyFileVersionAttribute",
        "System.Reflection.AssemblyInformationalVersionAttribute",
        //"System.Reflection.AssemblyTitleAttribute",
        //"System.Reflection.AssemblyDescriptionAttribute",
        //"System.Reflection.AssemblyCopyrightAttribute",
        //"System.Reflection.AssemblyConfigurationAttribute",
    };

    private static async Task WriteRemarksAsync(
        TextWriter tw,
        DotNetXmlMember? dotNetXmlMember,
        IReadOnlyDictionary<string, string> hri,
        CancellationToken ct)
    {
        if (dotNetXmlMember?.Remarks is { } memberRemarks)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(WriterUtilities.RenderDotNetXmlElement(memberRemarks, false, hri));
        }

        if (dotNetXmlMember?.Example is { } memberExamples)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync(WriterUtilities.RenderDotNetXmlElement(memberExamples, false, hri));
        }

        if (dotNetXmlMember?.SeeAlso is { } memberSeeAlso)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync($"See also: {WriterUtilities.RenderReference(memberSeeAlso, hri)}");
        }
    }

    private static async Task WriteFieldsAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        FieldDefinition field,
        int initialLevel,
        IReadOnlyDictionary<string, string> hri,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(WriterUtilities.GetSectionString(
            initialLevel + 3,
            $"{WriterUtilities.EscapeSpecialCharacters(Naming.GetName(field))} field"));

        var dotNetXmlFieldName = DotNetXmlNaming.GetDotNetXmlName(field);
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Field, dotNetXmlFieldName),
            out var dotNetXmlField))
        {
            if (dotNetXmlField.Summary is { } memberSummary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(WriterUtilities.RenderDotNetXmlElement(memberSummary, false, hri));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await tw.WriteLineAsync($"{CecilUtilities.GetModifierKeywordString(field)} {Naming.GetName(field.FieldType)} {Naming.GetName(field)};");
        await tw.WriteLineAsync("```");

        await WriteRemarksAsync(tw, dotNetXmlField, hri, ct);
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WritePropertyAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        PropertyDefinition property,
        int initialLevel,
        IReadOnlyDictionary<string, string> hri,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(WriterUtilities.GetSectionString(
            initialLevel + 3,
            $"{WriterUtilities.EscapeSpecialCharacters(Naming.GetName(property))} {(CecilUtilities.IsIndexer(property) ? "indexer" : "property")}"));

        var dotNetXmlPropertyName = DotNetXmlNaming.GetDotNetXmlName(property);
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Property, dotNetXmlPropertyName),
            out var dotNetXmlProperty))
        {
            if (dotNetXmlProperty.Summary is { } memberSummary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(WriterUtilities.RenderDotNetXmlElement(memberSummary, false, hri));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await tw.WriteLineAsync($"{Naming.GetName(property.PropertyType)} {Naming.GetName(property, true)}");
        await tw.WriteLineAsync("{");
        if (CecilUtilities.GetGetter(property) is { } gm)
        {
            await tw.WriteLineAsync($"    {CecilUtilities.GetPropertyEventModifierKeywordString(gm)} get;");
        }
        if (CecilUtilities.GetSetter(property) is { } sm)
        {
            await tw.WriteLineAsync($"    {CecilUtilities.GetPropertyEventModifierKeywordString(sm)} set;");
        }
        await tw.WriteLineAsync("}");
        await tw.WriteLineAsync("```");

        await WriteRemarksAsync(tw, dotNetXmlProperty, hri, ct);
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WriteEventAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        EventDefinition @event,
        int initialLevel,
        IReadOnlyDictionary<string, string> hri,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(WriterUtilities.GetSectionString(
            initialLevel + 3,
            $"{WriterUtilities.EscapeSpecialCharacters(Naming.GetName(@event))} event"));

        var dotNetXmlEventName = DotNetXmlNaming.GetDotNetXmlName(@event);
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Event, dotNetXmlEventName),
            out var dotNetXmlEvent))
        {
            if (dotNetXmlEvent.Summary is { } memberSummary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(WriterUtilities.RenderDotNetXmlElement(memberSummary, false, hri));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await tw.WriteLineAsync($"event {Naming.GetName(@event.EventType)} {Naming.GetName(@event)}");
        await tw.WriteLineAsync("{");
        if (CecilUtilities.GetAdd(@event) is { } am)
        {
            await tw.WriteLineAsync($"    {CecilUtilities.GetPropertyEventModifierKeywordString(am)} add;");
        }
        if (CecilUtilities.GetRemove(@event) is { } rm)
        {
            await tw.WriteLineAsync($"    {CecilUtilities.GetPropertyEventModifierKeywordString(rm)} remove;");
        }
        await tw.WriteLineAsync("}");
        await tw.WriteLineAsync("```");

        await WriteRemarksAsync(tw, dotNetXmlEvent, hri, ct);
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WriteMethodAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        MethodDefinition method,
        int initialLevel,
        IReadOnlyDictionary<string, string> hri,
        CancellationToken ct)
    {
        var title = method.IsConstructor ?
            "Constructor" :
            $"{WriterUtilities.EscapeSpecialCharacters(Naming.GetName(method))}(){(CecilUtilities.IsExtensionMethod(method) ? " extension" : "")} method";

        await tw.WriteLineAsync(WriterUtilities.GetSectionString(
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
                await tw.WriteLineAsync(WriterUtilities.RenderDotNetXmlElement(memberSummary, false, hri));
            }
        }

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("```csharp");
        await WriterUtilities.WriteSignatureAsync(tw, method);
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
                        await tw.WriteLineAsync($"| `{gan}` | {WriterUtilities.RenderDotNetXmlElement(description, true, hri)} |");
                    }
                    else
                    {
                        await tw.WriteLineAsync($"| `{gan}` | |");
                    }
                }
            }

            foreach (var gp in method.GenericParameters)
            {
                var gpn = Naming.GetName(gp);
                if (dotNetXmlMethod?.TypeParameters.FirstOrDefault(p => p.Name == gpn) is { } dotNetParameter &&
                    dotNetParameter.Description is { } description)
                {
                    await tw.WriteLineAsync($"| `{gpn}` | {WriterUtilities.RenderDotNetXmlElement(description, true, hri)} |");
                }
                else
                {
                    await tw.WriteLineAsync($"| `{gpn}` | |");
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
                    await tw.WriteLineAsync($"| `{parameter.Name}` | {WriterUtilities.RenderDotNetXmlElement(description, true, hri)} |");
                }
                else
                {
                    await tw.WriteLineAsync($"| `{parameter.Name}` | |");
                }
            }
        }

        await WriteRemarksAsync(tw, dotNetXmlMethod, hri, ct);
    }

    //////////////////////////////////////////////////////////////////////////

    private sealed class VisibleMembers
    {
        public readonly FieldDefinition[] Fields;
        public readonly PropertyDefinition[] Properties;
        public readonly EventDefinition[] Events;
        public readonly MethodDefinition[] Methods;
        public readonly MemberReference[] OverallMembers;

        public VisibleMembers(TypeDefinition type)
        {
            var ems = new HashSet<MethodReference>();

            this.Fields = CecilUtilities.GetFields(type);

            this.Properties = CecilUtilities.GetProperties(type);
            foreach (var property in this.Properties)
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

            this.Events = CecilUtilities.GetEvents(type);
            foreach (var @event in this.Events)
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

            this.Methods = CecilUtilities.GetMethods(type).
                Where(m => !ems.Contains(m)).
                ToArray();

            this.OverallMembers = this.Fields.
                Concat<MemberReference>(this.Properties).
                Concat(this.Events).
                Concat(this.Methods).
                OrderBy(m => Naming.GetName(m, MethodForms.WithBraces)).
                ToArray();
        }
    }

    //////////////////////////////////////////////////////////////////////////

    private static async Task WriteTypeAsync(
        StreamWriter tw,
        DotNetXmlDocument dotNetDocument,
        TypeDefinition type,
        int initialLevel,
        VisibleMembers visibleMembers,
        IReadOnlyDictionary<string, string> hri,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(WriterUtilities.GetSectionString(
            initialLevel + 2,
            $"{WriterUtilities.EscapeSpecialCharacters(Naming.GetName(type))} {CecilUtilities.GetTypeKeywordString(type)}"));

        var dotNetXmlTypeName = DotNetXmlNaming.GetDotNetXmlName(type, false);
        DotNetXmlMember? dotNetXmlType = null;
        if (dotNetDocument.Members.TryGetValue(
            new(DotNetXmlMemberTypes.Type, dotNetXmlTypeName),
            out dotNetXmlType))
        {
            if (dotNetXmlType.Summary is { } summary)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync(WriterUtilities.RenderDotNetXmlElement(summary, false, hri));
            }
        }

        var isDelegateType = CecilUtilities.IsDelegateType(type);
        var isEnumType = CecilUtilities.IsEnumType(type);

        if (isDelegateType)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("```csharp");
            await tw.WriteLineAsync($"namespace {type.Namespace};");
            await tw.WriteLineAsync();
            await WriterUtilities.WriteDelegateSignatureAsync(tw, type);
            await tw.WriteLineAsync("```");
        }
        else if (isEnumType)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("```csharp");
            await tw.WriteLineAsync($"namespace {type.Namespace};");
            await tw.WriteLineAsync();
            await WriterUtilities.WriteEnumValuesAsync(tw, type);
            await tw.WriteLineAsync("```");
        }
        else
        {
            var hasImplementedTypes =
                (type.BaseType != null && !CecilUtilities.IsObjectType(type.BaseType!)) ||
                type.Interfaces.Count >= 1;

            await tw.WriteLineAsync();
            await tw.WriteLineAsync("```csharp");
            await tw.WriteLineAsync($"namespace {type.Namespace};");
            await tw.WriteLineAsync();
            await tw.WriteLineAsync($"{CecilUtilities.GetModifierKeywordString(type)} {Naming.GetName(type)}{(hasImplementedTypes ? " :" : "")}");

            if (type.BaseType is { } baseType && !CecilUtilities.IsObjectType(baseType))
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
            await tw.WriteLineAsync($"    // Total members: {visibleMembers.OverallMembers.Length}");
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
                        await tw.WriteLineAsync(
                            $"| `{gan}` | {WriterUtilities.RenderDotNetXmlElement(description, true, hri)} |");
                    }
                    else
                    {
                        await tw.WriteLineAsync(
                            $"| `{gan}` | |");
                    }
                }
            }

            foreach (var gp in type.GenericParameters)
            {
                var gpn = Naming.GetName(gp);
                if (dotNetXmlType?.TypeParameters.FirstOrDefault(p => p.Name == gpn) is { } dotNetParameter &&
                    dotNetParameter.Description is { } description)
                {
                    await tw.WriteLineAsync(
                        $"| `{gpn}` | {WriterUtilities.RenderDotNetXmlElement(description, true, hri)} |");
                }
                else
                {
                    await tw.WriteLineAsync(
                        $"| `{gpn}` | |");
                }
            }
        }

        await WriteRemarksAsync(tw, dotNetXmlType, hri, ct);

        if (!isDelegateType && !isEnumType)
        {
            /////////////////////////////////////////////////////////
            // Member index table.

            if (visibleMembers.OverallMembers.Length >= 1)
            {
                await tw.WriteLineAsync();
                await tw.WriteLineAsync("|Member type|Members|");
                await tw.WriteLineAsync("|:----|:----|");

                if (visibleMembers.Fields.Length >= 1)
                {
                    var memberList = string.Join(", ", visibleMembers.Fields.
                        Select(f =>
                            hri.TryGetValue(FullNaming.GetFullName(f), out var id) ?
                            $"[ `{Naming.GetName(f)}` ](#{id})" :
                            $"`{Naming.GetName(f)}`"));
                    await tw.WriteLineAsync($"|Field| {memberList} |");
                }

                if (visibleMembers.Properties.Length >= 1)
                {
                    var memberList = string.Join(", ", visibleMembers.Properties.
                        Select(p =>
                            hri.TryGetValue(FullNaming.GetFullName(p), out var id) ?
                            $"[ `{Naming.GetName(p)}` ](#{id})" :
                            $"`{Naming.GetName(p)}`"));
                    await tw.WriteLineAsync($"|Property| {memberList} |");
                }

                if (visibleMembers.Events.Length >= 1)
                {
                    var memberList = string.Join(", ", visibleMembers.Properties.
                        Select(e =>
                            hri.TryGetValue(FullNaming.GetFullName(e), out var id) ?
                            $"[ `{Naming.GetName(e)}` ](#{id})" :
                            $"`{Naming.GetName(e)}`"));
                    await tw.WriteLineAsync($"|Event| {memberList} |");
                }

                if (visibleMembers.Methods.Length >= 1)
                {
                    var memberList = string.Join(", ", visibleMembers.Methods.
                        // Because all overload methods are same name.
                        DistinctBy(m => Naming.GetName(m)).
                        Select(m =>
                            hri.TryGetValue(FullNaming.GetFullName(m), out var id) ?
                            $"[ `{Naming.GetName(m, MethodForms.WithBraces)}` ](#{id})" :
                            $"`{Naming.GetName(m, MethodForms.WithBraces)}`"));
                    await tw.WriteLineAsync($"|Method| {memberList} |");
                }
            }

            /////////////////////////////////////////////////////////
            // Fields.

            foreach (var field in visibleMembers.Fields)
            {
                await tw.WriteLineAsync();
                await WriteFieldsAsync(tw, dotNetDocument, field, initialLevel, hri, ct);
            }

            /////////////////////////////////////////////////////////
            // Properties.

            foreach (var property in visibleMembers.Properties)
            {
                await tw.WriteLineAsync();
                await WritePropertyAsync(tw, dotNetDocument, property, initialLevel, hri, ct);
            }

            /////////////////////////////////////////////////////////
            // Events.

            foreach (var @event in visibleMembers.Events)
            {
                await tw.WriteLineAsync();
                await WriteEventAsync(tw, dotNetDocument, @event, initialLevel, hri, ct);
            }

            /////////////////////////////////////////////////////////
            // Methods.

            foreach (var method in visibleMembers.Methods)
            {
                await tw.WriteLineAsync();
                await WriteMethodAsync(tw, dotNetDocument, method, initialLevel, hri, ct);
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
        IReadOnlyDictionary<string, string> hri,
        CancellationToken ct)
    {
        await tw.WriteLineAsync(WriterUtilities.GetSectionString(
            initialLevel + 1,
            $"{WriterUtilities.EscapeSpecialCharacters(@namespace)} namespace"));

        /////////////////////////////////////////////////////////
        // Type index table.

        var types = CecilUtilities.GetTypes(assembly.MainModule).
            Where(t => t.Namespace == @namespace).
            ToArray();

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("|Type|Members|");
        await tw.WriteLineAsync("|:----|:----|");

        var membersByType = new Dictionary<TypeReference, VisibleMembers>();
        foreach (var type in types)
        {
            var visibleMembers = new VisibleMembers(type);

            var members = visibleMembers.OverallMembers;

            var memberList = string.Join(", ", members.
                // Because all overload methods are same name.
                DistinctBy(m => Naming.GetName(m)).
                Select(m => hri.TryGetValue(FullNaming.GetFullName(m), out var id) ? 
                    $"[ `{Naming.GetName(m, MethodForms.WithBraces)}` ](#{id})" :
                    $"`{Naming.GetName(m, MethodForms.WithBraces)}`"));
            if (memberList.Length >= 1)
            {
                await tw.WriteLineAsync(
                    hri.TryGetValue(FullNaming.GetFullName(type), out var id) ?
                        $"| [ `{Naming.GetName(type)}` ](#{id}) | {memberList} |" :
                        $"| `{Naming.GetName(type)}` | {memberList} |");
            }

            membersByType[type] = visibleMembers;
        }

        /////////////////////////////////////////////////////////
        // Types.

        foreach (var type in types)
        {
            await tw.WriteLineAsync();
            await WriteTypeAsync(
                tw, dotNetDocument, type, initialLevel, membersByType[type], hri, ct);
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
            WriterUtilities.GetSectionString(
                initialLevel,
                $"{WriterUtilities.EscapeSpecialCharacters(dotNetDocument.AssemblyName)} assembly"));

        /////////////////////////////////////////////////////////
        // Assembly metadata.

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("|Metadata|Value|");
        await tw.WriteLineAsync("|:----|:----|");
        await tw.WriteLineAsync(
            $"| `AssemblyVersion` | {WriterUtilities.GetPrettyPrintValue(assembly.Name.Version.ToString())} |");

        foreach (var ca in assembly.CustomAttributes.
            Where(ca => ca.ConstructorArguments.Count >= 1 && validAttributes.Contains(ca.AttributeType.FullName)))
        {
            var cas = string.Join(", ",
                ca.ConstructorArguments.Select(ca => WriterUtilities.GetPrettyPrintValue(ca.Value)));
            await tw.WriteLineAsync(
                $"| `{Naming.GetName(ca.AttributeType).Replace("Attribute", "")}` | {cas} |");
        }

        /////////////////////////////////////////////////////////
        // Retrieve hash reference identities.

        var hri = WriterUtilities.GeneratePandocFormedHashReferenceIdentities(assembly);

        /////////////////////////////////////////////////////////
        // Examine namespaces.

        var allTypes = CecilUtilities.GetTypes(assembly.MainModule);
        var namespaces =
            allTypes.
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
            var types = string.Join(", ",
                allTypes.
                Where(t => t.Namespace == @namespace).
                OrderBy(t => Naming.GetName(t)).
                Select(t => hri.TryGetValue(FullNaming.GetFullName(t), out var id) ?
                    $"[ `{Naming.GetName(t)}` ](#{id})" :
                    $"`{Naming.GetName(t)}`"));

            await tw.WriteLineAsync(
                hri.TryGetValue(@namespace, out var id) ?
                    $"| [ `{@namespace}` ](#{id}) | {types} |" :
                    $"| `{@namespace}` | {types} |");
        }

        /////////////////////////////////////////////////////////
        // Namespaces.

        foreach (var @namespace in namespaces)
        {
            await tw.WriteLineAsync();
            await WriteNamespaceAsync(
                tw, dotNetDocument, assembly, @namespace, initialLevel, hri, ct);
        }

        await tw.FlushAsync();
    }
}
