/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import { mkdir, writeFile } from 'node:fs/promises';
import { basename, dirname, join, resolve } from 'node:path';
import type {
  DeclarationDocumentation,
  DocumentationComment,
  MemberDocumentation,
  ModuleDocumentation,
  PackageDocumentation,
} from './models.js';
import {
  escapeMarkdownText,
  normalizeInlineText,
  toAnchorSlug,
} from './text.js';

interface AnchorMaps {
  moduleAnchors: Map<ModuleDocumentation, string>;
  declarationAnchors: Map<DeclarationDocumentation, string>;
  memberAnchors: Map<MemberDocumentation, string>;
  nameAnchors: Map<string, string>;
}

const createSection = (level: number, title: string): string =>
  `${'#'.repeat(level)} ${title}`;

const createAnchorHref = (markdownFileName: string, anchor: string): string => {
  void markdownFileName;
  return `#${anchor}`;
};

const createAnchorElement = (anchor: string): string =>
  `<a name="${anchor}"></a>`;

const getModuleAnchorTitle = (
  moduleDocumentation: ModuleDocumentation
): string =>
  moduleDocumentation.exportPath === '.'
    ? 'root module'
    : `${moduleDocumentation.exportPath
        .replace(/^\.\//u, '')
        .replaceAll('/', ' ')} module`;

const createAnchorMaps = (
  packageDocumentation: PackageDocumentation
): AnchorMaps => {
  const usedCounts = new Map<string, number>();
  const moduleAnchors = new Map<ModuleDocumentation, string>();
  const declarationAnchors = new Map<DeclarationDocumentation, string>();
  const memberAnchors = new Map<MemberDocumentation, string>();
  const nameAnchors = new Map<string, string>();

  const allocateAnchor = (title: string): string => {
    const baseAnchor = toAnchorSlug(title);
    const count = usedCounts.get(baseAnchor);
    if (count === undefined) {
      usedCounts.set(baseAnchor, 0);
      return baseAnchor;
    }
    const nextCount = count + 1;
    usedCounts.set(baseAnchor, nextCount);
    return `${baseAnchor}-${nextCount}`;
  };

  const addNameAnchor = (name: string, anchor: string): void => {
    if (!nameAnchors.has(name)) {
      nameAnchors.set(name, anchor);
    }
  };

  for (const moduleDocumentation of packageDocumentation.modules) {
    const moduleAnchor = allocateAnchor(
      getModuleAnchorTitle(moduleDocumentation)
    );
    moduleAnchors.set(moduleDocumentation, moduleAnchor);
    addNameAnchor(moduleDocumentation.exportPath, moduleAnchor);

    for (const declaration of moduleDocumentation.declarations) {
      const declarationAnchor = allocateAnchor(declaration.title);
      declarationAnchors.set(declaration, declarationAnchor);
      addNameAnchor(declaration.name, declarationAnchor);
      addNameAnchor(declaration.title, declarationAnchor);

      for (const member of declaration.members) {
        const memberAnchor = allocateAnchor(member.title);
        memberAnchors.set(member, memberAnchor);
        addNameAnchor(member.name, memberAnchor);
        addNameAnchor(member.title, memberAnchor);
      }
    }
  }

  return {
    moduleAnchors,
    declarationAnchors,
    memberAnchors,
    nameAnchors,
  };
};

const renderCommentBlock = (
  lines: string[],
  documentation: DocumentationComment
): void => {
  if (documentation.summary !== undefined) {
    lines.push('');
    lines.push(escapeMarkdownText(documentation.summary));
  }
};

const renderDocumentationTables = (
  lines: string[],
  documentation: DocumentationComment
): void => {
  if (documentation.typeParameters.length >= 1) {
    lines.push('');
    lines.push('|Type parameter|Description|');
    lines.push('|:----|:----|');
    for (const typeParameter of documentation.typeParameters) {
      lines.push(
        `| \`${escapeMarkdownText(typeParameter.name)}\` | ${escapeMarkdownText(
          normalizeInlineText(typeParameter.description)
        )} |`
      );
    }
  }

  if (documentation.parameters.length >= 1) {
    lines.push('');
    lines.push('|Parameter|Description|');
    lines.push('|:----|:----|');
    for (const parameter of documentation.parameters) {
      lines.push(
        `| \`${escapeMarkdownText(parameter.name)}\` | ${escapeMarkdownText(
          normalizeInlineText(parameter.description)
        )} |`
      );
    }
  }

  if (documentation.returns !== undefined) {
    lines.push('');
    lines.push('|Return value|');
    lines.push('|:----|');
    lines.push(
      `| ${escapeMarkdownText(normalizeInlineText(documentation.returns))} |`
    );
  }
};

