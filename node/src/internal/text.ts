/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

const normalizeLineBreaks = (value: string): string =>
  value.replace(/\r\n?/g, '\n');

/**
 * Normalizes free-form documentation text into Markdown-friendly paragraphs.
 */
export const normalizeDocumentationText = (value: string): string => {
  const normalized = normalizeLineBreaks(value).trim();
  if (normalized.length === 0) {
    return '';
  }

  const paragraphs = normalized
    .split(/\n\s*\n/g)
    .map((paragraph) =>
      paragraph
        .split('\n')
        .map((line) => line.trim())
        .filter((line) => line.length >= 1)
        .join(' ')
    )
    .filter((paragraph) => paragraph.length >= 1);

  return paragraphs.join('\n\n');
};

/**
 * Normalizes text for inline Markdown table cells.
 */
export const normalizeInlineText = (value: string): string =>
  normalizeDocumentationText(value).replace(/\s+/g, ' ').trim();

/**
 * Trims common indentation from a code-like block.
 */
export const trimCommonIndent = (value: string): string => {
  const lines = normalizeLineBreaks(value).split('\n');
  const trimmedLines = [...lines];

  while (trimmedLines.length >= 1 && trimmedLines[0].trim().length === 0) {
    trimmedLines.shift();
  }
  while (
    trimmedLines.length >= 1 &&
    trimmedLines[trimmedLines.length - 1].trim().length === 0
  ) {
    trimmedLines.pop();
  }

  const indent = trimmedLines.reduce<number>((current, line) => {
    if (line.trim().length === 0) {
      return current;
    }
    const lineIndent = line.match(/^\s*/u)?.[0].length ?? 0;
    return Math.min(current, lineIndent);
  }, Number.MAX_SAFE_INTEGER);

  return trimmedLines
    .map((line) =>
      indent === Number.MAX_SAFE_INTEGER
        ? line.trimEnd()
        : line.slice(indent).trimEnd()
    )
    .join('\n');
};

/**
 * Escapes Markdown-special HTML characters.
 */
export const escapeMarkdownText = (value: string): string =>
  value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');

/**
 * Creates a stable slug compatible with Dockit's existing anchor style.
 */
export const toAnchorSlug = (value: string): string => {
  let result = '';
  let previousWhitespace = false;

  for (const character of value) {
    if (/\s/u.test(character)) {
      if (!previousWhitespace) {
        result += '-';
        previousWhitespace = true;
      }
      continue;
    }

    if (/[.\-]/u.test(character) || /[0-9a-z]/iu.test(character)) {
      result += character.toLowerCase();
      previousWhitespace = false;
    }
  }

  return result;
};
