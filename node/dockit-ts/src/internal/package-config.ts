/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import {
  dirname,
  extname,
  isAbsolute,
  join,
  relative,
  resolve,
} from 'node:path';
import ts from 'typescript';

interface DockitPackageConfiguration {
  entryPoints?: unknown;
}

interface PackageJson {
  name?: string;
  version?: string;
  description?: string;
  exports?: unknown;
  main?: string;
  module?: string;
  types?: string;
  typings?: string;
  dockit?: DockitPackageConfiguration;
}

/**
 * Describes one resolved package export entry.
 */
export interface PackageEntryPoint {
  exportPath: string;
  sourceFilePath: string;
}

/**
 * Describes the resolved configuration required to analyze a package.
 */
export interface ProjectConfiguration {
  packageName: string;
  packageVersion: string | undefined;
  packageDescription: string | undefined;
  packageRootPath: string;
  entryPoints: readonly PackageEntryPoint[];
  program: ts.Program;
}

export interface LoadProjectConfigurationOptions {
  entryPaths?: readonly string[];
}

interface RawEntryPointCandidate {
  exportPath: string;
  targetPath: string;
  source: string;
}

interface EntryPointResolutionAttempt {
  candidate: RawEntryPointCandidate;
  attemptedPaths: readonly string[];
  sourceFilePath: string | undefined;
}

interface PathMappingHints {
  outDirs: readonly string[];
  sourceRoots: readonly string[];
}

const supportedSourceExtensions = [
  '.ts',
  '.tsx',
  '.mts',
  '.cts',
  '.js',
  '.jsx',
  '.mjs',
  '.cjs',
];

const fallbackEntryTargets = [
  './index.ts',
  './index.tsx',
  './index.js',
  './index.jsx',
  './index.mjs',
  './index.cjs',
  './src/index.ts',
  './src/index.tsx',
  './src/index.js',
  './src/index.jsx',
  './src/index.mjs',
  './src/index.cjs',
  './src/main.ts',
  './src/main.tsx',
  './src/main.js',
  './src/main.jsx',
  './src/main.mjs',
  './src/main.cjs',
] as const;

const defaultOutDirs = ['dist', 'build', 'lib', 'out'] as const;
const defaultSourceRoots = ['src', '.'] as const;

const normalizeSlashes = (value: string): string => value.replaceAll('\\', '/');

const normalizeExportPath = (value: string): string => {
  const normalizedValue = normalizeSlashes(value).trim();
  if (
    normalizedValue.length === 0 ||
    normalizedValue === '.' ||
    normalizedValue === './'
  ) {
    return '.';
  }
  return normalizedValue.startsWith('.')
    ? normalizedValue
    : `./${normalizedValue}`;
};

const stripKnownSourceExtension = (value: string): string => {
  if (value.endsWith('.d.mts')) {
    return value.slice(0, -'.d.mts'.length);
  }
  if (value.endsWith('.d.cts')) {
    return value.slice(0, -'.d.cts'.length);
  }
  if (value.endsWith('.d.ts')) {
    return value.slice(0, -'.d.ts'.length);
  }

  const extension = extname(value);
  return extension.length >= 1 ? value.slice(0, -extension.length) : value;
};

const normalizeRelativeDirectory = (value: string): string => {
  const normalizedValue = normalizeSlashes(value)
    .trim()
    .replace(/^\.\/+/u, '');
  if (
    normalizedValue.length === 0 ||
    normalizedValue === '.' ||
    normalizedValue === './'
  ) {
    return '.';
  }
  return normalizedValue.replace(/\/+$/u, '');
};

const uniqueValues = (values: readonly string[]): readonly string[] => [
  ...new Set(values.map((value) => normalizeRelativeDirectory(value))),
];

