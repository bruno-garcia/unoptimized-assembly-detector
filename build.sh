#!/bin/bash
set -eu

mkdir -p nupkg

rm -rf ~/.nuget/packages/unoptimizedassemblydetector/
rm -rf ~/nuget-local-feed/packages/unoptimizedassemblydetector/
rm -f ./nupkg/*.nupkg

dotnet pack -c release -nologo src/UnoptimizedAssemblyDetector/UnoptimizedAssemblyDetector.csproj

dotnet build -c release -f net461 sample/Sample/Sample.csproj
dotnet build -c release -f netcoreapp2.1 sample/Sample/Sample.csproj
