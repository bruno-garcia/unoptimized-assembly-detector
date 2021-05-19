#!/bin/bash

set -eu

rm -rf ~/.nuget/packages/unoptimizedassemblydetector/
rm -f ./nupkg/*.nupkg

dotnet clean

dotnet pack -c release -nologo src/UnoptimizedAssemblyDetector/UnoptimizedAssemblyDetector.csproj

# dotnet msbuild /t:DetectUnoptimizedAssembly sample/Sample/Sample.csproj

dotnet restore
dotnet msbuild /t:MyTest sample/Sample/Sample.csproj 
dotnet msbuild /t:DetectUnoptimizedAssembly sample/Sample/Sample.csproj 
