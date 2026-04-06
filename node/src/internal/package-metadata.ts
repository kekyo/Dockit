/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { execFile } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { promisify } from 'node:util';
import { normalizeInlineText } from './text.js';

interface PackageJsonAuthor {
  name?: unknown;
  email?: unknown;
  url?: unknown;
}

interface PackageJson {
  name?: unknown;
  version?: unknown;
  description?: unknown;
  author?: unknown;
  license?: unknown;
  keywords?: unknown;
  type?: unknown;
  main?: unknown;
  module?: unknown;
  types?: unknown;
  typings?: unknown;
}

interface GitCommitMetadata {
  hash: string | undefined;
  date: string | undefined;
  message: string | undefined;
}

const execFileAsync = promisify(execFile);

/**
 * Describes a scalar metadata value rendered in the package summary table.
 */
export type PackageMetadataScalar = string | readonly string[] | undefined;

/**
 * Describes one metadata node in the package metadata tree.
 */
export type PackageMetadataNode = PackageMetadataScalar | PackageMetadataObject;

/**
 * Describes a nested metadata object flattened into dotted keys for rendering.
 */
export interface PackageMetadataObject {
  [key: string]: PackageMetadataNode;
}

/**
 * Describes the metadata rows rendered ahead of the generated documentation.
 */
export interface PackageMetadata {
  /** Root metadata object rendered into the summary table. */
  value: PackageMetadataObject;
}

/**
 * Configures how package metadata is loaded for the Markdown metadata table.
 */
export interface LoadPackageMetadataOptions {
  /** Overrides the package.json file used only for metadata table rendering. */
  packageJsonPath?: string;
  /** Overrides the generated build date, primarily for deterministic tests. */
  buildDate?: Date;
}

const readPackageJson = async (packageJsonPath: string): Promise<PackageJson> =>
  JSON.parse(await readFile(packageJsonPath, 'utf8')) as PackageJson;

const getStringValue = (value: unknown): string | undefined =>
  typeof value === 'string' && value.trim().length >= 1
    ? value.trim()
    : undefined;

const getStringArrayValue = (value: unknown): readonly string[] | undefined => {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const values = value
    .map((entry) => getStringValue(entry))
    .filter((entry): entry is string => entry !== undefined);
  return values.length >= 1 ? values : undefined;
};

const formatAuthor = (value: unknown): string | undefined => {
  const authorText = getStringValue(value);
  if (authorText !== undefined) {
    return authorText;
  }

  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return undefined;
  }

  const author = value as PackageJsonAuthor;
  const segments: string[] = [];
  const name = getStringValue(author.name);
  const email = getStringValue(author.email);
  const url = getStringValue(author.url);

  if (name !== undefined) {
    segments.push(name);
  }
  if (email !== undefined) {
    segments.push(`<${email}>`);
  }
  if (url !== undefined) {
    segments.push(`(${url})`);
  }

  return segments.length >= 1 ? segments.join(' ') : undefined;
};

const readGitLines = async (
  cwd: string,
  args: readonly string[]
): Promise<readonly string[]> => {
  try {
    const { stdout } = await execFileAsync('git', [...args], { cwd });
    return stdout
      .replace(/\r\n?/gu, '\n')
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length >= 1);
  } catch {
    return [];
  }
};

const readGitCommitMetadata = async (
  cwd: string
): Promise<GitCommitMetadata> => {
  try {
    const { stdout } = await execFileAsync(
      'git',
      ['log', '-1', '--format=%H%n%cI%n%B'],
      { cwd }
    );
    const [rawHash = '', rawDate = '', ...rawMessageLines] = stdout
      .replace(/\r\n?/gu, '\n')
      .split('\n');
    const message = normalizeInlineText(rawMessageLines.join('\n'));

    return {
      hash: rawHash.trim().length >= 1 ? rawHash.trim() : undefined,
      date: rawDate.trim().length >= 1 ? rawDate.trim() : undefined,
      message: message.length >= 1 ? message : undefined,
    };
  } catch {
    return {
      hash: undefined,
      date: undefined,
      message: undefined,
    };
  }
};

/**
 * Loads package metadata for the Markdown metadata table.
 *
 * @param projectPath Project root used when `packageJsonPath` is omitted.
 * @param options Metadata loading options.
 * @returns Ordered metadata rows for Markdown rendering.
 * @remarks
 * When `packageJsonPath` is omitted, this reads `<projectPath>/package.json`.
 * Git-related rows are resolved from the directory that contains the selected
 * package.json file. Missing Git data is rendered as empty metadata values.
 */
export const loadPackageMetadata = async (
  projectPath: string,
  options: LoadPackageMetadataOptions = {}
): Promise<PackageMetadata> => {
  const packageJsonPath =
    options.packageJsonPath === undefined
      ? resolve(projectPath, 'package.json')
      : resolve(options.packageJsonPath);
  const packageJson = await readPackageJson(packageJsonPath);
  const gitWorkingDirectory = dirname(packageJsonPath);
  const gitCommitMetadata = await readGitCommitMetadata(gitWorkingDirectory);
  const gitTags =
    gitCommitMetadata.hash === undefined
      ? undefined
      : await readGitLines(gitWorkingDirectory, [
          'tag',
          '--points-at',
          gitCommitMetadata.hash,
        ]);
  const gitBranches =
    gitCommitMetadata.hash === undefined
      ? undefined
      : await readGitLines(gitWorkingDirectory, [
          'branch',
          '--format=%(refname:short)',
          '--contains',
          gitCommitMetadata.hash,
        ]);

  return {
    value: {
      name: getStringValue(packageJson.name),
      version: getStringValue(packageJson.version),
      description: getStringValue(packageJson.description),
      author: formatAuthor(packageJson.author),
      license: getStringValue(packageJson.license),
      keywords: getStringArrayValue(packageJson.keywords),
      type: getStringValue(packageJson.type),
      main: getStringValue(packageJson.main),
      module: getStringValue(packageJson.module),
      types: getStringValue(packageJson.types ?? packageJson.typings),
      git: {
        tags: gitTags,
        branches: gitBranches,
        commit: {
          hash: gitCommitMetadata.hash,
          date: gitCommitMetadata.date,
          message: gitCommitMetadata.message,
        },
      },
      buildDate: (options.buildDate ?? new Date()).toISOString(),
    },
  };
};
