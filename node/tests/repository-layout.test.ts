/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { describe, expect, it } from 'vitest';
import { readUtf8File, repositoryPath } from './test-helpers.js';

describe('repository layout', () => {
  it('keeps node package scripts aligned with the node root layout', async () => {
    const packageJson = JSON.parse(
      await readUtf8File(repositoryPath('node', 'package.json'))
    ) as {
      scripts?: {
        pack?: string;
      };
    };

    expect(packageJson.scripts?.pack).toBe(
      'npm run build && screw-up pack --pack-destination ../artifacts/'
    );
  });

  it('packs from the node root without referencing the legacy dockit-ts subdirectory', async () => {
    const buildPackScript = await readUtf8File(repositoryPath('build_pack.sh'));

    expect(buildPackScript).toContain('\ncd node\n');
    expect(buildPackScript).not.toContain('node/dockit-ts');
  });
});
