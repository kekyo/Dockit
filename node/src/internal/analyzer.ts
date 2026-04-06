/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import ts from 'typescript';
import { extractDocumentation } from './jsdoc.js';
import type {
  DeclarationDocumentation,
  DeclarationKind,
  EnumValueDocumentation,
  MemberDocumentation,
  MemberKind,
  ModuleDocumentation,
  PackageDocumentation,
} from './models.js';
import { loadProjectConfiguration } from './package-config.js';
import {
  renderDeclarationSignatures,
  renderMemberSignatures,
} from './signatures.js';

export interface AnalyzeProjectOptions {
  entryPaths?: readonly string[];
}

const getActualSymbol = (
  symbol: ts.Symbol,
  checker: ts.TypeChecker
): ts.Symbol =>
  (symbol.flags & ts.SymbolFlags.Alias) !== 0
    ? checker.getAliasedSymbol(symbol)
    : symbol;

const getDeclarationNameNode = (
  declaration: ts.Declaration
): ts.Node | undefined => {
  if (!('name' in declaration)) {
    return undefined;
  }

  const name = declaration.name;
  return name == null ? undefined : (name as ts.Node);
};

const getModifiers = (declaration: ts.Declaration): readonly ts.Modifier[] =>
  ts.canHaveModifiers(declaration) ? (ts.getModifiers(declaration) ?? []) : [];

const isPrivateMember = (declaration: ts.Declaration): boolean => {
  const nameNode = getDeclarationNameNode(declaration);
  if (nameNode !== undefined && ts.isPrivateIdentifier(nameNode)) {
    return true;
  }

  return getModifiers(declaration).some(
    (modifier) => modifier.kind === ts.SyntaxKind.PrivateKeyword
  );
};

const createEnumValueDocumentation = (
  declaration: ts.EnumMember,
  checker: ts.TypeChecker
): EnumValueDocumentation => {
  const symbol = checker.getSymbolAtLocation(declaration.name);
  const documentation = extractDocumentation(
    symbol,
    symbol?.getDeclarations() ?? [declaration],
    checker
  );

  return {
    ...documentation,
    name: declaration.name.getText(),
    valueText: declaration.initializer?.getText(),
  };
};

const createMemberTitle = (
  kind: MemberKind,
  name: string,
  indexLabel: string
): string => {
  switch (kind) {
    case 'constructor':
      return 'Constructor';
    case 'field':
      return `${name} field`;
    case 'property':
      return `${name} property`;
    case 'method':
      return `${indexLabel} method`;
    case 'index-signature':
      return `${name} index signature`;
  }
};

const getMemberIndexGroup = (
  kind: MemberKind
): 'Field' | 'Property' | 'Method' => {
  switch (kind) {
    case 'field':
      return 'Field';
    case 'property':
    case 'index-signature':
      return 'Property';
    case 'constructor':
    case 'method':
      return 'Method';
  }
};

