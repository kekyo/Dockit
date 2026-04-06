/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { Box } from './index.js';

/**
 * Describes a string box.
 *
 * @param box Box parameter.
 * @returns Description text.
 */
export const describeBox = (box: Box<string>): string => box.format('extra');
