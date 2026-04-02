/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { run } from './program.js';

const main = async (): Promise<void> => {
  const exitCode = await run(
    process.argv.slice(2),
    process.stdout,
    process.stderr
  );
  process.exitCode = exitCode;
};

void main();
