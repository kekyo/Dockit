# Dockit

[![Project Status: Active – The project has reached a stable, usable state and is being actively developed.](https://www.repostatus.org/badges/latest/active.svg)](https://www.repostatus.org/#active)

| Package               | Link                                                                                                                       |
| :-------------------- | :------------------------------------------------------------------------------------------------------------------------- |
| dockit-cli (.NET CLI) | [![NuGet dockit-cli](https://img.shields.io/nuget/v/dockit-cli.svg?style=flat)](https://www.nuget.org/packages/dockit-cli) |
| dockit-cli (NPM CLI)  | [![NPM dockit-cli](https://img.shields.io/npm/v/dockit-cli.svg)](https://www.npmjs.com/package/dockit-cli)                 |

---

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

---

## Install

### TypeScript / JavaScript

Install the `NPM` package via npmjs:

```bash
npm install -g dockit-cli
```

---

## Usage

### TypeScript / JavaScript

The TypeScript generator accepts a package root path and an output directory:

```bash
dockit-ts [options] <project-path> <output-directory>
```

Available options:

- `-h`, `--help`: Show usage help.
- `-l VALUE`, `--initial-level=VALUE`: Set the base heading level of the generated Markdown. The default is `1`.

Generate Markdown from an npm package:

```bash
dockit-ts ./path/to/package ./docs/api
```

This writes `./docs/api/<package-name>.md`.

Generate Markdown from a CLI-style package that keeps source files under `src`:

```bash
dockit-ts --entry ./src/index.ts ./path/to/package ./docs/api
```

### Documents

For more information, please visit repository and refer README: [Dockit](https://github.com/kekyo/Dockit)

---

## License

Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)

License under Apache-v2.
