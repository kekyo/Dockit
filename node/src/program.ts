/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { mkdir } from 'node:fs/promises';
import { performance } from 'node:perf_hooks';
import { resolve } from 'node:path';
import { analyzeProject } from './internal/analyzer.js';
import {
  getMarkdownOutputPath,
  writeMarkdown,
} from './internal/markdown-writer.js';
import { loadPackageMetadata } from './internal/package-metadata.js';
import { git_commit_hash, version } from './generated/packageMetadata.js';

interface ParsedCommandLine {
  showHelp: boolean;
  showVersion: boolean;
  projectPath: string | undefined;
  outputDirectory: string | undefined;
  metadataPackageJsonPath: string | undefined;
  entryPaths: readonly string[];
  initialLevel: number;
  errorMessage: string | undefined;
}

const createParsedCommandLine = (
  values: Partial<ParsedCommandLine>
): ParsedCommandLine => ({
  showHelp: values.showHelp ?? false,
  showVersion: values.showVersion ?? false,
  projectPath: values.projectPath,
  outputDirectory: values.outputDirectory,
  metadataPackageJsonPath: values.metadataPackageJsonPath,
  entryPaths: values.entryPaths ?? [],
  initialLevel: values.initialLevel ?? 1,
  errorMessage: values.errorMessage,
});

const parseArguments = (args: readonly string[]): ParsedCommandLine => {
  let showHelp = false;
  let showVersion = false;
  let initialLevel = 1;
  let metadataPackageJsonPath: string | undefined;
  const entryPaths: string[] = [];
  const positionalArguments: string[] = [];

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];

    if (argument === '-h' || argument === '--help') {
      showHelp = true;
      continue;
    }

    if (argument === '-v' || argument === '--version') {
      showVersion = true;
      continue;
    }

    if (argument === '-l' || argument === '--initial-level') {
      const value = args[index + 1];
      if (value === undefined) {
        return createParsedCommandLine({
          errorMessage: 'Missing value for --initial-level.',
        });
      }
      initialLevel = Number.parseInt(value, 10);
      index += 1;
      continue;
    }

    if (argument.startsWith('--initial-level=')) {
      initialLevel = Number.parseInt(
        argument.slice('--initial-level='.length),
        10
      );
      continue;
    }

    if (argument === '-e' || argument === '--entry') {
      const value = args[index + 1];
      if (value === undefined) {
        return createParsedCommandLine({
          errorMessage: 'Missing value for --entry.',
        });
      }
      entryPaths.push(value);
      index += 1;
      continue;
    }

    if (argument.startsWith('--entry=')) {
      entryPaths.push(argument.slice('--entry='.length));
      continue;
    }

    if (argument === '--with-metadata') {
      const value = args[index + 1];
      if (value === undefined) {
        return createParsedCommandLine({
          errorMessage: 'Missing value for --with-metadata.',
        });
      }
      metadataPackageJsonPath = value;
      index += 1;
      continue;
    }

    if (argument.startsWith('--with-metadata=')) {
      metadataPackageJsonPath = argument.slice('--with-metadata='.length);
      continue;
    }

    if (argument.startsWith('-')) {
      return createParsedCommandLine({
        errorMessage: `Unknown option: ${argument}`,
      });
    }

    positionalArguments.push(argument);
  }

  if (showHelp) {
    return createParsedCommandLine({
      showHelp: true,
      metadataPackageJsonPath,
      entryPaths,
      initialLevel,
    });
  }

  if (showVersion) {
    return createParsedCommandLine({
      showVersion: true,
    });
  }

  if (!Number.isInteger(initialLevel) || initialLevel < 1) {
    return createParsedCommandLine({
      errorMessage: 'Initial level must be 1 or greater.',
    });
  }

  if (positionalArguments.length !== 2) {
    return createParsedCommandLine({
      errorMessage: 'Expected <project-path> and <output-directory>.',
    });
  }

  return createParsedCommandLine({
    projectPath: positionalArguments[0],
    outputDirectory: positionalArguments[1],
    metadataPackageJsonPath,
    entryPaths,
    initialLevel,
  });
};

const writeBanner = (writer: NodeJS.WritableStream): void => {
  writer.write(`Dockit [typescript] [${version}-${git_commit_hash}]\n`);
};

const writeUsage = (writer: NodeJS.WritableStream): void => {
  writeBanner(writer);
  writer.write(
    'Generate Markdown documentation from a TypeScript or JavaScript npm project.\n'
  );
  writer.write('Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)\n');
  writer.write('https://github.com/kekyo/Dockit\n');
  writer.write('License: Under MIT.\n');
  writer.write('\n');
  writer.write(
    'Usage: dockit-ts [options] <project-path> <output-directory>\n'
  );
  writer.write('Options:\n');
  writer.write('  -h, --help                 Show this message and exit.\n');
  writer.write(
    '  -v, --version              Show version information and exit.\n'
  );
  writer.write(
    '  -l VALUE, --initial-level=VALUE  Set the base heading level of the generated Markdown. The default is 1.\n'
  );
  writer.write(
    '  -e VALUE, --entry=VALUE    Add a source entry point. Can be specified multiple times.\n'
  );
  writer.write(
    '  --with-metadata=PATH       Read only the metadata table from the specified package.json file.\n'
  );
};

const formatElapsedTime = (elapsedMilliseconds: number): string =>
  `${elapsedMilliseconds.toFixed(3)} ms`;

/**
 * Runs the dockit-ts command line interface.
 */
export const run = async (
  args: readonly string[],
  outputWriter: NodeJS.WritableStream,
  errorWriter: NodeJS.WritableStream
): Promise<number> => {
  const commandLine = parseArguments(args);
  if (commandLine.errorMessage !== undefined) {
    errorWriter.write(`${commandLine.errorMessage}\n\n`);
    writeUsage(errorWriter);
    return 1;
  }

  if (commandLine.showHelp) {
    writeUsage(outputWriter);
    return 0;
  }

  if (commandLine.showVersion) {
    writeBanner(outputWriter);
    return 0;
  }

  const startedAt = performance.now();
  writeBanner(outputWriter);

  try {
    const projectPath = resolve(commandLine.projectPath!);
    const packageDocumentation = await analyzeProject(projectPath, {
      entryPaths: commandLine.entryPaths,
    });
    const packageMetadata = await loadPackageMetadata(projectPath, {
      packageJsonPath: commandLine.metadataPackageJsonPath,
    });
    const outputDirectory = resolve(commandLine.outputDirectory!);
    const markdownPath = getMarkdownOutputPath(
      outputDirectory,
      packageDocumentation.packageName
    );

    await mkdir(outputDirectory, { recursive: true });
    await writeMarkdown(
      markdownPath,
      packageDocumentation,
      packageMetadata,
      commandLine.initialLevel
    );

    outputWriter.write('Converted TypeScript --> Markdown\n');
    outputWriter.write(`Input project: ${projectPath}\n`);
    outputWriter.write(`Output markdown: ${markdownPath}\n`);
    outputWriter.write(
      `Elapsed time: ${formatElapsedTime(performance.now() - startedAt)}\n`
    );

    return 0;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    errorWriter.write(`${message}\n`);
    return 1;
  }
};
