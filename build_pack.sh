#!/bin/sh

# Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
# Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
#
# Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0

echo ""
echo "==========================================================="
echo "Build Dockit"
echo ""

VERSION=`rv .`

# git clean -xfd

rm -rf artifacts
mkdir -p artifacts

dotnet restore
dotnet build -p:Configuration=Release -p:Platform="Any CPU" Dockit.sln
zip artifacts/dockit-${VERSION}.zip README.md LICENSE Images/Dockit.100.png
(cd dotnet/Dockit/bin/Release; zip ../../../../artifacts/dockit-${VERSION}.zip */*)
dotnet pack -p:Configuration=Release -p:Platform="Any CPU" -o artifacts Dockit.sln

cd node
npm ci
npm run pack