const deriveExportPathFromTargetPath = (
  packageRootPath: string,
  rawTargetPath: string
): string => {
  const absoluteTargetPath =
    rawTargetPath.startsWith('.') || !isAbsolute(rawTargetPath)
      ? resolve(packageRootPath, rawTargetPath)
      : rawTargetPath;
  let relativeTargetPath = normalizeSlashes(
    relative(packageRootPath, absoluteTargetPath)
  );
  relativeTargetPath = stripKnownSourceExtension(relativeTargetPath);

  if (relativeTargetPath.startsWith('src/')) {
    relativeTargetPath = relativeTargetPath.slice('src/'.length);
  }

  if (
    relativeTargetPath === '' ||
    relativeTargetPath === 'index' ||
    relativeTargetPath === 'main'
  ) {
    return '.';
  }
  if (relativeTargetPath.endsWith('/index')) {
    relativeTargetPath = relativeTargetPath.slice(0, -'/index'.length);
  }
  if (relativeTargetPath.endsWith('/main')) {
    relativeTargetPath = relativeTargetPath.slice(0, -'/main'.length);
  }

  return relativeTargetPath.length === 0 ? '.' : `./${relativeTargetPath}`;
};

const readPackageJson = async (projectPath: string): Promise<PackageJson> => {
  const packageJsonPath = resolve(projectPath, 'package.json');
  const text = await readFile(packageJsonPath, 'utf8');
  return JSON.parse(text) as PackageJson;
};

