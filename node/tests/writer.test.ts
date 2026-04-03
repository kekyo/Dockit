/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
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
import {
  fixturePath,
  readUtf8File,
  withTemporaryDirectory,
} from './test-helpers.js';

describe('writeMarkdown', () => {
  it('writes package metadata, module indexes, and declaration sections for TypeScript fixtures', async () => {
    const packageDocumentation = await analyzeProject(
      fixturePath('ts-project')
    );

    await withTemporaryDirectory(async (outputDirectory) => {
      const markdownPath = getMarkdownOutputPath(
        outputDirectory,
        packageDocumentation.packageName
      );
      await writeMarkdown(markdownPath, packageDocumentation, 1);
      const markdown = await readUtf8File(markdownPath);

      expect(markdown).toContain('# dockit-ts-fixture package');
      expect(markdown).toContain('| `PackageVersion` | &quot;1.2.3&quot; |');
      expect(markdown).toContain(
        '| [ `.` ](./dockit-ts-fixture.md#root-module) |'
      );
      expect(markdown).toContain(
        '| [ `./extras` ](./dockit-ts-fixture.md#extras-module) |'
      );
      expect(markdown).toContain(
        '| [ `createResult()` ](./dockit-ts-fixture.md#createresult-function) |'
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
      expect(markdown).toContain(
        'See also: [Result](./dockit-ts-fixture.md#result-type-alias)'
      );

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

    await withTemporaryDirectory(async (outputDirectory) => {
      const markdownPath = join(outputDirectory, 'dockit-js-fixture.md');
      await writeMarkdown(markdownPath, packageDocumentation, 1);
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