const createMemberDocumentation = (
  declarations: readonly ts.Declaration[],
  checker: ts.TypeChecker
): MemberDocumentation | undefined => {
  const visibleDeclarations = declarations.filter(
    (declaration) => !isPrivateMember(declaration)
  );
  if (visibleDeclarations.length === 0) {
    return undefined;
  }

  const declaration = visibleDeclarations[0];
  const nameNode = getDeclarationNameNode(declaration);
  const symbol =
    nameNode === undefined ? undefined : checker.getSymbolAtLocation(nameNode);
  const documentation = extractDocumentation(
    symbol,
    symbol?.getDeclarations() ?? visibleDeclarations,
    checker
  );

  if (ts.isConstructorDeclaration(declaration)) {
    return {
      ...documentation,
      kind: 'constructor',
      name: 'constructor',
      indexLabel: 'constructor()',
      indexGroup: 'Method',
      sortKey: 'constructor',
      title: 'Constructor',
      signatureLines: visibleDeclarations.flatMap((candidate) =>
        renderMemberSignatures(candidate, checker)
      ),
    };
  }

  if (ts.isPropertyDeclaration(declaration)) {
    const name = declaration.name.getText();
    return {
      ...documentation,
      kind: 'field',
      name,
      indexLabel: name,
      indexGroup: 'Field',
      sortKey: name,
      title: createMemberTitle('field', name, name),
      signatureLines: renderMemberSignatures(declaration, checker),
    };
  }

  if (ts.isPropertySignature(declaration)) {
    const name = declaration.name.getText();
    return {
      ...documentation,
      kind: 'property',
      name,
      indexLabel: name,
      indexGroup: 'Property',
      sortKey: name,
      title: createMemberTitle('property', name, name),
      signatureLines: renderMemberSignatures(declaration, checker),
    };
  }

  if (
    ts.isGetAccessorDeclaration(declaration) ||
    ts.isSetAccessorDeclaration(declaration)
  ) {
    const name = declaration.name.getText();
    return {
      ...documentation,
      kind: 'property',
      name,
      indexLabel: name,
      indexGroup: 'Property',
      sortKey: name,
      title: createMemberTitle('property', name, name),
      signatureLines: visibleDeclarations.flatMap((candidate) =>
        renderMemberSignatures(candidate, checker)
      ),
    };
  }

  if (
    ts.isMethodDeclaration(declaration) ||
    ts.isMethodSignature(declaration)
  ) {
    const name = declaration.name.getText();
    const indexLabel = `${name}()`;
    return {
      ...documentation,
      kind: 'method',
      name,
      indexLabel,
      indexGroup: 'Method',
      sortKey: indexLabel,
      title: createMemberTitle('method', name, indexLabel),
      signatureLines: visibleDeclarations.flatMap((candidate) =>
        renderMemberSignatures(candidate, checker)
      ),
    };
  }

  if (ts.isIndexSignatureDeclaration(declaration)) {
    const name = declaration.parameters
      .map((parameter) => parameter.getText())
      .join(', ');
    const indexLabel = `[${name}]`;
    return {
      ...documentation,
      kind: 'index-signature',
      name: indexLabel,
      indexLabel,
      indexGroup: 'Property',
      sortKey: indexLabel,
      title: createMemberTitle('index-signature', indexLabel, indexLabel),
      signatureLines: renderMemberSignatures(declaration, checker),
    };
  }

  return undefined;
};

const createDeclarationKind = (
  declaration: ts.Declaration
): DeclarationKind | undefined => {
  if (ts.isClassDeclaration(declaration)) {
    return 'class';
  }
  if (ts.isInterfaceDeclaration(declaration)) {
    return 'interface';
  }
  if (ts.isEnumDeclaration(declaration)) {
    return 'enum';
  }
  if (ts.isFunctionDeclaration(declaration)) {
    return 'function';
  }
  if (ts.isTypeAliasDeclaration(declaration)) {
    return 'type-alias';
  }
  if (ts.isVariableDeclaration(declaration)) {
    if (
      declaration.initializer !== undefined &&
      (ts.isArrowFunction(declaration.initializer) ||
        ts.isFunctionExpression(declaration.initializer))
    ) {
      return 'function';
    }
    return (ts.getCombinedNodeFlags(declaration.parent) &
      ts.NodeFlags.Const) !==
      0
      ? 'constant'
      : 'variable';
  }
  return undefined;
};

const createDeclarationTitle = (
  kind: DeclarationKind,
  name: string
): string => {
  switch (kind) {
    case 'class':
      return `${name} class`;
    case 'interface':
      return `${name} interface`;
    case 'enum':
      return `${name} enum`;
    case 'function':
      return `${name}() function`;
    case 'type-alias':
      return `${name} type alias`;
    case 'constant':
      return `${name} constant`;
    case 'variable':
      return `${name} variable`;
  }
};

const getDeclarationIndexLabel = (
  kind: DeclarationKind,
  name: string
): string => (kind === 'function' ? `${name}()` : name);

const collectStructuredMembers = (
  members: readonly (
    | ts.ClassElement
    | ts.TypeElement
    | ts.ObjectTypeDeclaration
  )[],
  checker: ts.TypeChecker
): readonly MemberDocumentation[] => {
  const grouped = new Map<string, ts.Declaration[]>();

  for (const member of members) {
    if (
      !ts.isConstructorDeclaration(member) &&
      !ts.isPropertyDeclaration(member) &&
      !ts.isPropertySignature(member) &&
      !ts.isGetAccessorDeclaration(member) &&
      !ts.isSetAccessorDeclaration(member) &&
      !ts.isMethodDeclaration(member) &&
      !ts.isMethodSignature(member) &&
      !ts.isIndexSignatureDeclaration(member)
    ) {
      continue;
    }

    const key = ts.isConstructorDeclaration(member)
      ? 'constructor'
      : ts.isIndexSignatureDeclaration(member)
        ? 'index-signature'
        : member.name.getText();
    const current = grouped.get(key) ?? [];
    current.push(member);
    grouped.set(key, current);
  }

  return [...grouped.values()]
    .map((group) => createMemberDocumentation(group, checker))
    .filter((member): member is MemberDocumentation => member !== undefined)
    .sort((left, right) => left.sortKey.localeCompare(right.sortKey));
};

