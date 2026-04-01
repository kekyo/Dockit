# Dockit

![Dockit](Images/Dockit.100.png)

[![Project Status: Active – The project has reached a stable, usable state and is being actively developed.](https://www.repostatus.org/badges/latest/active.svg)](https://www.repostatus.org/#active)

|Package|Link|
|:----|:----|
|dockit-cli (.NET CLI)|[![NuGet dockit-cli](https://img.shields.io/nuget/v/dockit-cli.svg?style=flat)](https://www.nuget.org/packages/dockit-cli)|

----

## What is this?

Dockit is an automatic Markdown documentation generator, fetch from .NET XML comment/metadata.

The advantage of Dockit is that it generates the document once in Markdown format
and then uses Pandoc to generate the document from Markdown.
This allows you to target a variety of output formats.

It is also much simpler to manage than other solutions,
as you simply install NuGet and it automatically generates the documentation for you.

## Install

Install .NET tooling via NuGet:

```bash
dotnet tool install -g dockit-cli
```

Or, pre-built .NET Framework binaries in [GitHub Release page](https://github.com/kekyo/Dockit/releases).

## Usage

Dockit accepts two positional arguments and optional flags:

```bash
dockit [options] <assembly-path> <output-directory>
```

Available options:

- `-h`, `--help`: Show usage help.
- `-l VALUE`, `--initial-level=VALUE`: Set the base heading level of the generated Markdown. The default is `1`.

Before you run it, make sure that:

- The target assembly has already been built.
- XML documentation output is enabled for that project.
- The XML documentation file is placed next to the assembly with the same base name, such as `MyLibrary.dll` and `MyLibrary.xml`.
- Referenced assemblies are also available in the assembly directory so metadata can be resolved.

For SDK-style projects, the minimum setup is:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Generate Markdown from a library build output:

```bash
dockit ./src/MyLibrary/bin/Release/net8.0/MyLibrary.dll ./docs/api
```

This writes `./docs/api/MyLibrary.md`.

Generate documentation after a normal build:

```bash
dotnet build -c Release
dockit ./MyLibrary/bin/Release/net8.0/MyLibrary.dll ./artifacts/docs
```

Generate Markdown first, then convert it with Pandoc:

```bash
dockit ./MyLibrary/bin/Release/net8.0/MyLibrary.dll ./docs
pandoc ./docs/MyLibrary.md -o ./docs/MyLibrary.pdf
```

----

## Motivation

Unfortunately, in many (formal?) software development undertakings,
there are many projects that require documentation as a deliverable that is never read at all.
Modern software development environments, especially IDEs have improved greatly,
and source code parsing engines such as the Language Server Protocol,
allow help information to be displayed directly in the editor.

Interface specifications for software libraries are combined with metadata information and supplied by the LSP engine.
In the past, this same thing had to be done manually by humans, hence the reason for the existence of a "reference manual."

With this background, I do not see the value in increasing the output quality of this tool or making it more sophisticated.
Rather, I wanted to create a situation where the minimum effort is required to install only the NuGet package,
and the output is generated.

(Although care was taken to ensure that a certain level of documentation was generated)
It doesn't matter because no one will read it anyway.)

You just submit the reference document output by Dockit to your important (and not-so-important) customers :)

----

## License

Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)

License under Apache-v2.
