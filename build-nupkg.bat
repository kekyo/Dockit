@echo off

rem Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
rem Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
rem
rem Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0

echo.
echo "==========================================================="
echo "Build Dockit"
echo.

rem git clean -xfd

dotnet restore
dotnet pack -p:Configuration=Release -p:Platform="Any CPU" -o artifacts Dockit.sln
