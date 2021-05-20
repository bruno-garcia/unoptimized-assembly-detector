#!/bin/bash
set -eu

rm -rf ~/.nuget/packages/unoptimizedassemblydetector/
rm -rf ~/nuget-local-feed/packages/unoptimizedassemblydetector/
rm -f ./nupkg/*.nupkg

dotnet clean -nologo

dotnet pack -c release -nologo src/UnoptimizedAssemblyDetector/UnoptimizedAssemblyDetector.csproj

dotnet build -c release -f net461 sample/Sample/Sample.csproj
dotnet build -c release -f netcoreapp2.1 sample/Sample/Sample.csproj

# .NET Framework MSBuild:
# msbuild /t:Restore;Build /p:Configuration=Release /p:TargetFrameworkVersion=v4.6.1 sample/Sample/Sample.csproj