const renderCommentDetails = (
  lines: string[],
  documentation: DocumentationComment,
  anchors: AnchorMaps,
  markdownFileName: string
): void => {
  if (documentation.remarks !== undefined) {
    lines.push('');
    lines.push(escapeMarkdownText(documentation.remarks));
  }

  for (const example of documentation.examples) {
    lines.push('');
    lines.push('```ts');
    lines.push(example);
    lines.push('```');
  }

  if (documentation.seeAlso.length >= 1) {
    const values = documentation.seeAlso.map((entry) => {
      const anchor = anchors.nameAnchors.get(entry);
      return anchor === undefined
        ? escapeMarkdownText(entry)
        : `[${escapeMarkdownText(entry)}](${createAnchorHref(markdownFileName, anchor)})`;
    });
    lines.push('');
    lines.push(`See also: ${values.join(', ')}`);
  }
};

const renderAnchoredSection = (
  lines: string[],
  level: number,
  title: string,
  anchor: string
): void => {
  lines.push(createAnchorElement(anchor));
  lines.push('');
  lines.push(createSection(level, title));
};

const renderSignatureBlock = (
  lines: string[],
  signatureLines: readonly string[]
): void => {
  lines.push('');
  lines.push('```ts');
  lines.push(...signatureLines);
  lines.push('```');
};

const renderMemberIndex = (
  lines: string[],
  declaration: DeclarationDocumentation,
  anchors: AnchorMaps,
  markdownFileName: string
): void => {
  if (declaration.members.length === 0) {
    return;
  }

  lines.push('');
  lines.push('|Member type|Members|');
  lines.push('|:----|:----|');

  for (const group of ['Field', 'Property', 'Method'] as const) {
    const members = declaration.members.filter(
      (member) => member.indexGroup === group
    );
    if (members.length === 0) {
      continue;
    }
    const memberLinks = members
      .map(
        (member) =>
          `[ \`${escapeMarkdownText(member.indexLabel)}\` ](${createAnchorHref(markdownFileName, anchors.memberAnchors.get(member)!)})`
      )
      .join(', ');
    lines.push(`|${group}| ${memberLinks} |`);
  }
};

const renderEnumValueTable = (
  lines: string[],
  declaration: DeclarationDocumentation
): void => {
  if (declaration.enumValues.length === 0) {
    return;
  }

  lines.push('');
  lines.push('|Enum value|Description|');
  lines.push('|:----|:----|');
  for (const enumValue of declaration.enumValues) {
    const description = [enumValue.summary, enumValue.remarks]
      .filter((value): value is string => value !== undefined)
      .map((value) => normalizeInlineText(value))
      .join(' ');
    lines.push(
      `| \`${escapeMarkdownText(enumValue.name)}\` | ${escapeMarkdownText(description)} |`
    );
  }
};

const renderDeclaration = (
  lines: string[],
  declaration: DeclarationDocumentation,
  initialLevel: number,
  anchors: AnchorMaps,
  markdownFileName: string
): void => {
  lines.push('');
  renderAnchoredSection(
    lines,
    initialLevel + 2,
    declaration.title,
    anchors.declarationAnchors.get(declaration)!
  );
  renderCommentBlock(lines, declaration);
  renderSignatureBlock(lines, declaration.signatureLines);
  renderEnumValueTable(lines, declaration);
  renderDocumentationTables(lines, declaration);
  renderCommentDetails(lines, declaration, anchors, markdownFileName);
  renderMemberIndex(lines, declaration, anchors, markdownFileName);

  for (const member of declaration.members) {
    lines.push('');
    renderAnchoredSection(
      lines,
      initialLevel + 3,
      member.title,
      anchors.memberAnchors.get(member)!
    );
    renderCommentBlock(lines, member);
    renderSignatureBlock(lines, member.signatureLines);
    renderDocumentationTables(lines, member);
    renderCommentDetails(lines, member, anchors, markdownFileName);
  }
};

