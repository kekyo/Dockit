/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Dockit.Internal;
using System.IO;
using System.Threading.Tasks;

namespace Dockit;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var assemblyPath = args[0];
        var initialLevel = 1;

        var referenceBasePath = Path.GetDirectoryName(assemblyPath)!;

        var dotNetXmlPath = Path.Combine(
            Path.GetDirectoryName(assemblyPath)!,
            Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");
        var markdownPath = Path.Combine(
            Path.GetDirectoryName(assemblyPath)!,
            Path.GetFileNameWithoutExtension(assemblyPath) + ".md");

        /////////////////////////////////////////////////////////
        // Load artifacts.

        var dotnetDocumentTask = DotNetXmlDocument.LoadAsync(dotNetXmlPath, default);

        var resolver = new AssemblyResolver(new[] { referenceBasePath });
        using var assembly = resolver.ReadAssemblyFrom(assemblyPath);

        var dotNetDocument = await dotnetDocumentTask;

        /////////////////////////////////////////////////////////
        // Write markdown.

        await Writer.WriteMarkdownAsync(
            markdownPath, assembly, dotNetDocument, initialLevel,
            default);

        return 0;
    }
}
