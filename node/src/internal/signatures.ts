/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

import ts from 'typescript';

const renderModifiers = (
  declaration: ts.Node & {
    modifiers?: ts.NodeArray<ts.ModifierLike> | undefined;
  },
  options: {
    includeExport: boolean;
    includeDefaultPublic: boolean;
  }
): string[] => {
  const tokens =
    declaration.modifiers
      ?.filter((modifier) => {
        switch (modifier.kind) {
          case ts.SyntaxKind.ExportKeyword:
            return options.includeExport;
          case ts.SyntaxKind.DefaultKeyword:
          case ts.SyntaxKind.DeclareKeyword:
          case ts.SyntaxKind.AbstractKeyword:
          case ts.SyntaxKind.AsyncKeyword:
          case ts.SyntaxKind.StaticKeyword:
          case ts.SyntaxKind.ReadonlyKeyword:
          case ts.SyntaxKind.ProtectedKeyword:
          case ts.SyntaxKind.PublicKeyword:
          case ts.SyntaxKind.OverrideKeyword:
            return true;
          default:
            return false;
        }
      })
      .map((modifier) => modifier.getText()) ?? [];

  if (
    options.includeDefaultPublic &&
    !tokens.includes('public') &&
    !tokens.includes('protected') &&
    !tokens.includes('private')
  ) {
    return ['public', ...tokens];
  }

  return tokens;
};

const renderTypeParameters = (
  typeParameters: readonly ts.TypeParameterDeclaration[] | undefined
): string =>
  typeParameters === undefined || typeParameters.length === 0
    ? ''
    : `<${typeParameters.map((typeParameter) => typeParameter.name.text).join(',')}>`;

const renderTypeNode = (
  typeNode: ts.TypeNode | undefined,
  fallbackType: ts.Type | undefined,
  checker: ts.TypeChecker
): string => {
  if (typeNode !== undefined) {
    return typeNode.getText();
  }
  if (fallbackType !== undefined) {
    return checker.typeToString(
      fallbackType,
      undefined,
      ts.TypeFormatFlags.NoTruncation |
        ts.TypeFormatFlags.UseAliasDefinedOutsideCurrentScope
    );
  }
  return 'unknown';
};

const renderCallSignatureTexts = (
  declaration: ts.VariableDeclaration,
  exportedName: string,
  checker: ts.TypeChecker
): readonly string[] => {
  const targetNode = declaration.initializer ?? declaration;
  const type = checker.getTypeAtLocation(targetNode);
  const signatures = type.getCallSignatures();
  if (signatures.length === 0) {
    return [];
  }

  return signatures.map((signature) => {
    const suffix = checker.signatureToString(
      signature,
      targetNode,
      ts.TypeFormatFlags.NoTruncation |
        ts.TypeFormatFlags.UseAliasDefinedOutsideCurrentScope
    );
    return `export function ${exportedName}${suffix};`;
  });
};

const renderParameter = (
  parameter: ts.ParameterDeclaration,
  checker: ts.TypeChecker
): string => {
  const modifiers = renderModifiers(parameter, {
    includeExport: false,
    includeDefaultPublic: false,
  });
  const prefix = parameter.dotDotDotToken === undefined ? '' : '...';
  const nameText =
    ts.isIdentifier(parameter.name) || ts.isPrivateIdentifier(parameter.name)
      ? parameter.name.text
      : parameter.name.getText();
  const questionToken = parameter.questionToken === undefined ? '' : '?';
  const typeText = renderTypeNode(
    parameter.type,
    checker.getTypeAtLocation(parameter),
    checker
  );
  const defaultValue =
    parameter.initializer === undefined
      ? ''
      : ` = ${parameter.initializer.getText()}`;

  return `${modifiers.length >= 1 ? `${modifiers.join(' ')} ` : ''}${prefix}${nameText}${questionToken}: ${typeText}${defaultValue}`;
};

const renderParameters = (
  parameters: readonly ts.ParameterDeclaration[],
  checker: ts.TypeChecker
): string =>
  parameters.map((parameter) => renderParameter(parameter, checker)).join(', ');

