/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import ts from 'typescript';
import type { DocumentationComment, NamedDocumentation } from './models.js';
import { createEmptyDocumentationComment } from './models.js';
import { normalizeDocumentationText, trimCommonIndent } from './text.js';

type JSDocCommentValue = string | ts.NodeArray<ts.JSDocComment> | undefined;

const renderCommentPart = (
  part:
    | string
    | ts.JSDocText
    | ts.JSDocLink
    | ts.JSDocLinkCode
    | ts.JSDocLinkPlain
    | undefined
): string => {
  if (part === undefined) {
    return '';
  }
  if (typeof part === 'string') {
    return part;
  }
  if ('text' in part && typeof part.text === 'string') {
    return part.text;
  }
  return part.getText();
};

const renderComment = (comment: JSDocCommentValue): string | undefined => {
  if (comment === undefined) {
    return undefined;
  }
  if (typeof comment === 'string') {
    const text = normalizeDocumentationText(comment);
    return text.length >= 1 ? text : undefined;
  }

  const rendered = [...comment].map((part) => renderCommentPart(part)).join('');
  const text = normalizeDocumentationText(rendered);
  return text.length >= 1 ? text : undefined;
};

const renderExampleComment = (
  comment: JSDocCommentValue
): string | undefined => {
  const text = renderComment(comment);
  if (text === undefined) {
    return undefined;
  }
  const normalized = trimCommonIndent(text);
  return normalized.length >= 1 ? normalized : undefined;
};

const normalizeTagDescription = (description: string): string | undefined => {
  const normalized = description.replace(/^- (?=\S)/u, '').trim();
  return normalized.length >= 1 ? normalized : undefined;
};

const mergeNamedDocumentation = (
  values: readonly NamedDocumentation[],
  candidate: NamedDocumentation
): readonly NamedDocumentation[] => {
  if (values.some((value) => value.name === candidate.name)) {
    return values;
  }
  return [...values, candidate];
};

const parseTypeParameterTag = (
  tag: ts.JSDocTag,
  description: string
): NamedDocumentation | undefined => {
  const match = /^@(?:typeParam|template)\s+([^\s-]+)\s*/u.exec(tag.getText());
  if (match === null) {
    return undefined;
  }

  const normalizedDescription = description.replace(
    new RegExp(`^${match[1].replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')}\\s+`, 'u'),
    ''
  );
  const cleanedDescription = normalizeTagDescription(normalizedDescription);
  if (cleanedDescription === undefined) {
    return undefined;
  }

  return {
    name: match[1],
    description: cleanedDescription,
  };
};

const selectDocumentationNode = (
  declarations: readonly ts.Declaration[]
): ts.Declaration | undefined =>
  declarations.find(
    (declaration) => ts.getJSDocCommentsAndTags(declaration).length >= 1
  ) ?? declarations[0];

/**
 * Extracts structured documentation from a declaration and its symbol.
 */
export const extractDocumentation = (
  symbol: ts.Symbol | undefined,
  declarations: readonly ts.Declaration[],
  checker: ts.TypeChecker
): DocumentationComment => {
  const documentationNode = selectDocumentationNode(declarations);
  const summaryText =
    symbol === undefined
      ? undefined
      : normalizeDocumentationText(
          ts.displayPartsToString(symbol.getDocumentationComment(checker))
        );
  const baseDocumentation = createEmptyDocumentationComment();

  if (documentationNode === undefined) {
    return {
      ...baseDocumentation,
      summary: summaryText && summaryText.length >= 1 ? summaryText : undefined,
    };
  }

  let remarks = baseDocumentation.remarks;
  let returns = baseDocumentation.returns;
  let typeParameters = baseDocumentation.typeParameters;
  let parameters = baseDocumentation.parameters;
  const examples: string[] = [];
  const seeAlso: string[] = [];

  for (const tag of ts.getJSDocTags(documentationNode)) {
    if (
      ts.isJSDocTemplateTag(tag) ||
      tag.tagName.text === 'typeParam' ||
      tag.tagName.text === 'template'
    ) {
      const description = renderComment(tag.comment);
      if (description !== undefined) {
        const normalizedDescription = normalizeTagDescription(description);
        if (ts.isJSDocTemplateTag(tag)) {
          if (normalizedDescription !== undefined) {
            for (const typeParameter of tag.typeParameters) {
              typeParameters = mergeNamedDocumentation(typeParameters, {
                name: typeParameter.name.text,
                description: normalizedDescription,
              });
            }
          }
        } else {
          const namedDocumentation = parseTypeParameterTag(tag, description);
          if (namedDocumentation !== undefined) {
            typeParameters = mergeNamedDocumentation(
              typeParameters,
              namedDocumentation
            );
          }
        }
      }
      continue;
    }

    if (ts.isJSDocParameterTag(tag)) {
      const description = renderComment(tag.comment);
      const normalizedDescription =
        description === undefined
          ? undefined
          : normalizeTagDescription(description);
      if (description !== undefined && normalizedDescription !== undefined) {
        parameters = mergeNamedDocumentation(parameters, {
          name: tag.name.getText(),
          description: normalizedDescription,
        });
      }
      continue;
    }

    if (ts.isJSDocReturnTag(tag)) {
      const description = renderComment(tag.comment);
      returns =
        description === undefined
          ? undefined
          : normalizeTagDescription(description);
      continue;
    }

    if (tag.tagName.text === 'remarks') {
      remarks = renderComment(tag.comment);
      continue;
    }

    if (tag.tagName.text === 'example') {
      const example = renderExampleComment(tag.comment);
      if (example !== undefined) {
        examples.push(example);
      }
      continue;
    }

    if (ts.isJSDocSeeTag(tag) || tag.tagName.text === 'see') {
      const rawValue =
        renderComment(tag.comment) ??
        normalizeDocumentationText(tag.getText().replace(/^@see\s*/u, ''));
      if (rawValue !== undefined) {
        seeAlso.push(rawValue);
      }
    }
  }

  return {
    summary: summaryText && summaryText.length >= 1 ? summaryText : undefined,
    typeParameters,
    parameters,
    returns,
    remarks,
    examples,
    seeAlso,
  };
};
