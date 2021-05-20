#!/bin/bash
set -eu

./build.sh

test() {
  if ! echo $1 | grep "warning : Unoptimized assembly detected: 'EmptyLibDebug.dll'"
  then
    echo "Didn't detect EmptyLibDebug as unoptimized"
    exit -1
  fi
  if ! echo $1 | grep "warning : Unoptimized assembly detected: 'StructureMap.dll'"
  then
    echo "Didn't detect StructureMap as unoptimized"
    exit -1
  fi
}

test "$(dotnet build -c release -f net461 sample/Sample/Sample.csproj)"
test "$(dotnet build -c release -f netcoreapp2.1 sample/Sample/Sample.csproj)"