const updateTypeSummaryLine = (
  lines: readonly string[],
  memberCount: number
): readonly string[] =>
  lines.map((line) =>
    line.includes('// Total members:')
      ? `    // Total members: ${memberCount}`
      : line
  );

const createDeclarationDocumentation = (
  exportSymbol: ts.Symbol,
  checker: ts.TypeChecker
): DeclarationDocumentation | undefined => {
  const actualSymbol = getActualSymbol(exportSymbol, checker);
  const declarations =
    actualSymbol
      .getDeclarations()
      ?.filter(
        (declaration): declaration is ts.Declaration =>
          declaration !== undefined
      ) ?? [];
  const declaration = declarations[0];
  if (declaration === undefined) {
    return undefined;
  }

  const kind = createDeclarationKind(declaration);
  if (kind === undefined) {
    return undefined;
  }

  const exportedName = exportSymbol.getName();
  const documentation = extractDocumentation(
    exportSymbol,
    declarations,
    checker
  );
  const enumValues = ts.isEnumDeclaration(declaration)
    ? declaration.members
        .map((member) => createEnumValueDocumentation(member, checker))
        .sort((left, right) => left.name.localeCompare(right.name))
    : [];
  const members =
    ts.isClassDeclaration(declaration) || ts.isInterfaceDeclaration(declaration)
      ? collectStructuredMembers(declaration.members, checker)
      : ts.isTypeAliasDeclaration(declaration) &&
          ts.isTypeLiteralNode(declaration.type)
        ? collectStructuredMembers(declaration.type.members, checker)
        : [];
  const signatureLines = updateTypeSummaryLine(
    declarations.flatMap((candidate) =>
      renderDeclarationSignatures(candidate, exportedName, checker)
    ),
    members.length
  );

  return {
    ...documentation,
    kind,
    name: exportedName,
    indexLabel: getDeclarationIndexLabel(kind, exportedName),
    sortKey: getDeclarationIndexLabel(kind, exportedName),
    title: createDeclarationTitle(kind, exportedName),
    signatureLines,
    enumValues,
    members,
  };
};

const createModuleDocumentation = (
  sourceFile: ts.SourceFile,
  exportPath: string,
  checker: ts.TypeChecker
): ModuleDocumentation => {
  const moduleSymbol =
    checker.getSymbolAtLocation(sourceFile) ??
    (sourceFile as ts.SourceFile & { symbol?: ts.Symbol }).symbol;
  if (moduleSymbol === undefined) {
    throw new Error(`Failed to resolve module symbol: ${sourceFile.fileName}`);
  }

  const declarations = checker
    .getExportsOfModule(moduleSymbol)
    .map((symbol) => createDeclarationDocumentation(symbol, checker))
    .filter(
      (declaration): declaration is DeclarationDocumentation =>
        declaration !== undefined
    )
    .sort((left, right) => left.sortKey.localeCompare(right.sortKey));

  const title = exportPath === '.' ? 'Root module' : `${exportPath} module`;

  return {
    exportPath,
    title,
    entryFilePath: sourceFile.fileName,
    declarations,
  };
};

/**
 * Analyzes an npm project and produces a documentation model.
 */
export const analyzeProject = async (
  projectPath: string,
  options: AnalyzeProjectOptions = {}
): Promise<PackageDocumentation> => {
  const configuration = await loadProjectConfiguration(projectPath, {
    entryPaths: options.entryPaths,
  });
  const checker = configuration.program.getTypeChecker();
  const modules = configuration.entryPoints.map((entryPoint) => {
    const sourceFile = configuration.program.getSourceFile(
      entryPoint.sourceFilePath
    );
    if (sourceFile === undefined) {
      throw new Error(
        `Entry source file was not included in the TypeScript program: ${entryPoint.sourceFilePath}`
      );
    }

    return createModuleDocumentation(
      sourceFile,
      entryPoint.exportPath,
      checker
    );
  });

  return {
    packageName: configuration.packageName,
    packageVersion: configuration.packageVersion,
    packageDescription: configuration.packageDescription,
    modules: modules.sort((left, right) =>
      left.exportPath.localeCompare(right.exportPath)
    ),
  };
};
