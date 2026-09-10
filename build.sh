#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATION="${1:-Release}"
DEFAULT_SDK_ROOT="${HOME:-}/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64"
BRIDGE_SDK="${OCCTCSHARPBRIDGE_SDK:-${DEFAULT_SDK_ROOT}}"
PORTABLE_ROOT="${BRIDGE_SDK}/portable"
RUNTIME_ROOT="${PORTABLE_ROOT}/runtime"
SOLUTION="${ROOT_DIR}/OCCAD.sln"
APP_DIR="${ROOT_DIR}/src/OCCAD.Avalonia/bin/x64/${CONFIGURATION}/net10.0"

fail() { printf '[build] ERROR: %s\n' "$*" >&2; exit 1; }
log() { printf '[build] %s\n' "$*"; }
require_command() { command -v "$1" >/dev/null 2>&1 || fail "Required command was not found: $1"; }
json_string() { sed -nE "s/^[[:space:]]*\"$2\"[[:space:]]*:[[:space:]]*\"([^\"]+)\".*/\\1/p" "$1" | head -n 1; }

case "${CONFIGURATION}" in Debug|Release) ;; *) fail "Unknown configuration '${CONFIGURATION}'. Use Debug or Release." ;; esac
[[ "$(uname -s)" == "Linux" ]] || fail "Linux is required; use build.ps1 on Windows."
case "$(uname -m)" in x86_64|amd64) ;; *) fail "Linux x64 is required; detected $(uname -m)." ;; esac
[[ -n "${OCCTCSHARPBRIDGE_SDK:-}" || -n "${HOME:-}" ]] || fail "HOME is not set. Set OCCTCSHARPBRIDGE_SDK explicitly."
require_command dotnet
require_command git

for name in OcctNet.dll OcctNet.Avalonia.dll bridge-contract.json bridge-manifest.json libOcctNative.so; do
    [[ -f "${BRIDGE_SDK}/${name}" ]] || fail "Installed OcctCSharpBridge SDK is missing '${name}' at '${BRIDGE_SDK}'. Run OcctCSharpBridge ./publish.sh or set OCCTCSHARPBRIDGE_SDK."
done
[[ -f "${PORTABLE_ROOT}/package-manifest.json" ]] || fail "Installed SDK is missing portable/package-manifest.json: ${PORTABLE_ROOT}"
[[ -f "${RUNTIME_ROOT}/libOcctNative.so" ]] || fail "Installed SDK is missing portable/runtime/libOcctNative.so: ${RUNTIME_ROOT}"
[[ -d "${PORTABLE_ROOT}/occt/resources" ]] || fail "Installed SDK is missing portable OCCT resources: ${PORTABLE_ROOT}/occt/resources"
compgen -G "${RUNTIME_ROOT}/libTKernel.so*" >/dev/null || fail "Installed SDK portable runtime is missing TKernel: ${RUNTIME_ROOT}"

sdk_version="$(dotnet --version)"
[[ "${sdk_version}" =~ ^10\.0\.[0-9]+$ ]] || fail "OCCAD requires a stable .NET 10 SDK; resolved ${sdk_version}."
bridge_version="$(json_string "${BRIDGE_SDK}/bridge-contract.json" bridgeVersion)"
bridge_source="$(json_string "${BRIDGE_SDK}/bridge-manifest.json" sourceCommit)"
occad_source="$(git -C "${ROOT_DIR}" rev-parse HEAD 2>/dev/null || printf 'unknown')"

export OCCTCSHARPBRIDGE_SDK="${BRIDGE_SDK}"

log "OCCAD source:   ${occad_source}"
log "Bridge SDK:     ${BRIDGE_SDK}"
log "Bridge:         ${bridge_version:-unknown}"
log "Bridge source:  ${bridge_source:-unknown}"
log "Bridge runtime: ${RUNTIME_ROOT}"
log ".NET SDK:       ${sdk_version}"

dotnet build "${SOLUTION}" -c "${CONFIGURATION}" -p:Platform=x64 --nologo

[[ -f "${APP_DIR}/OCCAD.dll" ]] || fail "Build output is missing OCCAD.dll: ${APP_DIR}"
[[ -f "${APP_DIR}/runtime/libOcctNative.so" ]] || fail "Build output is missing runtime/libOcctNative.so: ${APP_DIR}"
[[ -d "${APP_DIR}/occt/resources" ]] || fail "Build output is missing OCCT resources: ${APP_DIR}"
[[ -f "${APP_DIR}/bridge-portable-manifest.json" ]] || fail "Build output is missing bridge-portable-manifest.json: ${APP_DIR}"

log "Application: ${APP_DIR}"
log "Completed."
