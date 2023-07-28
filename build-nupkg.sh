#!/bin/sh

# Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
# Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
#
# Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0

echo ""
echo "==========================================================="
echo "Build Dockit"
echo ""

# git clean -xfd

dotnet restore
dotnet pack -p:Configuration=Release -p:Platform="Any CPU" -o artifacts Dockit.sln
