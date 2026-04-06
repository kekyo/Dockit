/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { analyzeProject } from '../src/internal/analyzer.js';
import {
  getMarkdownOutputPath,
  writeMarkdown,
} from '../src/internal/markdown-writer.js';
import type { PackageMetadata } from '../src/internal/package-metadata.js';
import {
  fixturePath,
  readUtf8File,
  withTemporaryDirectory,
} from './test-helpers.js';

const createPackageMetadata = (): PackageMetadata => ({
  value: {
    name: 'dockit-ts-fixture',
    version: '1.2.3',
    description: 'Fixture package for dockit-ts.',
    author:
      'Kouji Matsui <koji@example.com> (https://example.com/authors/koji)',
    license: 'MIT',
    keywords: ['dockit', 'metadata', 'fixture'],
    type: 'module',
    main: './dist/index.cjs',
    module: './dist/index.js',
    types: './dist/index.d.ts',
    git: {
      tags: ['v1.2.3', 'latest'],
      branches: ['develop', 'main'],
      commit: {
        hash: '0123456789abcdef0123456789abcdef01234567',
        date: '2026-04-06T00:00:00.000Z',
        message: 'test: update metadata table',
      },
    },
    buildDate: '2026-04-06T12:34:56.000Z',
  },
});

describe('writeMarkdown', () => {
  it('writes package metadata, module indexes, and declaration sections for TypeScript fixtures', async () => {
    const packageDocumentation = await analyzeProject(
      fixturePath('ts-project')
    );
    const packageMetadata = createPackageMetadata();

    await withTemporaryDirectory(async (outputDirectory) => {
      const markdownPath = getMarkdownOutputPath(
        outputDirectory,
        packageDocumentation.packageName
      );
      await writeMarkdown(
        markdownPath,
        packageDocumentation,
        packageMetadata,
        1
      );
      const markdown = await readUtf8File(markdownPath);

      expect(markdown).toContain('# dockit-ts-fixture package');
      expect(markdown).toContain('| `name` | &quot;dockit-ts-fixture&quot; |');
      expect(markdown).toContain('| `version` | &quot;1.2.3&quot; |');
      expect(markdown).toContain(
        '| `description` | &quot;Fixture package for dockit-ts.&quot; |'
      );
      expect(markdown).toContain(
        '| `author` | &quot;Kouji Matsui &lt;koji@example.com&gt; (https://example.com/authors/koji)&quot; |'
      );
      expect(markdown).toContain('| `license` | &quot;MIT&quot; |');
      expect(markdown).toContain(
        '| `keywords` | &quot;dockit&quot;, &quot;metadata&quot;, &quot;fixture&quot; |'
      );
      expect(markdown).toContain('| `type` | &quot;module&quot; |');
      expect(markdown).toContain('| `main` | &quot;./dist/index.cjs&quot; |');
      expect(markdown).toContain('| `module` | &quot;./dist/index.js&quot; |');
      expect(markdown).toContain('| `types` | &quot;./dist/index.d.ts&quot; |');
      expect(markdown).toContain(
        '| `git.tags` | &quot;v1.2.3&quot;, &quot;latest&quot; |'
      );
      expect(markdown).toContain(
        '| `git.branches` | &quot;develop&quot;, &quot;main&quot; |'
      );
      expect(markdown).toContain(
        '| `git.commit.hash` | &quot;0123456789abcdef0123456789abcdef01234567&quot; |'
      );
      expect(markdown).toContain(
        '| `git.commit.date` | &quot;2026-04-06T00:00:00.000Z&quot; |'
      );
      expect(markdown).toContain(
        '| `git.commit.message` | &quot;test: update metadata table&quot; |'
      );
      expect(markdown).toContain(
        '| `buildDate` | &quot;2026-04-06T12:34:56.000Z&quot; |'
      );

      const authorIndex = markdown.indexOf('| `author` |');
      const buildDateIndex = markdown.indexOf('| `buildDate` |');
      const descriptionIndex = markdown.indexOf('| `description` |');
      const gitBranchesIndex = markdown.indexOf('| `git.branches` |');
      const gitCommitDateIndex = markdown.indexOf('| `git.commit.date` |');
      const gitCommitHashIndex = markdown.indexOf('| `git.commit.hash` |');
      const gitCommitMessageIndex = markdown.indexOf(
        '| `git.commit.message` |'
      );
      const gitTagsIndex = markdown.indexOf('| `git.tags` |');
      const keywordsIndex = markdown.indexOf('| `keywords` |');
      const licenseIndex = markdown.indexOf('| `license` |');
      const mainIndex = markdown.indexOf('| `main` |');
      const moduleIndex = markdown.indexOf('| `module` |');
      const nameIndex = markdown.indexOf('| `name` |');
      const typeIndex = markdown.indexOf('| `type` |');
      const typesIndex = markdown.indexOf('| `types` |');
      const versionIndex = markdown.indexOf('| `version` |');

      expect(authorIndex).toBeLessThan(buildDateIndex);
      expect(buildDateIndex).toBeLessThan(descriptionIndex);
      expect(descriptionIndex).toBeLessThan(gitBranchesIndex);
      expect(gitBranchesIndex).toBeLessThan(gitCommitDateIndex);
      expect(gitCommitDateIndex).toBeLessThan(gitCommitHashIndex);
      expect(gitCommitHashIndex).toBeLessThan(gitCommitMessageIndex);
      expect(gitCommitMessageIndex).toBeLessThan(gitTagsIndex);
      expect(gitTagsIndex).toBeLessThan(keywordsIndex);
      expect(keywordsIndex).toBeLessThan(licenseIndex);
      expect(licenseIndex).toBeLessThan(mainIndex);
      expect(mainIndex).toBeLessThan(moduleIndex);
      expect(moduleIndex).toBeLessThan(nameIndex);
      expect(nameIndex).toBeLessThan(typeIndex);
      expect(typeIndex).toBeLessThan(typesIndex);
      expect(typesIndex).toBeLessThan(versionIndex);

      expect(markdown).toContain('| [ `.` ](#root-module) |');
      expect(markdown).toContain('| [ `./extras` ](#extras-module) |');
      expect(markdown).toContain(
        '| [ `createResult()` ](#createresult-function) |'
      );
      expect(markdown).not.toContain('<a id="');
      expect(markdown).toContain(
        '<a name="root-module"></a>\n\n## Root module'
      );
      expect(markdown).toContain(
        '<a name="extras-module"></a>\n\n## ./extras module'
      );
      expect(markdown).toContain(
        '<a name="createresult-function"></a>\n\n### createResult() function'
      );
      expect(markdown).toContain('### Box class');
      expect(markdown).toContain('Represents a box.');
      expect(markdown).toContain('| `TValue` | Stored value type. |');
      expect(markdown).toContain('#### Constructor');
      expect(markdown).toContain('#### format() method');
      expect(markdown).toContain('#### [key: string] index signature');
      expect(markdown).toContain('### NotificationContext type alias');
      expect(markdown).toContain('export type NotificationContext = {');
      expect(markdown).toContain('#### initialize() method');
      expect(markdown).toContain('#### show property');
      expect(markdown).toContain('#### title property');
      expect(markdown).toContain('| `channel` | Channel parameter. |');
      expect(markdown).toContain('| `message` | Message parameter. |');
      expect(markdown).toContain('### Result type alias');
      expect(markdown).toContain(
        'export type Result<TValue> = TValue | ErrorInfo;'
      );
      expect(markdown).toContain('### currentMode constant');
      expect(markdown).toContain(
        "export const currentMode: 'auto' | 'manual';"
      );
      expect(markdown).toContain('### SampleState enum');
      expect(markdown).toContain(
        '| `Busy` | Busy state. Used while processing is active. |'
      );
      expect(markdown).toContain('See also: [Result](#result-type-alias)');

      const createResultSectionIndex = markdown.indexOf(
        '### createResult() function'
      );
      const createResultSignatureIndex = markdown.indexOf(
        'export function createResult',
        createResultSectionIndex
      );
      const createResultTypeParameterTableIndex = markdown.indexOf(
        '|Type parameter|Description|',
        createResultSectionIndex
      );
      const createResultParameterTableIndex = markdown.indexOf(
        '|Parameter|Description|',
        createResultSectionIndex
      );
      const createResultReturnTableIndex = markdown.indexOf(
        '|Return value|',
        createResultSectionIndex
      );

      expect(createResultSignatureIndex).toBeGreaterThan(
        createResultSectionIndex
      );
      expect(createResultSignatureIndex).toBeLessThan(
        createResultTypeParameterTableIndex
      );
      expect(createResultTypeParameterTableIndex).toBeLessThan(
        createResultParameterTableIndex
      );
      expect(createResultParameterTableIndex).toBeLessThan(
        createResultReturnTableIndex
      );
    });
  });

  it('writes documentation from JavaScript JSDoc fixtures', async () => {
    const packageDocumentation = await analyzeProject(
      fixturePath('js-project')
    );
    const packageMetadata = createPackageMetadata();

    await withTemporaryDirectory(async (outputDirectory) => {
      const markdownPath = join(outputDirectory, 'dockit-js-fixture.md');
      await writeMarkdown(
        markdownPath,
        packageDocumentation,
        packageMetadata,
        1
      );
      const markdown = await readUtf8File(markdownPath);

      expect(markdown).toContain('# dockit-js-fixture package');
      expect(markdown).toContain('### chooseMode() function');
      expect(markdown).toContain('Converts a string to a mode value.');
      expect(markdown).toContain('| `value` | Text parameter. |');
      expect(markdown).toContain('| Converted mode. |');
      expect(markdown).toContain('JavaScript remarks.');
      expect(markdown).toContain('```ts');
      expect(markdown).toContain('const mode = chooseMode("a");');

      const chooseModeSectionIndex = markdown.indexOf(
        '### chooseMode() function'
      );
      const chooseModeSignatureIndex = markdown.indexOf(
        'export function chooseMode',
        chooseModeSectionIndex
      );
      const chooseModeParameterTableIndex = markdown.indexOf(
        '|Parameter|Description|',
        chooseModeSectionIndex
      );
      const chooseModeReturnTableIndex = markdown.indexOf(
        '|Return value|',
        chooseModeSectionIndex
      );

      expect(chooseModeSignatureIndex).toBeGreaterThan(chooseModeSectionIndex);
      expect(chooseModeSignatureIndex).toBeLessThan(
        chooseModeParameterTableIndex
      );
      expect(chooseModeParameterTableIndex).toBeLessThan(
        chooseModeReturnTableIndex
      );
    });
  });
});
