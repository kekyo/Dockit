/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { resolve } from 'node:path';
import prettierMax from 'prettier-max';
import screwUp from 'screw-up';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [
    screwUp({
      outputMetadataFile: true,
    }),
    prettierMax(),
  ],
  build: {
    ssr: resolve(__dirname, 'src/cli.ts'),
    target: 'node20',
    sourcemap: true,
    minify: false,
    outDir: 'dist',
    emptyOutDir: true,
    rollupOptions: {
      external: ['typescript', /^node:/u],
      output: {
        entryFileNames: 'cli.mjs',
        format: 'es',
        banner: '#!/usr/bin/env node',
      },
    },
  },
});
