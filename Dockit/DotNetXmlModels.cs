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
using System.Xml.Linq;

namespace Dockit;

internal sealed class DotNetXmlParameter
{
    public readonly string Name;
    public readonly XElement? Description;

    public DotNetXmlParameter(
        string name, XElement? description)
    {
        this.Name = name;
        this.Description = description;
    }

    public override string ToString() =>
        $"{this.Name}: {this.Description?.Value.Trim() ?? "(null)"}";
}

internal enum DotNetXmlMemberTypes
{
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
    public readonly XElement? Examples;

    public DotNetXmlMember(
        DotNetXmlMemberTypes type,
        string name,
        XElement? summary,
        DotNetXmlParameter[] typeParameters,
        DotNetXmlParameter[] parameters,
        XElement? returns,
        XElement? remarks,
        XElement? examples)
    {
        this.Type = type;
        this.Name = name;
        this.Summary = summary;
        this.TypeParameters = typeParameters;
        this.Parameters = parameters;
        this.Returns = returns;
        this.Remarks = remarks;
        this.Examples = examples;
    }

    public override string ToString() =>
        $"{this.Type} {this.Name}: {this.Summary?.Value.Trim() ?? "(null)"}";
}

internal readonly struct DotNetXmlMemberKey : IEquatable<DotNetXmlMemberKey>
{
    public readonly DotNetXmlMemberTypes Type;
    public readonly string Name;

    public DotNetXmlMemberKey(
        DotNetXmlMemberTypes type, string name)
    {
        this.Type = type;
        this.Name = name;
    }

    public bool Equals(DotNetXmlMemberKey rhs) =>
        this.Type == rhs.Type &&
        this.Name == rhs.Name;

    bool IEquatable<DotNetXmlMemberKey>.Equals(DotNetXmlMemberKey rhs) =>
        rhs is { } && this.Equals(rhs);

    public override bool Equals(object? obj) =>
        obj is DotNetXmlMemberKey rhs && this.Equals(rhs);

    public override int GetHashCode()
    {
        var hashCode = this.Type.GetHashCode();
        hashCode = (hashCode * 397) ^ this.Name.GetHashCode();
        return hashCode;
    }

    public override string ToString() =>
        $"{this.Type} {this.Name}";
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
}

