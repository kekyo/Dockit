# Dockit

![Dockit](Images/Dockit.100.png)

[![Project Status: Active – The project has reached a stable, usable state and is being actively developed.](https://www.repostatus.org/badges/latest/active.svg)](https://www.repostatus.org/#active)

|Package|Link|
|:----|:----|
|dockit-cli (.NET CLI)|[![NuGet dockit-cli](https://img.shields.io/nuget/v/dockit-cli.svg?style=flat)](https://www.nuget.org/packages/dockit-cli)|
|dockit-cli (NPM CLI)|[![NPM dockit-cli](https://img.shields.io/npm/v/dockit-cli.svg)](https://www.npmjs.com/package/dockit-cli)|

----

[(For Japanese language/日本語はこちら)](./README_ja.md)

> Please note that this English version of the document was machine-translated and then partially edited, so it may contain inaccuracies.
> We welcome pull requests to correct any errors in the text.

## What is this?

Dockit is an automatic Markdown documentation generator.
This repository contains:

- a `.NET` generator for assemblies and XML documentation metadata
- a TypeScript generator for npm projects using the TypeScript compiler API

The advantage of Dockit is that it generates the document once in Markdown format
and then uses Pandoc to generate the document from Markdown.
This allows you to target a variety of output formats.

It is also much simpler to manage than other solutions,
as you simply install NuGet and it automatically generates the documentation for you.

----

## Install

### .NET

Install the `.NET` tooling via NuGet:

```bash
dotnet tool install -g dockit-cli
```

Or, pre-built .NET Framework binaries in [GitHub Release page](https://github.com/kekyo/Dockit/releases).

### TypeScript / JavaScript

Install the `NPM` package via npmjs:

```bash
npm install -g dockit-cli
```

----

## Usage

### .NET

The `.NET` generator accepts two positional arguments and optional flags:

```bash
dockit-dotnet [options] <assembly-path> <output-directory>
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
dockit-dotnet ./src/MyLibrary/bin/Release/net8.0/MyLibrary.dll ./docs/api
```

This writes `./docs/api/MyLibrary.md`.

Successful runs print the resolved input and output paths:

```text
Input assembly: /absolute/path/to/MyLibrary.dll
Input XML: /absolute/path/to/MyLibrary.xml
Output markdown: /absolute/path/to/docs/api/MyLibrary.md
Elapsed time: 123.456 ms
```

Generate documentation after a normal build:

```bash
dotnet build -c Release
dockit-dotnet ./MyLibrary/bin/Release/net8.0/MyLibrary.dll ./artifacts/docs
```

### TypeScript / JavaScript

The TypeScript generator accepts a package root path and an output directory:

```bash
dockit-ts [options] <project-path> <output-directory>
```

Available options:

- `-h`, `--help`: Show usage help.
- `-l VALUE`, `--initial-level=VALUE`: Set the base heading level of the generated Markdown. The default is `1`.
- `-e VALUE`, `--entry=VALUE`: Add a source entry point. Can be specified multiple times.

Before you run it, make sure that:

- The target directory contains `package.json`.
- The project is a TypeScript or JavaScript npm package.
- Exported declarations are reachable from the package entry points.
- A `tsconfig.json` or `jsconfig.json` is available when the project needs custom compiler settings.

When `package.json` does not expose source entry points directly, Dockit tries these strategies in order:

1. Explicit `--entry` values.
2. `package.json` `dockit.entryPoints`.
3. `package.json` `exports`, `types`, `typings`, `module`, `main`.
4. Conventional fallback files such as `./src/index.ts` and `./src/main.ts`.

For CLI-oriented packages, you can set custom entry points in `package.json`:

```json
{
  "dockit": {
    "entryPoints": {
      ".": "./src/index.ts",
      "./extra": "./src/extra.ts"
    }
  }
}
```

Generate Markdown from an npm package:

```bash
dockit-ts ./path/to/package ./docs/api
```

This writes `./docs/api/<package-name>.md`.

Successful runs print the resolved input and output paths:

```text
Input project: /absolute/path/to/package
Output markdown: /absolute/path/to/docs/api/<package-name>.md
Elapsed time: 123.456 ms
```

Generate Markdown from a CLI-style package that keeps source files under `src`:

```bash
dockit-ts --entry ./src/index.ts ./path/to/package ./docs/api
```

----

## Applications

Once you've generated Markdown using Dockit, you can use [pandoc](https://pandoc.org/) to convert it into other formats.
For example, you can generate Markdown from a .NET assembly and use pandoc to generate a PDF:

```bash
dockit-dotnet ./MyLibrary/bin/Release/net8.0/MyLibrary.dll ./docs
pandoc ./docs/MyLibrary.md -o ./docs/MyLibrary.pdf
```

Alternatively, you can convert the Markdown to HTML using pandoc and then generate a PDF using [wkhtmltopdf](https://wkhtmltopdf.org/).
The advantage of this method is that you can style the formatted PDF using CSS.
For this purpose, we have prepared a [sample CSS file](./assets/sample.css), so please use it as a reference.

However, since wkhtmltopdf is now deprecated, this method is no longer recommended.
Please use the following as a reference for converting HTML:

```bash
pandoc ./docs/MyLibrary.md --reference-links --reference-location=block -t html5 -c ./assets/sample.css --embed-resources --standalone --filter=mermaid-filter -o ./docs/MyLibrary.html
wkhtmltopdf -s A4 -T 23mm -B 28mm -L 20mm -R 20mm --disable-smart-shrinking --keep-relative-links --zoom 1.0 --footer-spacing 7 --footer-font-name "Noto Sans" --footer-font-size 8 --footer-left "Copyright (c) FooBar. CC-BY" --footer-right "[page]/[topage]" --outline ./docs/MyLibrary.html ./docs/MyLibrary.pdf
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

Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)

License under Apache-v2.