const renderHeritageClauses = (
  declaration: ts.InterfaceDeclaration | ts.ClassDeclaration
): string => {
  const clauses =
    declaration.heritageClauses?.map((heritageClause) => {
      const keyword =
        heritageClause.token === ts.SyntaxKind.ExtendsKeyword
          ? 'extends'
          : 'implements';
      const types = heritageClause.types
        .map((typeNode) => typeNode.getText())
        .join(', ');
      return `${keyword} ${types}`;
    }) ?? [];

  return clauses.length >= 1 ? ` ${clauses.join(' ')}` : '';
};

/**
 * Renders a top-level declaration signature.
 */
export const renderDeclarationSignatures = (
  declaration: ts.Declaration,
  exportedName: string,
  checker: ts.TypeChecker
): readonly string[] => {
  if (ts.isClassDeclaration(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: true,
      includeDefaultPublic: false,
    });
    const className = declaration.name?.text ?? exportedName;
    return [
      `${modifiers.join(' ')} class ${className}${renderTypeParameters(declaration.typeParameters)}${renderHeritageClauses(declaration)}`.trim(),
      '{',
      '    // Total members: 0',
      '}',
    ];
  }

  if (ts.isInterfaceDeclaration(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: true,
      includeDefaultPublic: false,
    });
    const interfaceName = declaration.name.text;
    return [
      `${modifiers.join(' ')} interface ${interfaceName}${renderTypeParameters(declaration.typeParameters)}${renderHeritageClauses(declaration)}`.trim(),
      '{',
      '    // Total members: 0',
      '}',
    ];
  }

  if (ts.isEnumDeclaration(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: true,
      includeDefaultPublic: false,
    });
    const lines = [
      `${modifiers.join(' ')} enum ${declaration.name.text}`.trim(),
      '{',
    ];
    declaration.members.forEach((member, index) => {
      const name = member.name.getText();
      const initializer =
        member.initializer === undefined
          ? ''
          : ` = ${member.initializer.getText()}`;
      lines.push(
        `    ${name}${initializer}${index < declaration.members.length - 1 ? ',' : ''}`
      );
    });
    lines.push('}');
    return lines;
  }

  if (ts.isFunctionDeclaration(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: true,
      includeDefaultPublic: false,
    });
    const signature = checker.getSignatureFromDeclaration(declaration);
    const returnType =
      signature === undefined
        ? renderTypeNode(declaration.type, undefined, checker)
        : checker.typeToString(
            checker.getReturnTypeOfSignature(signature),
            declaration,
            ts.TypeFormatFlags.NoTruncation |
              ts.TypeFormatFlags.UseAliasDefinedOutsideCurrentScope
          );
    return [
      `${modifiers.join(' ')} function ${exportedName}${renderTypeParameters(declaration.typeParameters)}(${renderParameters(declaration.parameters, checker)}): ${returnType};`.trim(),
    ];
  }

  if (ts.isTypeAliasDeclaration(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: true,
      includeDefaultPublic: false,
    });
    if (ts.isTypeLiteralNode(declaration.type)) {
      return [
        `${modifiers.join(' ')} type ${declaration.name.text}${renderTypeParameters(declaration.typeParameters)} = {`.trim(),
        '    // Total members: 0',
        '};',
      ];
    }
    return [
      `${modifiers.join(' ')} type ${declaration.name.text}${renderTypeParameters(declaration.typeParameters)} = ${declaration.type.getText()};`.trim(),
    ];
  }

  if (ts.isVariableDeclaration(declaration)) {
    if (
      declaration.initializer !== undefined &&
      (ts.isArrowFunction(declaration.initializer) ||
        ts.isFunctionExpression(declaration.initializer))
    ) {
      const functionSignatures = renderCallSignatureTexts(
        declaration,
        exportedName,
        checker
      );
      if (functionSignatures.length >= 1) {
        return functionSignatures;
      }
    }

    const declarationList = declaration.parent;
    const declarationStatement = declarationList.parent;
    const modifiers = ts.isVariableStatement(declarationStatement)
      ? renderModifiers(declarationStatement, {
          includeExport: true,
          includeDefaultPublic: false,
        })
      : ['export'];
    const declarationKeyword =
      (ts.getCombinedNodeFlags(declarationList) & ts.NodeFlags.Const) !== 0
        ? 'const'
        : (ts.getCombinedNodeFlags(declarationList) & ts.NodeFlags.Let) !== 0
          ? 'let'
          : 'var';
    const typeText = renderTypeNode(
      declaration.type,
      checker.getTypeAtLocation(declaration),
      checker
    );
    return [
      `${modifiers.join(' ')} ${declarationKeyword} ${exportedName}: ${typeText};`
        .replace(/\s+/g, ' ')
        .trim(),
    ];
  }

  return [`export ${exportedName};`];
};