const renderModuleIndex = (
  lines: string[],
  moduleDocumentation: ModuleDocumentation,
  anchors: AnchorMaps,
  markdownFileName: string
): void => {
  lines.push('');
  lines.push('|Type|Members|');
  lines.push('|:----|:----|');

  for (const declaration of moduleDocumentation.declarations) {
    const memberList = declaration.members
      .map(
        (member) =>
          `[ \`${escapeMarkdownText(member.indexLabel)}\` ](${createAnchorHref(markdownFileName, anchors.memberAnchors.get(member)!)})`
      )
      .join(', ');
    lines.push(
      `| [ \`${escapeMarkdownText(declaration.indexLabel)}\` ](${createAnchorHref(markdownFileName, anchors.declarationAnchors.get(declaration)!)}) | ${memberList} |`
    );
  }
};

const renderModule = (
  lines: string[],
  moduleDocumentation: ModuleDocumentation,
  initialLevel: number,
  anchors: AnchorMaps,
  markdownFileName: string
): void => {
  lines.push('');
  renderAnchoredSection(
    lines,
    initialLevel + 1,
    moduleDocumentation.title,
    anchors.moduleAnchors.get(moduleDocumentation)!
  );
  renderModuleIndex(lines, moduleDocumentation, anchors, markdownFileName);

  for (const declaration of moduleDocumentation.declarations) {
    renderDeclaration(
      lines,
      declaration,
      initialLevel,
      anchors,
      markdownFileName
    );
  }
};

const sanitizePackageName = (packageName: string): string =>
  packageName.replace(/^@/u, '').replaceAll('/', '.');

/**
 * Resolves the Markdown output file path for a package.
 */
export const getMarkdownOutputPath = (
  outputDirectory: string,
  packageName: string
): string =>
  join(resolve(outputDirectory), `${sanitizePackageName(packageName)}.md`);

/**
 * Writes package documentation to a Dockit-style Markdown file.
 */
export const writeMarkdown = async (
  markdownPath: string,
  packageDocumentation: PackageDocumentation,
  initialLevel: number
): Promise<void> => {
  const anchors = createAnchorMaps(packageDocumentation);
  const markdownFileName = basename(markdownPath);
  const lines: string[] = [];

  lines.push(
    createSection(initialLevel, `${packageDocumentation.packageName} package`)
  );
  lines.push('');
  lines.push('|Metadata|Value|');
  lines.push('|:----|:----|');
  lines.push(
    `| \`PackageVersion\` | ${
      packageDocumentation.packageVersion === undefined
        ? ''
        : `&quot;${escapeMarkdownText(packageDocumentation.packageVersion)}&quot;`
    } |`
  );
  if (packageDocumentation.packageDescription !== undefined) {
    lines.push(
      `| \`PackageDescription\` | ${escapeMarkdownText(
        normalizeInlineText(packageDocumentation.packageDescription)
      )} |`
    );
  }

  lines.push('');
  lines.push('|Module|Declarations|');
  lines.push('|:----|:----|');

  for (const moduleDocumentation of packageDocumentation.modules) {
    const declarationLinks = moduleDocumentation.declarations
      .map(
        (declaration) =>
          `[ \`${escapeMarkdownText(declaration.indexLabel)}\` ](${createAnchorHref(markdownFileName, anchors.declarationAnchors.get(declaration)!)})`
      )
      .join(', ');
    lines.push(
      `| [ \`${escapeMarkdownText(moduleDocumentation.exportPath)}\` ](${createAnchorHref(markdownFileName, anchors.moduleAnchors.get(moduleDocumentation)!)}) | ${declarationLinks} |`
    );
  }

  for (const moduleDocumentation of packageDocumentation.modules) {
    renderModule(
      lines,
      moduleDocumentation,
      initialLevel,
      anchors,
      markdownFileName
    );
  }

  await mkdir(dirname(markdownPath), { recursive: true });
  await writeFile(markdownPath, `${lines.join('\n')}\n`, 'utf8');
};
