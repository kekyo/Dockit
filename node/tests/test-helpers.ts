/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { Writable } from 'node:stream';
import { fileURLToPath } from 'node:url';

export interface CapturedWritable {
  stream: NodeJS.WritableStream;
  read: () => string;
}

const here = dirname(fileURLToPath(import.meta.url));

export const fixturePath = (name: string): string =>
  resolve(here, '..', 'fixtures', name);

export const repositoryPath = (...paths: string[]): string =>
  resolve(here, '..', '..', ...paths);

export const createCapturedWritable = (): CapturedWritable => {
  const chunks: string[] = [];

  const stream = new Writable({
    write(chunk, _encoding, callback) {
      chunks.push(
        Buffer.isBuffer(chunk) ? chunk.toString('utf8') : String(chunk)
      );
      callback();
    },
  });

  return {
    stream,
    read: () => chunks.join(''),
  };
};

export const withTemporaryDirectory = async <T>(
  action: (directoryPath: string) => Promise<T>
): Promise<T> => {
  const directoryPath = await mkdtemp(join(tmpdir(), 'dockit-ts-'));
  try {
    return await action(directoryPath);
  } finally {
    await rm(directoryPath, { recursive: true, force: true });
  }
};

export const readUtf8File = async (filePath: string): Promise<string> =>
  readFile(filePath, 'utf8');
