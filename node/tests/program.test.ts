/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { join, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { run } from '../src/program.js';
import {
  createCapturedWritable,
  fixturePath,
  readUtf8File,
  withTemporaryDirectory,
} from './test-helpers.js';

describe('run', () => {
  it('returns an error and usage when required arguments are missing', async () => {
    const outputWriter = createCapturedWritable();
    const errorWriter = createCapturedWritable();

    const exitCode = await run([], outputWriter.stream, errorWriter.stream);

    expect(exitCode).toBe(1);
    expect(outputWriter.read()).toBe('');
    expect(errorWriter.read()).toContain(
      'Expected <project-path> and <output-directory>.'
    );
    expect(errorWriter.read()).toContain(
      'Usage: dockit-ts [options] <project-path> <output-directory>'
    );
  });

  it('returns success and usage when help is requested', async () => {
    const outputWriter = createCapturedWritable();
    const errorWriter = createCapturedWritable();

    const exitCode = await run(
      ['--help'],
      outputWriter.stream,
      errorWriter.stream
    );

    expect(exitCode).toBe(0);
    expect(errorWriter.read()).toBe('');
    expect(outputWriter.read()).toContain(
      'Usage: dockit-ts [options] <project-path> <output-directory>'
    );
    expect(outputWriter.read()).toContain('--initial-level');
    expect(outputWriter.read()).toContain('--entry');
  });

  it('returns success and banner when version is requested with the long option', async () => {
    const outputWriter = createCapturedWritable();
    const errorWriter = createCapturedWritable();

    const exitCode = await run(
      ['--version'],
      outputWriter.stream,
      errorWriter.stream
    );

    expect(exitCode).toBe(0);
    expect(errorWriter.read()).toBe('');
    expect(outputWriter.read()).toMatch(/^Dockit \[typescript\] \[[^\]]+\]\n$/);
  });

  it('returns success and banner when version is requested with the short option', async () => {
    const outputWriter = createCapturedWritable();
    const errorWriter = createCapturedWritable();

    const exitCode = await run(['-v'], outputWriter.stream, errorWriter.stream);

    expect(exitCode).toBe(0);
    expect(errorWriter.read()).toBe('');
    expect(outputWriter.read()).toMatch(/^Dockit \[typescript\] \[[^\]]+\]\n$/);
  });

  it('parses the initial level option and generates markdown', async () => {
    await withTemporaryDirectory(async (outputDirectory) => {
      const outputWriter = createCapturedWritable();
      const errorWriter = createCapturedWritable();

      const exitCode = await run(
        ['--initial-level=2', fixturePath('ts-project'), outputDirectory],
        outputWriter.stream,
        errorWriter.stream
      );

      const markdown = await readUtf8File(
        join(outputDirectory, 'dockit-ts-fixture.md')
      );

      expect(exitCode).toBe(0);
      expect(outputWriter.read()).toContain(
        `Input project: ${resolve(fixturePath('ts-project'))}`
      );
      expect(outputWriter.read()).toContain(
        `Output markdown: ${join(outputDirectory, 'dockit-ts-fixture.md')}`
      );
      expect(outputWriter.read()).toMatch(/Elapsed time: \d+\.\d{3} ms/);
      expect(errorWriter.read()).toBe('');
      expect(markdown).toContain('## dockit-ts-fixture package');
    });
  });

  it('returns an error when initial level is less than one', async () => {
    const outputWriter = createCapturedWritable();
    const errorWriter = createCapturedWritable();

    const exitCode = await run(
      [
        '--initial-level=0',
        fixturePath('ts-project'),
        fixturePath('ts-project'),
      ],
      outputWriter.stream,
      errorWriter.stream
    );

    expect(exitCode).toBe(1);
    expect(outputWriter.read()).toBe('');
    expect(errorWriter.read()).toContain('Initial level must be 1 or greater.');
  });

  it('accepts explicit --entry values for cli-style packages', async () => {
    await withTemporaryDirectory(async (outputDirectory) => {
      const outputWriter = createCapturedWritable();
      const errorWriter = createCapturedWritable();

      const exitCode = await run(
        [
          '--entry',
          './src/index.ts',
          fixturePath('cli-project'),
          outputDirectory,
        ],
        outputWriter.stream,
        errorWriter.stream
      );

      const markdown = await readUtf8File(
        join(outputDirectory, 'dockit-cli-fixture.md')
      );

      expect(exitCode).toBe(0);
      expect(outputWriter.read()).toContain(
        `Input project: ${resolve(fixturePath('cli-project'))}`
      );
      expect(outputWriter.read()).toContain(
        `Output markdown: ${join(outputDirectory, 'dockit-cli-fixture.md')}`
      );
      expect(outputWriter.read()).toMatch(/Elapsed time: \d+\.\d{3} ms/);
      expect(errorWriter.read()).toBe('');
      expect(markdown).toContain('# dockit-cli-fixture package');
      expect(markdown).toContain('### CliSurface interface');
    });
  });

  it('returns a diagnostic error when entry points cannot be resolved', async () => {
    const outputWriter = createCapturedWritable();
    const errorWriter = createCapturedWritable();

    const exitCode = await run(
      [fixturePath('broken-project'), fixturePath('broken-project')],
      outputWriter.stream,
      errorWriter.stream
    );

    expect(exitCode).toBe(1);
    expect(outputWriter.read()).toMatch(/^Dockit \[typescript\] \[[^\]]+\]\n$/);
    expect(errorWriter.read()).toContain(
      'No supported source entry points were found.'
    );
    expect(errorWriter.read()).toContain('Checked entry sources:');
    expect(errorWriter.read()).toContain('fallback: . -> ./src/index.ts');
    expect(errorWriter.read()).toContain('Attempted source file paths:');
  });
});
