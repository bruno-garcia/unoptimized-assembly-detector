#!/bin/bash
set -eu

rm -rf ~/.nuget/packages/unoptimizedassemblydetector/
rm -rf ~/nuget-local-feed/packages/unoptimizedassemblydetector/
rm -f ./nupkg/*.nupkg

dotnet clean

dotnet pack -c release -nologo src/UnoptimizedAssemblyDetector/UnoptimizedAssemblyDetector.csproj

dotnet build -c release -f net6.0 sample/Sample/Sample.csproj

# .NET Framework MSBuild:
# msbuild /t:Build /c:Release /p:TargetFrameworkVersion=v4.8 sample/Sample/Sample.csproj

