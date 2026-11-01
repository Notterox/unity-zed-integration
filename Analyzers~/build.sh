#!/usr/bin/env bash
# Runs the analyzer checks, then builds the analyzer and copies it to
# Analyzers~/UnityZedIntegration.Analyzers.dll, the copy the package references (committed).
#
# Usage (Git Bash): Analyzers~/build.sh

set -euo pipefail
cd "$(dirname "$0")"

dotnet run --project UnityZedIntegration.Analyzers.Check -c Release
dotnet build UnityZedIntegration.Analyzers -c Release
cp UnityZedIntegration.Analyzers/bin/Release/netstandard2.0/UnityZedIntegration.Analyzers.dll .
echo "Updated Analyzers~/UnityZedIntegration.Analyzers.dll"