const loadPathMappingHints = async (
  packageRootPath: string
): Promise<PathMappingHints> => {
  const sourceRoots = new Set<string>(defaultSourceRoots);
  const outDirs = new Set<string>(defaultOutDirs);

  const configPath =
    ts.findConfigFile(packageRootPath, ts.sys.fileExists, 'tsconfig.json') ??
    ts.findConfigFile(packageRootPath, ts.sys.fileExists, 'jsconfig.json');
  if (configPath === undefined) {
    return {
      outDirs: uniqueValues([...outDirs]),
      sourceRoots: uniqueValues([...sourceRoots]),
    };
  }

  const configFile = ts.readConfigFile(configPath, ts.sys.readFile);
  if (configFile.error !== undefined || configFile.config === undefined) {
    return {
      outDirs: uniqueValues([...outDirs]),
      sourceRoots: uniqueValues([...sourceRoots]),
    };
  }

  const configDirectory = dirname(configPath);
  const compilerOptions =
    configFile.config !== null && typeof configFile.config === 'object'
      ? (configFile.config.compilerOptions as
          | Record<string, unknown>
          | undefined)
      : undefined;

  const addRelativeHint = (
    collection: Set<string>,
    rawValue: unknown
  ): void => {
    if (typeof rawValue !== 'string' || rawValue.trim().length === 0) {
      return;
    }

    const normalizedValue = normalizeRelativeDirectory(
      relative(packageRootPath, resolve(configDirectory, rawValue))
    );
    collection.add(normalizedValue);
  };

  addRelativeHint(outDirs, compilerOptions?.outDir);
  addRelativeHint(sourceRoots, compilerOptions?.rootDir);

  const includeValues =
    configFile.config !== null &&
    typeof configFile.config === 'object' &&
    Array.isArray((configFile.config as Record<string, unknown>).include)
      ? ((configFile.config as Record<string, unknown>).include as unknown[])
      : [];
  for (const includeValue of includeValues) {
    if (typeof includeValue !== 'string' || includeValue.trim().length === 0) {
      continue;
    }

    const staticPrefix = includeValue.split(/[/*]/u, 1)[0];
    if (staticPrefix.length >= 1) {
      addRelativeHint(sourceRoots, staticPrefix);
    }
  }

  return {
    outDirs: uniqueValues([...outDirs]),
    sourceRoots: uniqueValues([...sourceRoots]),
  };
};

const pickExportTarget = (value: unknown): string | undefined => {
  if (typeof value === 'string') {
    return value;
  }
  if (Array.isArray(value)) {
    for (const child of value) {
      const selected = pickExportTarget(child);
      if (selected !== undefined) {
        return selected;
      }
    }
    return undefined;
  }
  if (value === null || typeof value !== 'object') {
    return undefined;
  }

  const record = value as Record<string, unknown>;
  const preferredConditions = [
    'types',
    'import',
    'default',
    'require',
    'node',
    'browser',
  ];
  for (const condition of preferredConditions) {
    if (condition in record) {
      const selected = pickExportTarget(record[condition]);
      if (selected !== undefined) {
        return selected;
      }
    }
  }

  for (const child of Object.values(record)) {
    const selected = pickExportTarget(child);
    if (selected !== undefined) {
      return selected;
    }
  }

  return undefined;
};

const collectExportTargets = (value: unknown): readonly [string, string][] => {
  if (typeof value === 'string') {
    return [['.', value]];
  }
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return [];
  }

  const record = value as Record<string, unknown>;
  const exportKeys = Object.keys(record).filter(
    (key) => key.startsWith('.') && !key.includes('*')
  );

  if (exportKeys.length >= 1) {
    return exportKeys
      .sort((left, right) => left.localeCompare(right))
      .flatMap((exportPath) => {
        const target = pickExportTarget(record[exportPath]);
        return target === undefined ? [] : ([[exportPath, target]] as const);
      });
  }

  const rootTarget = pickExportTarget(record);
  return rootTarget === undefined ? [] : [['.', rootTarget]];
};

const addCandidate = (
  candidates: string[],
  seenCandidates: Set<string>,
  candidate: string
): void => {
  const normalizedCandidate = resolve(candidate);
  if (seenCandidates.has(normalizedCandidate)) {
    return;
  }

  seenCandidates.add(normalizedCandidate);
  candidates.push(normalizedCandidate);
};

const addMirroredSourceCandidates = (
  candidates: string[],
  seenCandidates: Set<string>,
  projectPath: string,
  normalizedTarget: string,
  pathMappingHints: PathMappingHints
): void => {
  const relativeTargetPath = normalizeRelativeDirectory(
    relative(projectPath, normalizedTarget)
  );
  const matchedOutDir = pathMappingHints.outDirs.find(
    (outDir) =>
      relativeTargetPath === outDir ||
      relativeTargetPath.startsWith(`${outDir}/`)
  );
  if (matchedOutDir === undefined) {
    return;
  }

  const relativeInsideOutDir =
    relativeTargetPath === matchedOutDir
      ? ''
      : relativeTargetPath.slice(matchedOutDir.length + 1);
  const baseRelativePath = stripKnownSourceExtension(relativeInsideOutDir);

  for (const sourceRoot of pathMappingHints.sourceRoots) {
    const sourceBasePath =
      sourceRoot === '.' || sourceRoot.length === 0
        ? resolve(projectPath, baseRelativePath)
        : resolve(projectPath, sourceRoot, baseRelativePath);
    for (const candidateExtension of supportedSourceExtensions) {
      addCandidate(
        candidates,
        seenCandidates,
        sourceBasePath + candidateExtension
      );
    }
  }
};

const enumerateCandidateSourceFilePaths = (
  projectPath: string,
  rawTargetPath: string,
  pathMappingHints: PathMappingHints
): readonly string[] => {
  const normalizedTarget = rawTargetPath.startsWith('.')
    ? resolve(projectPath, rawTargetPath)
    : isAbsolute(rawTargetPath)
      ? rawTargetPath
      : resolve(projectPath, rawTargetPath);

  const candidates: string[] = [];
  const seenCandidates = new Set<string>();

  addMirroredSourceCandidates(
    candidates,
    seenCandidates,
    projectPath,
    normalizedTarget,
    pathMappingHints
  );

  addCandidate(candidates, seenCandidates, normalizedTarget);

  const extension = extname(normalizedTarget);
  if (extension.length === 0) {
    for (const candidateExtension of supportedSourceExtensions) {
      addCandidate(
        candidates,
        seenCandidates,
        normalizedTarget + candidateExtension
      );
      addCandidate(
        candidates,
        seenCandidates,
        join(normalizedTarget, `index${candidateExtension}`)
      );
    }
  } else {
    if (normalizedTarget.endsWith('.d.ts')) {
      const basePath = normalizedTarget.slice(0, -'.d.ts'.length);
      for (const candidateExtension of ['.ts', '.tsx', '.js', '.jsx']) {
        addCandidate(candidates, seenCandidates, basePath + candidateExtension);
      }
    }
    if (normalizedTarget.endsWith('.d.mts')) {
      addCandidate(
        candidates,
        seenCandidates,
        normalizedTarget.slice(0, -'.d.mts'.length) + '.mts'
      );
    }
    if (normalizedTarget.endsWith('.d.cts')) {
      addCandidate(
        candidates,
        seenCandidates,
        normalizedTarget.slice(0, -'.d.cts'.length) + '.cts'
      );
    }
    if (!supportedSourceExtensions.includes(extension)) {
      const basePath = normalizedTarget.slice(0, -extension.length);
      for (const candidateExtension of supportedSourceExtensions) {
        addCandidate(candidates, seenCandidates, basePath + candidateExtension);
      }
    }
  }

  return candidates;
};

const resolveEntryPointCandidates = (
  packageRootPath: string,
  candidates: readonly RawEntryPointCandidate[],
  pathMappingHints: PathMappingHints
): readonly EntryPointResolutionAttempt[] =>
  candidates.map((candidate) => {
    const attemptedPaths = enumerateCandidateSourceFilePaths(
      packageRootPath,
      candidate.targetPath,
      pathMappingHints
    );
    const sourceFilePath = attemptedPaths.find((attemptedPath) =>
      existsSync(attemptedPath)
    );
    return {
      candidate,
      attemptedPaths,
      sourceFilePath,
    };
  });

const createEntryPointsFromAttempts = (
  attempts: readonly EntryPointResolutionAttempt[]
): readonly PackageEntryPoint[] => {
  const entryPoints: PackageEntryPoint[] = [];
  const seenExportPaths = new Set<string>();

  for (const attempt of attempts) {
    if (
      attempt.sourceFilePath === undefined ||
      seenExportPaths.has(attempt.candidate.exportPath)
    ) {
      continue;
    }

    seenExportPaths.add(attempt.candidate.exportPath);
    entryPoints.push({
      exportPath: attempt.candidate.exportPath,
      sourceFilePath: attempt.sourceFilePath,
    });
  }

  return entryPoints;
};

const formatEntryPointResolutionError = (
  attempts: readonly EntryPointResolutionAttempt[]
): Error => {
  const describedSources = new Set<string>();
  const checkedSourceLines: string[] = [];
  for (const attempt of attempts) {
    const sourceLine = `${attempt.candidate.source}: ${attempt.candidate.exportPath} -> ${attempt.candidate.targetPath}`;
    if (!describedSources.has(sourceLine)) {
      describedSources.add(sourceLine);
      checkedSourceLines.push(`- ${sourceLine}`);
    }
  }

  const attemptedPaths = [
    ...new Set(
      attempts.flatMap((attempt) =>
        attempt.attemptedPaths.map((attemptedPath) =>
          normalizeSlashes(attemptedPath)
        )
      )
    ),
  ];

  return new Error(
    [
      'No supported source entry points were found.',
      'Checked entry sources:',
      ...checkedSourceLines,
      'Attempted source file paths:',
      ...attemptedPaths.map((attemptedPath) => `- ${attemptedPath}`),
      'You can fix this by one of the following methods:',
      '- pass `--entry ./src/index.ts` explicitly',
      '- define `package.json` `dockit.entryPoints`',
      '- place a conventional entry such as `./src/index.ts` or `./src/main.ts`',
    ].join('\n')
  );
};

const createCliEntryCandidates = (
  packageRootPath: string,
  entryPaths: readonly string[]
): readonly RawEntryPointCandidate[] =>
  entryPaths.map((entryPath) => ({
    exportPath: deriveExportPathFromTargetPath(packageRootPath, entryPath),
    targetPath: entryPath,
    source: 'CLI --entry',
  }));

const createConfiguredEntryCandidates = (
  packageRootPath: string,
  packageJson: PackageJson
): readonly RawEntryPointCandidate[] => {
  const configuredEntryPoints = packageJson.dockit?.entryPoints;
  if (configuredEntryPoints === undefined) {
    return [];
  }

  if (typeof configuredEntryPoints === 'string') {
    return [
      {
        exportPath: deriveExportPathFromTargetPath(
          packageRootPath,
          configuredEntryPoints
        ),
        targetPath: configuredEntryPoints,
        source: 'package.json dockit.entryPoints',
      },
    ];
  }

  if (Array.isArray(configuredEntryPoints)) {
    return configuredEntryPoints.map((entryPoint, index) => {
      if (typeof entryPoint !== 'string') {
        throw new Error(
          `package.json dockit.entryPoints[${index}] must be a string.`
        );
      }
      return {
        exportPath: deriveExportPathFromTargetPath(packageRootPath, entryPoint),
        targetPath: entryPoint,
        source: 'package.json dockit.entryPoints',
      };
    });
  }

  if (
    configuredEntryPoints !== null &&
    typeof configuredEntryPoints === 'object'
  ) {
    return Object.entries(configuredEntryPoints).map(
      ([exportPath, targetPath]) => {
        if (typeof targetPath !== 'string') {
          throw new Error(
            `package.json dockit.entryPoints.${exportPath} must be a string.`
          );
        }
        return {
          exportPath: normalizeExportPath(exportPath),
          targetPath,
          source: 'package.json dockit.entryPoints',
        };
      }
    );
  }

  throw new Error(
    'package.json dockit.entryPoints must be a string, an array of strings, or an object map.'
  );
};

const createLegacyEntryCandidates = (
  packageJson: PackageJson
): readonly RawEntryPointCandidate[] => {
  const candidateSources: readonly [string, string | undefined][] = [
    ['package.json types', packageJson.types],
    ['package.json typings', packageJson.typings],
    ['package.json module', packageJson.module],
    ['package.json main', packageJson.main],
  ];

  return candidateSources.flatMap(([source, targetPath]) =>
    typeof targetPath === 'string'
      ? [
          {
            exportPath: '.',
            targetPath,
            source,
          },
        ]
      : []
  );
};

const createFallbackEntryCandidates = (
  packageRootPath: string
): readonly RawEntryPointCandidate[] =>
  fallbackEntryTargets.map((targetPath) => ({
    exportPath: deriveExportPathFromTargetPath(packageRootPath, targetPath),
    targetPath,
    source: 'fallback',
  }));

const resolveEntryPoints = (
  packageRootPath: string,
  packageJson: PackageJson,
  options: LoadProjectConfigurationOptions,
  pathMappingHints: PathMappingHints
): readonly PackageEntryPoint[] => {
  const cliEntryCandidates = createCliEntryCandidates(
    packageRootPath,
    options.entryPaths ?? []
  );
  if (cliEntryCandidates.length >= 1) {
    const attempts = resolveEntryPointCandidates(
      packageRootPath,
      cliEntryCandidates,
      pathMappingHints
    );
    const entryPoints = createEntryPointsFromAttempts(attempts);
    if (entryPoints.length >= 1) {
      return entryPoints;
    }
    throw formatEntryPointResolutionError(attempts);
  }

  const configuredEntryCandidates = createConfiguredEntryCandidates(
    packageRootPath,
    packageJson
  );
  if (configuredEntryCandidates.length >= 1) {
    const attempts = resolveEntryPointCandidates(
      packageRootPath,
      configuredEntryCandidates,
      pathMappingHints
    );
    const entryPoints = createEntryPointsFromAttempts(attempts);
    if (entryPoints.length >= 1) {
      return entryPoints;
    }
    throw formatEntryPointResolutionError(attempts);
  }

  const automaticCandidateGroups: ReadonlyArray<
    readonly RawEntryPointCandidate[]
  > = [
    collectExportTargets(packageJson.exports).map(
      ([exportPath, targetPath]) => ({
        exportPath,
        targetPath,
        source: 'package.json exports',
      })
    ),
    createLegacyEntryCandidates(packageJson),
    createFallbackEntryCandidates(packageRootPath),
  ];

  const attempts: EntryPointResolutionAttempt[] = [];
  for (const candidateGroup of automaticCandidateGroups) {
    if (candidateGroup.length === 0) {
      continue;
    }

    const groupAttempts = resolveEntryPointCandidates(
      packageRootPath,
      candidateGroup,
      pathMappingHints
    );
    attempts.push(...groupAttempts);

    const entryPoints = createEntryPointsFromAttempts(groupAttempts);
    if (entryPoints.length >= 1) {
      return entryPoints;
    }
  }

  throw formatEntryPointResolutionError(attempts);
};

const createProgramFromConfig = (
  packageRootPath: string,
  entryPoints: readonly PackageEntryPoint[]
): ts.Program => {
  const configPath =
    ts.findConfigFile(packageRootPath, ts.sys.fileExists, 'tsconfig.json') ??
    ts.findConfigFile(packageRootPath, ts.sys.fileExists, 'jsconfig.json');

  if (configPath === undefined) {
    return ts.createProgram({
      rootNames: [
        ...new Set(entryPoints.map((entryPoint) => entryPoint.sourceFilePath)),
      ],
      options: {
        allowJs: true,
        checkJs: true,
        module: ts.ModuleKind.NodeNext,
        moduleResolution: ts.ModuleResolutionKind.NodeNext,
        target: ts.ScriptTarget.ES2022,
        strict: true,
        skipLibCheck: true,
        noEmit: true,
        esModuleInterop: true,
        resolveJsonModule: true,
      },
    });
  }

  const configFile = ts.readConfigFile(configPath, ts.sys.readFile);
  if (configFile.error !== undefined) {
    throw new Error(
      ts.formatDiagnosticsWithColorAndContext([configFile.error], formatHost)
    );
  }

  const configDirectory = dirname(configPath);
  const parsed = ts.parseJsonConfigFileContent(
    configFile.config,
    ts.sys,
    configDirectory,
    undefined,
    configPath
  );
  if (parsed.errors.length >= 1) {
    throw new Error(
      ts.formatDiagnosticsWithColorAndContext(parsed.errors, formatHost)
    );
  }

  const containsJavaScriptEntry = entryPoints.some((entryPoint) =>
    ['.js', '.jsx', '.mjs', '.cjs'].includes(extname(entryPoint.sourceFilePath))
  );

  return ts.createProgram({
    rootNames: [
      ...new Set([
        ...parsed.fileNames,
        ...entryPoints.map((entryPoint) => entryPoint.sourceFilePath),
      ]),
    ],
    options: {
      ...parsed.options,
      allowJs: parsed.options.allowJs ?? containsJavaScriptEntry,
      checkJs: parsed.options.checkJs ?? containsJavaScriptEntry,
      noEmit: true,
      skipLibCheck: parsed.options.skipLibCheck ?? true,
    },
    projectReferences: parsed.projectReferences,
  });
};

const formatHost: ts.FormatDiagnosticsHost = {
  getCanonicalFileName: (fileName) => fileName,
  getCurrentDirectory: () => process.cwd(),
  getNewLine: () => '\n',
};

/**
 * Loads package configuration and creates a TypeScript program for analysis.
 */
export const loadProjectConfiguration = async (
  projectPath: string,
  options: LoadProjectConfigurationOptions = {}
): Promise<ProjectConfiguration> => {
  const packageRootPath = resolve(projectPath);
  const packageJson = await readPackageJson(packageRootPath);
  const pathMappingHints = await loadPathMappingHints(packageRootPath);

  if (
    typeof packageJson.name !== 'string' ||
    packageJson.name.trim().length === 0
  ) {
    throw new Error('package.json must contain a package name.');
  }

  const entryPoints = resolveEntryPoints(
    packageRootPath,
    packageJson,
    options,
    pathMappingHints
  );
  const program = createProgramFromConfig(packageRootPath, entryPoints);

  return {
    packageName: packageJson.name,
    packageVersion: packageJson.version,
    packageDescription: packageJson.description,
    packageRootPath,
    entryPoints,
    program,
  };
};