/**
 * Renders one class or interface member signature.
 */
export const renderMemberSignatures = (
  declaration: ts.Declaration,
  checker: ts.TypeChecker
): readonly string[] => {
  if (ts.isConstructorDeclaration(declaration)) {
    return [
      `${renderModifiers(declaration, { includeExport: false, includeDefaultPublic: true }).join(' ')} constructor(${renderParameters(declaration.parameters, checker)});`.trim(),
    ];
  }

  if (ts.isPropertyDeclaration(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: false,
      includeDefaultPublic: true,
    });
    const name = declaration.name.getText();
    const questionToken = declaration.questionToken === undefined ? '' : '?';
    const typeText = renderTypeNode(
      declaration.type,
      checker.getTypeAtLocation(declaration),
      checker
    );
    return [
      `${modifiers.join(' ')} ${name}${questionToken}: ${typeText};`.trim(),
    ];
  }

  if (ts.isPropertySignature(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: false,
      includeDefaultPublic: false,
    });
    const name = declaration.name.getText();
    const questionToken = declaration.questionToken === undefined ? '' : '?';
    const typeText = renderTypeNode(
      declaration.type,
      checker.getTypeAtLocation(declaration),
      checker
    );
    return [
      `${modifiers.join(' ')} ${name}${questionToken}: ${typeText};`.trim(),
    ];
  }

  if (
    ts.isGetAccessorDeclaration(declaration) ||
    ts.isSetAccessorDeclaration(declaration)
  ) {
    const modifiers = renderModifiers(declaration, {
      includeExport: false,
      includeDefaultPublic: true,
    });
    const keyword = ts.isGetAccessorDeclaration(declaration) ? 'get' : 'set';
    const parameters = ts.isGetAccessorDeclaration(declaration)
      ? ''
      : renderParameters(declaration.parameters, checker);
    const suffix = ts.isGetAccessorDeclaration(declaration)
      ? `: ${renderTypeNode(declaration.type, checker.getTypeAtLocation(declaration), checker)}`
      : '';
    return [
      `${modifiers.join(' ')} ${keyword} ${declaration.name.getText()}(${parameters})${suffix};`.trim(),
    ];
  }

  if (
    ts.isMethodDeclaration(declaration) ||
    ts.isMethodSignature(declaration)
  ) {
    const modifiers = renderModifiers(declaration, {
      includeExport: false,
      includeDefaultPublic: ts.isMethodDeclaration(declaration),
    });
    const name = declaration.name.getText();
    const signature = checker.getSignatureFromDeclaration(declaration);
    const returnType =
      signature === undefined
        ? renderTypeNode(declaration.type, undefined, checker)
        : checker.typeToString(
            checker.getReturnTypeOfSignature(signature),
            declaration,
            ts.TypeFormatFlags.NoTruncation |
              ts.TypeFormatFlags.UseAliasDefinedOutsideCurrentScope
          );
    return [
      `${modifiers.join(' ')} ${name}${renderTypeParameters(declaration.typeParameters)}(${renderParameters(declaration.parameters, checker)}): ${returnType};`.trim(),
    ];
  }

  if (ts.isIndexSignatureDeclaration(declaration)) {
    const modifiers = renderModifiers(declaration, {
      includeExport: false,
      includeDefaultPublic: false,
    });
    const returnType = renderTypeNode(declaration.type, undefined, checker);
    return [
      `${modifiers.join(' ')} [${renderParameters(declaration.parameters, checker)}]: ${returnType};`.trim(),
    ];
  }

  return [declaration.getText()];
};
