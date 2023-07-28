/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dockit;

public static class Program
{
    private static async Task<DotNetXmlDocument> LoadDotNetXmlDocumentAsync(
        string path, CancellationToken ct)
    {
        using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);

        var xDocument =
#if NETFRAMEWORK || NETSTANDARD2_0
            await Task.Run(() => XDocument.Load(fs, LoadOptions.None));
#else
            await XDocument.LoadAsync(fs, LoadOptions.None, ct);
#endif

        var assemblyName = xDocument.Root!.
            Element("assembly")!.
            Element("name")!.
            Value;

        var members = xDocument.Root!.
            Element("members")!.
            Elements("member").
            Select(memberElement =>
            {
                var memberName = memberElement.
                   Attribute("name")!.
                   Value;

                var type = memberName.Substring(0, 2) switch
                {
                    "T:" => DotNetXmlMemberTypes.Type,
                    "F:" => DotNetXmlMemberTypes.Field,
                    "M:" => DotNetXmlMemberTypes.Method,
                    "P:" => DotNetXmlMemberTypes.Property,
                    "E:" => DotNetXmlMemberTypes.Event,
                    _ => throw new InvalidDataException(),
                };
                var name = memberName.Substring(2);

                return new { key = new DotNetXmlMemberKey(type, name), memberElement, };
            }).
            ToDictionary(
                entry => entry.key,
                entry =>
                {
                    var summary = entry.memberElement.Element("summary");
                    var typeParameters = entry.memberElement.
                        Elements("typeparam").
                        Select(paramElement =>
                        {
                            var name = paramElement.
                                Attribute("name")!.
                                Value;
                            return new DotNetXmlParameter(name, paramElement);
                        }).
                        ToArray();
                    var parameters = entry.memberElement.
                        Elements("param").
                        Select(paramElement =>
                        {
                            var name = paramElement.
                                Attribute("name")!.
                                Value;
                            return new DotNetXmlParameter(name, paramElement);
                        }).
                        ToArray();
                    var returns = entry.memberElement.Element("returns");
                    var remarks = entry.memberElement.Element("remarks");
                    var examples = entry.memberElement.Element("examples");

                    return new DotNetXmlMember(
                        entry.key.Type, entry.key.Name,
                        summary, typeParameters, parameters, returns, remarks, examples);
                });

