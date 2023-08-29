# Dockit

![Dockit](Images/Dockit.100.png)

[![Project Status: WIP – Initial development is in progress, but there has not yet been a stable, usable release suitable for the public.](https://www.repostatus.org/badges/latest/wip.svg)](https://www.repostatus.org/#wip)
[![NuGet Dockit](https://img.shields.io/nuget/v/Dockit.svg?style=flat)](https://www.nuget.org/packages/Dockit)

----

## What is this?

Dockit is an automatic Markdown documentation generator, fetch from .NET XML comment/metadata.

The advantage of Dockit is that it generates the document once in Markdown format
and then uses Pandoc to generate the document from Markdown.
This allows you to target a variety of output formats.

It is also much simpler to manage than other solutions,
as you simply install NuGet and it automatically generates the documentation for you.

TODO:

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
