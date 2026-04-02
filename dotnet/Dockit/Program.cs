/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Dockit.Internal;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Dockit;

public static class Program
{
    public static Task<int> Main(string[] args) =>
        RunAsync(args, Console.Out, Console.Error);

    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter outputWriter,
        TextWriter errorWriter)
    {
        var commandLine = ParseArguments(args);
        if (commandLine.ErrorMessage is { } errorMessage)
        {
            await errorWriter.WriteLineAsync(errorMessage);
            await errorWriter.WriteLineAsync();
            WriteUsage(errorWriter);
            return 1;
        }

        if (commandLine.ShowHelp)
        {
            WriteUsage(outputWriter);
            return 0;
        }

        var assemblyPath = commandLine.AssemblyPath!;
        var markdownBasePath = commandLine.MarkdownBasePath!;
        var initialLevel = commandLine.InitialLevel;

        var referenceBasePath = Path.GetDirectoryName(assemblyPath)!;

        var dotNetXmlPath = Path.Combine(
            Path.GetDirectoryName(assemblyPath)!,
            Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");
        var markdownPath = Path.Combine(
            markdownBasePath,
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

    private static ParsedCommandLine ParseArguments(string[] args)
    {
        var showHelp = false;
        var initialLevel = 1;

        var options = CreateOptionSet(
            value => showHelp = value,
            value => initialLevel = value);

        List<string> remainingArguments;
        try
        {
            remainingArguments = options.Parse(args);
        }
        catch (OptionException ex)
        {
            return ParsedCommandLine.FromError(ex.Message);
        }

        if (showHelp)
        {
            return ParsedCommandLine.ForHelp();
        }

        if (remainingArguments.Count != 2)
        {
            return ParsedCommandLine.FromError(
                "Expected <assembly-path> and <output-directory>.");
        }

        if (initialLevel < 1)
        {
            return ParsedCommandLine.FromError(
                "Initial level must be 1 or greater.");
        }

        return ParsedCommandLine.FromValues(
            remainingArguments[0],
            remainingArguments[1],
            initialLevel);
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine(
            $"Dockit [{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyVersion}-{ThisAssembly.AssemblyMetadata.CommitId}]");
        writer.WriteLine(
            "Generate Markdown documentation from a .NET assembly and its XML documentation file.");
        writer.WriteLine(
            "Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)");
        writer.WriteLine(
            $"https://github.com/kekyo/Dockit");
        writer.WriteLine("License: Under MIT.");
        writer.WriteLine();
        writer.WriteLine("Usage: dockit-dotnet [options] <assembly-path> <output-directory>");
        writer.WriteLine("Options:");
        CreateOptionSet(
            _ => { },
            _ => { }).
            WriteOptionDescriptions(writer);
    }

    private static OptionSet CreateOptionSet(
        Action<bool> setShowHelp,
        Action<int> setInitialLevel) =>
        new()
        {
            {
                "h|help",
                "Show this message and exit.",
                value => setShowHelp(value is not null)
            },
            {
                "l|initial-level=",
                "Set the base heading level of the generated Markdown. The default is 1.",
                (int value) => setInitialLevel(value)
            },
        };

    private sealed class ParsedCommandLine
    {
        private ParsedCommandLine(
            bool showHelp,
            string? assemblyPath,
            string? markdownBasePath,
            int initialLevel,
            string? errorMessage)
        {
            this.ShowHelp = showHelp;
            this.AssemblyPath = assemblyPath;
            this.MarkdownBasePath = markdownBasePath;
            this.InitialLevel = initialLevel;
            this.ErrorMessage = errorMessage;
        }

        public bool ShowHelp { get; }

        public string? AssemblyPath { get; }

        public string? MarkdownBasePath { get; }

        public int InitialLevel { get; }

        public string? ErrorMessage { get; }

        public static ParsedCommandLine ForHelp() =>
            new(true, null, null, 1, null);

        public static ParsedCommandLine FromError(string errorMessage) =>
            new(false, null, null, 1, errorMessage);

        public static ParsedCommandLine FromValues(
            string assemblyPath,
            string markdownBasePath,
            int initialLevel) =>
            new(false, assemblyPath, markdownBasePath, initialLevel, null);
    }
}
