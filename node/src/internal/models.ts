/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

/**
 * Supported top-level declaration kinds.
 */
export type DeclarationKind =
  | 'class'
  | 'interface'
  | 'enum'
  | 'function'
  | 'type-alias'
  | 'constant'
  | 'variable';

/**
 * Supported member declaration kinds.
 */
export type MemberKind =
  | 'constructor'
  | 'field'
  | 'property'
  | 'method'
  | 'index-signature';

/**
 * Describes a named documentation item such as a parameter.
 */
export interface NamedDocumentation {
  name: string;
  description: string;
}

/**
 * Describes extracted documentation text for a declaration.
 */
export interface DocumentationComment {
  summary: string | undefined;
  typeParameters: readonly NamedDocumentation[];
  parameters: readonly NamedDocumentation[];
  returns: string | undefined;
  remarks: string | undefined;
  examples: readonly string[];
  seeAlso: readonly string[];
}

/**
 * Describes an enum member documentation entry.
 */
export interface EnumValueDocumentation extends DocumentationComment {
  name: string;
  valueText: string | undefined;
}

/**
 * Describes a documented member.
 */
export interface MemberDocumentation extends DocumentationComment {
  kind: MemberKind;
  title: string;
  name: string;
  indexLabel: string;
  indexGroup: 'Field' | 'Property' | 'Method';
  sortKey: string;
  signatureLines: readonly string[];
}

/**
 * Describes a documented top-level declaration.
 */
export interface DeclarationDocumentation extends DocumentationComment {
  kind: DeclarationKind;
  title: string;
  name: string;
  indexLabel: string;
  sortKey: string;
  signatureLines: readonly string[];
  enumValues: readonly EnumValueDocumentation[];
  members: readonly MemberDocumentation[];
}

/**
 * Describes a documented exported module.
 */
export interface ModuleDocumentation {
  exportPath: string;
  title: string;
  entryFilePath: string;
  declarations: readonly DeclarationDocumentation[];
}

/**
 * Describes the full generated documentation model for a package.
 */
export interface PackageDocumentation {
  packageName: string;
  packageVersion: string | undefined;
  packageDescription: string | undefined;
  modules: readonly ModuleDocumentation[];
}

/**
 * Creates an empty documentation comment object.
 */
export const createEmptyDocumentationComment = (): DocumentationComment => ({
  summary: undefined,
  typeParameters: [],
  parameters: [],
  returns: undefined,
  remarks: undefined,
  examples: [],
  seeAlso: [],
});
