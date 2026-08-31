#!/usr/bin/env bash
set -eu
set -o pipefail

cd "$(dirname "$0")"

dotnet tool restore
dotnet paket restore
dotnet paket generate-load-scripts --group BuildScript --framework net10.0 --type fsx

dotnet fsi build.fsx "$@"
