#!/bin/bash
set -eux

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd $SCRIPT_DIR/..

OLD_VERSION="$1"
NEW_VERSION="$2"

sed -i '' -e "1,/<Version>/ s!<Version>.*</Version>!<Version>$NEW_VERSION</Version>!" src/UnoptimizedAssemblyDetector/UnoptimizedAssemblyDetector.csproj
sed -i '' -e 's,Version="[^"]*",Version="'$NEW_VERSION'",g' README.md
