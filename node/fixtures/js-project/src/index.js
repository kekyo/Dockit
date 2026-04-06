/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

/**
 * Converts a string to a mode value.
 *
 * @param {string} value - Text parameter.
 * @returns {"alpha" | "beta"} - Converted mode.
 * @remarks JavaScript remarks.
 * @example
 * const mode = chooseMode("a");
 */
export const chooseMode = (value) => (value.length >= 1 ? 'alpha' : 'beta');
