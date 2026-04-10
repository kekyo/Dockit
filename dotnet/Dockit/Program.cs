/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Dockit.Internal;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        if (commandLine.ShowVersion)
        {
            await outputWriter.WriteLineAsync(GetBanner());
            return 0;
        }

        var stopwatch = Stopwatch.StartNew();

        await outputWriter.WriteLineAsync(GetBanner());

        var assemblyPath = Path.GetFullPath(commandLine.AssemblyPath!);
        var markdownBasePath = Path.GetFullPath(commandLine.MarkdownBasePath!);
        var initialLevel = commandLine.InitialLevel;
        var visibilityOptions = commandLine.VisibilityOptions;
        var includeAssemblyAttributes = commandLine.IncludeAssemblyAttributes;

        var referenceBasePath = Path.GetDirectoryName(assemblyPath)!;

        var dotNetXmlPath = Path.ChangeExtension(assemblyPath, ".xml");
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
            markdownPath, assembly, dotNetDocument, initialLevel, visibilityOptions,
            includeAssemblyAttributes,
            default);

        await outputWriter.WriteLineAsync($"Converted .NET --> Markdown");
        await outputWriter.WriteLineAsync($"Input assembly: {assemblyPath}");
        await outputWriter.WriteLineAsync($"Input XML: {dotNetXmlPath}");
        await outputWriter.WriteLineAsync($"Output markdown: {markdownPath}");
        await outputWriter.WriteLineAsync($"Elapsed time: {FormatElapsedTime(stopwatch.Elapsed)}");

        return 0;
    }

    private static string FormatElapsedTime(TimeSpan elapsed) =>
        $"{elapsed.TotalMilliseconds:0.000} ms";

    private static string GetBanner() =>
        $"Dockit [{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyVersion}-{ThisAssembly.AssemblyMetadata.CommitId}]";

    private static ParsedCommandLine ParseArguments(string[] args)
    {
        var showHelp = false;
        var showVersion = false;
        var initialLevel = 1;
        var visibility = DocumentationAccessibility.Protected;
        var editorBrowsableVisibility = DocumentationEditorBrowsableVisibility.Advanced;
        var includeAssemblyAttributes = true;

        var options = CreateOptionSet(
            value => showHelp = value,
            value => showVersion = value,
            value => initialLevel = value,
            value => visibility = value,
            value => editorBrowsableVisibility = value,
            value => includeAssemblyAttributes = value);

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

        if (showVersion)
        {
            return ParsedCommandLine.ForVersion();
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
            initialLevel,
            new(visibility, editorBrowsableVisibility),
            includeAssemblyAttributes);
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine(GetBanner());
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
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { }).
            WriteOptionDescriptions(writer);
    }

    private static OptionSet CreateOptionSet(
        Action<bool> setShowHelp,
        Action<bool> setShowVersion,
        Action<int> setInitialLevel,
        Action<DocumentationAccessibility> setVisibility,
        Action<DocumentationEditorBrowsableVisibility> setEditorBrowsableVisibility,
        Action<bool> setIncludeAssemblyAttributes) =>
        new()
        {
            {
                "h|help",
                "Show this message and exit.",
                value => setShowHelp(value is not null)
            },
            {
                "v|version",
                "Show version information and exit.",
                value => setShowVersion(value is not null)
            },
            {
                "l|initial-level=",
                "Set the base heading level of the generated Markdown. The default is 1.",
                (int value) => setInitialLevel(value)
            },
            {
                "scope-visibility=",
                "Set the minimum accessibility to include: public, protected, protected-internal, internal, private-protected, private. The default is protected.",
                (string value) => setVisibility(ParseAccessibility(value))
            },
            {
                "editor-browsable-visibility=",
                "Set the EditorBrowsable visibility to include: normal, advanced, always. The default is advanced.",
                (string value) => setEditorBrowsableVisibility(ParseEditorBrowsableVisibility(value))
            },
            {
                "no-assembly-attributes",
                "Do not emit the assembly attribute csharp code block. The version table remains.",
                _ => setIncludeAssemblyAttributes(false)
            },
        };

    private static DocumentationAccessibility ParseAccessibility(string value) =>
        value switch
        {
            "public" => DocumentationAccessibility.Public,
            "protected-internal" => DocumentationAccessibility.ProtectedInternal,
            "protected" => DocumentationAccessibility.Protected,
            "internal" => DocumentationAccessibility.Internal,
            "private-protected" => DocumentationAccessibility.PrivateProtected,
            "private" => DocumentationAccessibility.Private,
            _ => throw new OptionException(
                $"Invalid value for --scope-visibility: '{value}'. Expected public, protected, protected-internal, internal, private-protected, or private.",
                "scope-visibility"),
        };

    private static DocumentationEditorBrowsableVisibility ParseEditorBrowsableVisibility(
        string value) =>
        value switch
        {
            "normal" => DocumentationEditorBrowsableVisibility.Normal,
            "advanced" => DocumentationEditorBrowsableVisibility.Advanced,
            "always" => DocumentationEditorBrowsableVisibility.Always,
            _ => throw new OptionException(
                $"Invalid value for --editor-browsable-visibility: '{value}'. Expected normal, advanced, or always.",
                "editor-browsable-visibility"),
        };

    private sealed class ParsedCommandLine
    {
        private ParsedCommandLine(
            bool showHelp,
            bool showVersion,
            string? assemblyPath,
            string? markdownBasePath,
            int initialLevel,
            DocumentationVisibilityOptions visibilityOptions,
            bool includeAssemblyAttributes,
            string? errorMessage)
        {
            this.ShowHelp = showHelp;
            this.ShowVersion = showVersion;
            this.AssemblyPath = assemblyPath;
            this.MarkdownBasePath = markdownBasePath;
            this.InitialLevel = initialLevel;
            this.VisibilityOptions = visibilityOptions;
            this.IncludeAssemblyAttributes = includeAssemblyAttributes;
            this.ErrorMessage = errorMessage;
        }

        public bool ShowHelp { get; }

        public bool ShowVersion { get; }

        public string? AssemblyPath { get; }

        public string? MarkdownBasePath { get; }

        public int InitialLevel { get; }

        public DocumentationVisibilityOptions VisibilityOptions { get; }

        public bool IncludeAssemblyAttributes { get; }

        public string? ErrorMessage { get; }

        public static ParsedCommandLine ForHelp() =>
            new(true, false, null, null, 1, DocumentationVisibilityOptions.Default, true, null);

        public static ParsedCommandLine ForVersion() =>
            new(false, true, null, null, 1, DocumentationVisibilityOptions.Default, true, null);

        public static ParsedCommandLine FromError(string errorMessage) =>
            new(false, false, null, null, 1, DocumentationVisibilityOptions.Default, true, errorMessage);

        public static ParsedCommandLine FromValues(
            string assemblyPath,
            string markdownBasePath,
            int initialLevel,
            DocumentationVisibilityOptions visibilityOptions,
            bool includeAssemblyAttributes) =>
            new(false, false, assemblyPath, markdownBasePath, initialLevel, visibilityOptions, includeAssemblyAttributes, null);
    }
}
