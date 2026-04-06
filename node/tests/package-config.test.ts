/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { describe, expect, it } from 'vitest';
import { loadProjectConfiguration } from '../src/internal/package-config.js';
import { fixturePath } from './test-helpers.js';

describe('loadProjectConfiguration', () => {
  it('resolves configured package.json dockit.entryPoints', async () => {
    const configuration = await loadProjectConfiguration(
      fixturePath('configured-project')
    );

    expect(configuration.entryPoints).toEqual([
      {
        exportPath: '.',
        sourceFilePath: expect.stringMatching(/src\/index\.ts$/u),
      },
      {
        exportPath: './extra',
        sourceFilePath: expect.stringMatching(/src\/extra\.ts$/u),
      },
    ]);
  });

  it('resolves fallback src/index.ts for cli-style packages', async () => {
    const configuration = await loadProjectConfiguration(
      fixturePath('cli-project')
    );

    expect(configuration.entryPoints).toEqual([
      {
        exportPath: '.',
        sourceFilePath: expect.stringMatching(/src\/index\.ts$/u),
      },
    ]);
  });

  it('accepts multiple explicit entry paths', async () => {
    const configuration = await loadProjectConfiguration(
      fixturePath('configured-project'),
      {
        entryPaths: ['./src/index.ts', './src/extra.ts'],
      }
    );

    expect(configuration.entryPoints).toEqual([
      {
        exportPath: '.',
        sourceFilePath: expect.stringMatching(/src\/index\.ts$/u),
      },
      {
        exportPath: './extra',
        sourceFilePath: expect.stringMatching(/src\/extra\.ts$/u),
      },
    ]);
  });

  it('maps dist metadata entry points back to source files before using dist declarations', async () => {
    const configuration = await loadProjectConfiguration(
      fixturePath('dist-entry-project')
    );

    expect(configuration.entryPoints).toEqual([
      {
        exportPath: '.',
        sourceFilePath: expect.stringMatching(/src\/index\.ts$/u),
      },
    ]);
  });
});
