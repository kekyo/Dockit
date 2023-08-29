/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Xml.Linq;
using System.Linq;

namespace Dockit.Internal;

internal sealed class DotNetXmlParameter
{
    public readonly string Name;
    public readonly XElement? Description;

    public DotNetXmlParameter(
        string name, XElement? description)
    {
        Name = name;
        Description = description;
    }

    public override string ToString() =>
        $"{Name}: {Description?.Value.Trim() ?? "(null)"}";
}

internal enum DotNetXmlMemberTypes
{
    Namespace,
    Type,
    Field,
    Property,
    Event,
    Method,
}

internal sealed class DotNetXmlMember
{
    public readonly DotNetXmlMemberTypes Type;
    public readonly string Name;
    public readonly XElement? Summary;
    public readonly DotNetXmlParameter[] TypeParameters;
    public readonly DotNetXmlParameter[] Parameters;
    public readonly XElement? Returns;
    public readonly XElement? Remarks;
    public readonly XElement? Example;
    public readonly XElement? SeeAlso;

    public DotNetXmlMember(
        DotNetXmlMemberTypes type,
        string name,
        XElement? summary,
        DotNetXmlParameter[] typeParameters,
        DotNetXmlParameter[] parameters,
        XElement? returns,
        XElement? remarks,
        XElement? example,
        XElement? seeAlso)
    {
        this.Type = type;
        this.Name = name;
        this.Summary = summary;
        this.TypeParameters = typeParameters;
        this.Parameters = parameters;
        this.Returns = returns;
        this.Remarks = remarks;
        this.Example = example;
        this.SeeAlso = seeAlso;
    }

    public override string ToString() =>
        $"{Type} {Name}: {Summary?.Value.Trim() ?? "(null)"}";
}

internal readonly struct DotNetXmlMemberKey : IEquatable<DotNetXmlMemberKey>
{
    public readonly DotNetXmlMemberTypes Type;
    public readonly string Name;

    public DotNetXmlMemberKey(
        DotNetXmlMemberTypes type, string name)
    {
        Type = type;
        Name = name;
    }

    public bool Equals(DotNetXmlMemberKey rhs) =>
        Type == rhs.Type &&
        Name == rhs.Name;

    bool IEquatable<DotNetXmlMemberKey>.Equals(DotNetXmlMemberKey rhs) =>
        rhs is { } && Equals(rhs);

    public override bool Equals(object? obj) =>
        obj is DotNetXmlMemberKey rhs && Equals(rhs);

    public override int GetHashCode()
    {
        var hashCode = Type.GetHashCode();
        hashCode = hashCode * 397 ^ Name.GetHashCode();
        return hashCode;
    }

    public override string ToString() =>
        $"{Type} {Name}";
}

internal sealed class DotNetXmlDocument
{
    public readonly string AssemblyName;
    public readonly Dictionary<DotNetXmlMemberKey, DotNetXmlMember> Members;

    public DotNetXmlDocument(
        string assemblyName, Dictionary<DotNetXmlMemberKey, DotNetXmlMember> members)
    {
        this.AssemblyName = assemblyName;
        this.Members = members;
    }

    public override string ToString() =>
        this.AssemblyName;

    public static async Task<DotNetXmlDocument> LoadAsync(
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
                    "N:" => DotNetXmlMemberTypes.Namespace,
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
                    var returns =
                        entry.memberElement.Element("returns") ??
                        entry.memberElement.Element("value");
                    var remarks = entry.memberElement.Element("remarks");
                    var example = entry.memberElement.Element("example");
                    var seealso = entry.memberElement.Element("seealso");

                    return new DotNetXmlMember(
                        entry.key.Type, entry.key.Name,
                        summary, typeParameters, parameters, returns, remarks, example, seealso);
                });

        return new DotNetXmlDocument(assemblyName, members);
    }
}