        return new DotNetXmlDocument(assemblyName, members);
    }

    private static async Task WriteMarkdownAsync(
        string markdownPath,
        AssemblyDefinition assembly,
        DotNetXmlDocument dotNetDocument,
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

        await tw.WriteLineAsync($"## {dotNetDocument.AssemblyName} assembly");

        /////////////////////////////////////////////////////////
        // Examine namespaces.

        var namespaces = assembly.MainModule.Types.
            Where(t => t.IsPublic).
            Select(t => t.Namespace).
            Distinct().
            OrderBy(ns => ns).
            ToArray();

        /////////////////////////////////////////////////////////
        // Namespace index.

        await tw.WriteLineAsync();
        await tw.WriteLineAsync("|Namespace|Types|");
        await tw.WriteLineAsync("|:----|:----|");

        foreach (var ns in namespaces)
        {
            var types = string.Join(",", assembly.MainModule.Types.
                Where(t => t.Namespace == ns).
                OrderBy(t => t.Name).
                Select(t => $"`{t.Name}`"));
            await tw.WriteLineAsync($"|`{ns}`|{types}|");
        }

        /////////////////////////////////////////////////////////
        // Namespaces.

        foreach (var ns in namespaces)
        {
            await tw.WriteLineAsync();
            await tw.WriteLineAsync($"### {ns} namespace");

            /////////////////////////////////////////////////////////
            // Types.

            foreach (var type in assembly.MainModule.Types.
                Where(t => t.Namespace == ns).
                OrderBy(t => t.Name))
            {
                var isDelegateType = Utilities.IsDelegateType(type);

                await tw.WriteLineAsync();
                await tw.WriteLineAsync($"#### {type.Name} {(isDelegateType ? "delegate" : "type")}");

                // TODO: generic type arguments
                if (dotNetDocument.Members.TryGetValue(
                    new(DotNetXmlMemberTypes.Type, Utilities.GetFullName(type)),
                    out var dotNetXmlType))
                {
                    if (dotNetXmlType.Summary is { } summary)
                    {
                        await tw.WriteLineAsync();
                        await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(summary));
                    }

                    if (dotNetXmlType.Remarks is { } remarks)
                    {
                        await tw.WriteLineAsync();
                        await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(remarks));
                    }
                }

                if (isDelegateType)
                {
                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync("```csharp");
                    await Utilities.WriteSignatureAsync(tw, type);
                    await tw.WriteLineAsync("```");
                    continue;
                }

                var hasImplementedTypes =
                    (type.BaseType != null && !Utilities.IsObjectType(type.BaseType!)) || type.Interfaces.Count >= 1;

                await tw.WriteLineAsync();
                await tw.WriteLineAsync("```csharp");
                await tw.WriteLineAsync($"{Utilities.GetModifier(type)} {Utilities.GetFullName(type)}{(hasImplementedTypes ? " :" : "")}");
                
                if (type.BaseType is { } baseType && !Utilities.IsObjectType(baseType))
                {
                    if (type.Interfaces.Count >= 1)
                    {
                        await tw.WriteLineAsync($"    {Utilities.GetFullName(baseType)},");
                    }
                    else
                    {
                        await tw.WriteLineAsync($"    {Utilities.GetFullName(baseType)}");
                    }
                }
                for (var ifsIndex = 0; ifsIndex < type.Interfaces.Count; ifsIndex++)
                {
                    var ifs = type.Interfaces[ifsIndex];
                    if (ifsIndex < type.Interfaces.Count - 1)
                    {
                        await tw.WriteLineAsync($"    {Utilities.GetFullName(ifs.InterfaceType)},");
                    }
                    else
                    {
                        await tw.WriteLineAsync($"    {Utilities.GetFullName(ifs.InterfaceType)}");
                    }
                }

                await tw.WriteLineAsync("```");

                /////////////////////////////////////////////////////////
                // Fields.

                foreach (var field in type.Fields.
                    Where(f => f.IsPublic).
                    OrderBy(f => f.Name))
                {
                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync($"##### {field.Name} field");

                    // TODO: generic type arguments
                    if (dotNetDocument.Members.TryGetValue(
                        new(DotNetXmlMemberTypes.Field, Utilities.GetFullName(field)),
                        out var dotNetXmlField))
                    {
                        if (dotNetXmlField.Summary is { } memberSummary)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary));
                        }

                        if (dotNetXmlField.Remarks is { } memberRemarks)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks));
                        }
                    }

                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync("```csharp");
                    await tw.WriteLineAsync($"{Utilities.GetModifier(field)} {Utilities.GetFullName(field.FieldType)} {Utilities.GetFullName(field)}");
                    await tw.WriteLineAsync("```");
                }

                /////////////////////////////////////////////////////////
                // Properties.

                // TODO: indexer

                foreach (var property in type.Properties.
                    Where(p => (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false)).
                    OrderBy(p => p.Name))
                {
                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync($"##### {property.Name} property");

                    // TODO: generic type arguments
                    if (dotNetDocument.Members.TryGetValue(
                        new(DotNetXmlMemberTypes.Property, Utilities.GetFullName(property)),
                        out var dotNetXmlProperty))
                    {
                        if (dotNetXmlProperty.Summary is { } memberSummary)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary));
                        }

                        if (dotNetXmlProperty.Remarks is { } memberRemarks)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks));
                        }
                    }

                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync("```csharp");
                    var getter = property.GetMethod is { } mg ? $" {Utilities.GetPropertyEventModifier(mg)} get;" : "";
                    var setter = property.SetMethod is { } ms ? $" {Utilities.GetPropertyEventModifier(ms)} set;" : "";
                    await tw.WriteLineAsync($"{Utilities.GetFullName(property.PropertyType)} {Utilities.GetFullName(property)} {{{getter}{setter} }}");
                    await tw.WriteLineAsync("```");
                }

                /////////////////////////////////////////////////////////
                // Events.

                foreach (var @event in type.Events.
                    Where(e => (e.AddMethod?.IsPublic ?? false) || (e.RemoveMethod?.IsPublic ?? false)).
                    OrderBy(e => e.Name))
                {
                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync($"##### {@event.Name} property");

                    // TODO: generic type arguments
                    if (dotNetDocument.Members.TryGetValue(
                        new(DotNetXmlMemberTypes.Event, Utilities.GetFullName(@event)),
                        out var dotNetXmlEvent))
                    {
                        if (dotNetXmlEvent.Summary is { } memberSummary)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary));
                        }

                        if (dotNetXmlEvent.Remarks is { } memberRemarks)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks));
                        }
                    }

                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync("```csharp");
                    var adder = @event.AddMethod is { } ma ? $" {Utilities.GetPropertyEventModifier(ma)} add;" : "";
                    var remover = @event.RemoveMethod is { } mr ? $" {Utilities.GetPropertyEventModifier(mr)} remove;" : "";
                    await tw.WriteLineAsync($"event {Utilities.GetFullName(@event.EventType)} {Utilities.GetFullName(@event)} {{{adder}{remover} }}");
                    await tw.WriteLineAsync("```");
                }

                /////////////////////////////////////////////////////////
                // Methods.

                // TODO: interface implementation

                foreach (var method in type.Methods.
                    Where(m => m.IsPublic).
                    OrderBy(m => m.Name))
                {
                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync($"##### {method.Name} method");

                    // TODO: generic type arguments
                    if (dotNetDocument.Members.TryGetValue(
                        new(DotNetXmlMemberTypes.Method, Utilities.GetFullName(method)),
                        out var dotNetXmlMethod))
                    {
                        if (dotNetXmlMethod.Summary is { } memberSummary)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberSummary));
                        }

                        if (dotNetXmlMethod.Remarks is { } memberRemarks)
                        {
                            await tw.WriteLineAsync();
                            await tw.WriteLineAsync(Utilities.RenderDotNetXmlElement(memberRemarks));
                        }
                    }

                    await tw.WriteLineAsync();
                    await tw.WriteLineAsync("```csharp");
                    await Utilities.WriteSignatureAsync(tw, method);
                    await tw.WriteLineAsync("```");
                }
            }
        }

        await tw.FlushAsync();
    }

    public static async Task<int> Main(string[] args)
    {
        var assemblyPath = args[0];
        var referenceBasePath = Path.GetDirectoryName(assemblyPath)!;

        var dotNetXmlPath = Path.Combine(
            Path.GetDirectoryName(assemblyPath)!,
            Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");
        var markdownPath = Path.Combine(
            Path.GetDirectoryName(assemblyPath)!,
            Path.GetFileNameWithoutExtension(assemblyPath) + ".md");

        /////////////////////////////////////////////////////////
        // Load artifacts.

        var dotnetDocumentTask = LoadDotNetXmlDocumentAsync(dotNetXmlPath, default);

        var resolver = new AssemblyResolver(new[] { referenceBasePath });
        using var assembly = resolver.ReadAssemblyFrom(assemblyPath);

        var dotNetDocument = await dotnetDocumentTask;

        /////////////////////////////////////////////////////////

        await WriteMarkdownAsync(markdownPath, assembly, dotNetDocument, default);

        return 0;
    }
}
