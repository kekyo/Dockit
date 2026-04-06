# Dockit

![Dockit](Images/Dockit.100.png)

[![Project Status: Active – The project has reached a stable, usable state and is being actively developed.](https://www.repostatus.org/badges/latest/active.svg)](https://www.repostatus.org/#active)

|Package|Link|
|:----|:----|
|dockit-cli (.NET CLI)|[![NuGet dockit-cli](https://img.shields.io/nuget/v/dockit-cli.svg?style=flat)](https://www.nuget.org/packages/dockit-cli)|
|dockit-cli (NPM CLI)|[![NPM dockit-cli](https://img.shields.io/npm/v/dockit-cli.svg)](https://www.npmjs.com/package/dockit-cli)|

----

[(For English language)](./README.md)

## これは何ですか？

Dockit は Markdown ドキュメントを自動生成するツールです。  
このリポジトリには次のものが含まれています。

- アセンブリと XML ドキュメントメタデータ向けの `.NET` ジェネレーター
- TypeScript Compiler API を使用する npm プロジェクト向けの TypeScript ジェネレーター

Dockit の利点は、まず Markdown 形式で一度だけドキュメントを生成し、
その後は Pandoc を使って Markdown から各種ドキュメントを生成する点にあります。  
これにより、さまざまな出力形式を対象にできます。

また、NuGet または NPM をインストールするだけで自動的にドキュメントを生成できるため、
他の解決策よりもはるかに簡単に管理できます。

----

## インストール

### .NET

`.NET` ツールを NuGet 経由でインストールします。

```bash
dotnet tool install -g dockit-cli
```

または、事前ビルド済みの .NET Framework バイナリを [GitHub Release page](https://github.com/kekyo/Dockit/releases) から取得できます。

### TypeScript / JavaScript

`NPM` パッケージを npmjs 経由でインストールします。

```bash
npm install -g dockit-cli
```

----

## 使い方

### .NET

`.NET` ジェネレーターは 2 つの位置引数と任意のフラグを受け取ります。

```bash
dockit-dotnet [options] <assembly-path> <output-directory>
```

利用可能なオプション:

- `-h`, `--help`: 使い方を表示します。
- `-l VALUE`, `--initial-level=VALUE`: 生成される Markdown の見出し開始レベルを設定します。デフォルトは `1` です。
- `--scope-visibility=VALUE`: 生成対象に含める最小アクセシビリティを設定します。指定できる値は `public`、`protected`、`protected-internal`、`internal`、`private-protected`、`private` です。デフォルトは `protected` です。
- `--editor-browsable-visibility=VALUE`: 生成対象に含める `EditorBrowsable` 可視性を設定します。指定できる値は `normal`、`advanced`、`always` です。デフォルトは `advanced` です。

実行前に、以下を確認してください。

- 対象アセンブリがすでにビルド済みであること。
- そのプロジェクトで XML ドキュメント出力が有効になっていること。
- XML ドキュメントファイルがアセンブリと同じベース名で同じ場所に配置されていること。たとえば `MyLibrary.dll` と `MyLibrary.xml` のようにします。
- 参照先アセンブリもアセンブリディレクトリに配置されており、メタデータ解決ができること。

SDK スタイルのプロジェクトでは、最小構成は次のとおりです。

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

ライブラリのビルド出力から Markdown を生成する例:

```bash
dockit-dotnet ./src/MyLibrary/bin/Release/net8.0/MyLibrary.dll ./docs/api
```

これにより `./docs/api/MyLibrary.md` が出力されます。

成功時には、解決された入力元と出力先のパスが標準出力に表示されます。

```text
Input assembly: /absolute/path/to/MyLibrary.dll
Input XML: /absolute/path/to/MyLibrary.xml
Output markdown: /absolute/path/to/docs/api/MyLibrary.md
Elapsed time: 123.456 ms
```

通常のビルド後にドキュメントを生成する例:

```bash
dotnet build -c Release
dockit-dotnet ./MyLibrary/bin/Release/net8.0/MyLibrary.dll ./artifacts/docs
```

### TypeScript / JavaScript

TypeScript ジェネレーターは、パッケージルートのパスと出力ディレクトリを受け取ります。

```bash
dockit-ts [options] <project-path> <output-directory>
```

利用可能なオプション:

- `-h`, `--help`: 使い方を表示します。
- `-l VALUE`, `--initial-level=VALUE`: 生成される Markdown の見出し開始レベルを設定します。デフォルトは `1` です。
- `-e VALUE`, `--entry=VALUE`: ソースのエントリポイントを追加します。複数回指定できます。
- `--with-metadata=PATH`: 生成される Markdown 先頭の Metadata 表だけ、指定した `package.json` から読み取ります。

実行前に、以下を確認してください。

- 対象ディレクトリに `package.json` が存在すること。
- 対象プロジェクトが TypeScript または JavaScript の npm パッケージであること。
- export される宣言に、パッケージのエントリポイントから到達できること。
- カスタムコンパイラ設定が必要な場合は、`tsconfig.json` または `jsconfig.json` が利用可能であること。

`package.json` がソースエントリポイントを直接公開していない場合、Dockit は次の順序で探索します。

1. 明示的に指定された `--entry` の値
2. `package.json` の `dockit.entryPoints`
3. `package.json` の `exports`, `types`, `typings`, `module`, `main`
4. `./src/index.ts` や `./src/main.ts` のような慣例的なフォールバックファイル

CLI 指向のパッケージでは、`package.json` にカスタムエントリポイントを設定できます。

```json
{
  "dockit": {
    "entryPoints": {
      ".": "./src/index.ts",
      "./extra": "./src/extra.ts"
    }
  }
}
```

npm パッケージから Markdown を生成する例:

```bash
dockit-ts ./path/to/package ./docs/api
```

これにより `./docs/api/<package-name>.md` が出力されます。

成功時には、解決された入力元と出力先のパスが標準出力に表示されます。

```text
Input project: /absolute/path/to/package
Output markdown: /absolute/path/to/docs/api/<package-name>.md
Elapsed time: 123.456 ms
```

生成された Markdown の先頭には `Metadata` 表が出力されます。
Dockit はパッケージメタデータと Git 情報から、`author`, `buildDate`, `description`, `git.branches`, `git.commit.date`, `git.commit.hash`, `git.commit.message`, `git.tags`, `keywords`, `license`, `main`, `module`, `name`, `type`, `types`, `version` を出力します。
階層を持つメタデータはドット区切りのキーに平坦化され、行はキー名の昇順で並び、配列値はカンマ区切りで出力されます。

`--with-metadata` を指定した場合でも、差し替わるのはこの Metadata 表の参照元だけです。
エントリポイント探索、パッケージ解析、出力ファイル名の決定には、従来通り対象プロジェクト自身の `package.json` が使われます。

ソースファイルを `src` 配下に保持する CLI スタイルのパッケージから Markdown を生成する例:

```bash
dockit-ts --entry ./src/index.ts ./path/to/package ./docs/api
```

Metadata 表だけ別の `package.json` を参照して生成する例:

```bash
dockit-ts --with-metadata ./path/to/metadata/package.json ./path/to/package ./docs/api
```

----

## 応用

Dockit を使用して markdown を生成したら、 [pandoc](https://pandoc.org/) を使用して他のフォーマットに変換することが出来ます。
例えば、.NETアセンブリからmarkdownを生成し、 pandoc を使ってPDFを生成します:

```bash
dockit-dotnet ./MyLibrary/bin/Release/net8.0/MyLibrary.dll ./docs
pandoc ./docs/MyLibrary.md -o ./docs/MyLibrary.pdf
```

他にも、 pandoc でHTMLに変換してから、 [wkhtmltopdf](https://wkhtmltopdf.org/) を使用してPDFを生成できます。
この方法で良い所は、整形後のPDFの形式をCSSで整えることが出来るということです。
このような目的のために、 [サンプルのCSS](./assets/sample.css) を用意しておいたので、参考にして下さい。

但し、現在は wkhtmltopdf が deprecated となってしまったため、今ではお勧めできない方法です。
HTMLからの変換を行う手法として、参考にして下さい:

```bash
pandoc ./docs/MyLibrary.md --reference-links --reference-location=block -t html5 -c ./assets/sample.css --embed-resources --standalone -o ./docs/MyLibrary.html
wkhtmltopdf -s A4 -T 23mm -B 28mm -L 20mm -R 20mm --disable-smart-shrinking --keep-relative-links --zoom 1.0 --footer-spacing 7 --footer-font-name "Noto Sans" --footer-font-size 8 --footer-left "Copyright (c) FooBar. CC-BY" --footer-right "[page]/[topage]" --outline ./docs/MyLibrary.html ./docs/MyLibrary.pdf
```

----

## 背景

残念ながら、多くの（形式的な？）ソフトウェア開発の現場では、
成果物として文書提出が求められるにもかかわらず、
実際にはまったく読まれないドキュメントを必要とするプロジェクトが数多くあります。  
現代のソフトウェア開発環境、とくに IDE は大きく進歩しており、
Language Server Protocol のようなソースコード解析エンジンによって、
ヘルプ情報をエディタ上へ直接表示できるようになっています。

ソフトウェアライブラリのインターフェイス仕様は、
メタデータ情報と結び付けられて LSP エンジンから提供されます。  
かつては同じことを人手で行う必要があり、
それが「リファレンスマニュアル」が存在した理由でもありました。

こうした背景から、私はこのツールの出力品質をさらに高めたり、
より高度なものにしたりする価値をあまり感じていません。  
それよりも、NuGet パッケージを入れる最小限の手間だけで、
出力が生成される状態を作りたいと考えました。

（一定水準のドキュメントが生成されるようには配慮していますが）  
どうせ誰も読まないので、あまり重要ではありません。

Dockit が出力したリファレンス文書を、
大事なお客様にも、そうでもないお客様にも、そのまま提出してください :)

----

## ライセンス

Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)

Under Apache-v2.
