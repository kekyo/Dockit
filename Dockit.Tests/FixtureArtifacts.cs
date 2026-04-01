using Dockit.Internal;
using Mono.Cecil;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dockit.Tests;

internal static class FixtureArtifacts
{
    private static readonly Lazy<string> repositoryRoot = new(FindRepositoryRoot);
    private static readonly Lazy<string> assemblyPath = new(() => FindProjectOutput("Dockit.TestAssembly.dll"));
    private static readonly Lazy<string> xmlPath = new(() => Path.ChangeExtension(AssemblyPath, ".xml")!);

    public static string RepositoryRoot => repositoryRoot.Value;

    public static string AssemblyPath => assemblyPath.Value;

    public static string XmlPath => xmlPath.Value;

    public static AssemblyDefinition ReadAssembly()
    {
        var resolver = new AssemblyResolver(new[] { Path.GetDirectoryName(AssemblyPath)! });
        return resolver.ReadAssemblyFrom(AssemblyPath);
    }

    public static Task<DotNetXmlDocument> LoadDocumentAsync() =>
        DotNetXmlDocument.LoadAsync(XmlPath, CancellationToken.None);

    public static TypeDefinition GetTopLevelType(AssemblyDefinition assembly, string @namespace, string name) =>
        assembly.MainModule.Types.Single(type =>
            type.Namespace == @namespace &&
            type.Name == name);

    public static TypeDefinition GetNestedType(
        AssemblyDefinition assembly,
        string @namespace,
        string declaringTypeName,
        string nestedTypeName)
    {
        var declaringType = GetTopLevelType(assembly, @namespace, declaringTypeName);
        return declaringType.NestedTypes.Single(type => type.Name == nestedTypeName);
    }

    public static string GetXmlNameBySummary(
        DotNetXmlDocument document,
        DotNetXmlMemberTypes type,
        string summary)
    {
        return document.Members.
            Single(entry =>
                entry.Key.Type == type &&
                Normalize(entry.Value.Summary?.Value) == summary).
            Key.Name;
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            text.Replace("\r", string.Empty).
                Split('\n').
                Select(line => line.Trim()).
                Where(line => line.Length >= 1));
    }

    public static async Task<DotNetXmlDocument> LoadDocumentFromStringAsync(string xml)
    {
        var path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"fixture-{Guid.NewGuid():N}.xml");

        await File.WriteAllTextAsync(path, xml);
        try
        {
            return await DotNetXmlDocument.LoadAsync(path, CancellationToken.None);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FindProjectOutput(string fileName)
    {
        var projectDirectory = Path.Combine(RepositoryRoot, "Dockit.TestAssembly");
        var path = Directory.EnumerateFiles(projectDirectory, fileName, SearchOption.AllDirectories).
            Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.Ordinal)).
            OrderByDescending(File.GetLastWriteTimeUtc).
            FirstOrDefault();

        return path ??
            throw new FileNotFoundException($"Fixture artifact was not found: {fileName}", projectDirectory);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            var solutionPath = Path.Combine(current.FullName, "Dockit.sln");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Dockit.sln was not found from the current test directory.");
    }
}
