#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATION="Release"

if [[ $# -gt 0 && "$1" != --* ]]; then
    case "$1" in Debug|Release) CONFIGURATION="$1"; shift ;; *) echo "[run] Unknown configuration '$1'. Use Debug or Release." >&2; exit 1 ;; esac
fi

APP_DIR="${ROOT_DIR}/src/OCCAD.Avalonia/bin/x64/${CONFIGURATION}/net10.0"
APPHOST="${APP_DIR}/OCCAD"
APP_DLL="${APP_DIR}/OCCAD.dll"
RUNTIME_DIR="${APP_DIR}/runtime"
OCCT_DIR="${APP_DIR}/occt"
RES_DIR="${OCCT_DIR}/resources"

fail() { printf '[run] ERROR: %s\n' "$*" >&2; exit 1; }
log() { printf '[run] %s\n' "$*"; }

[[ "$(uname -s)" == "Linux" ]] || fail "Linux is required; use run.ps1 on Windows."
[[ -f "${APP_DLL}" ]] || fail "OCCAD was not built: ${APP_DLL}. Run ./build.sh first."
[[ -f "${RUNTIME_DIR}/libOcctNative.so" ]] || fail "Portable Bridge runtime is missing: ${RUNTIME_DIR}. Re-run ./build.sh."
[[ -d "${RES_DIR}" ]] || fail "Portable OCCT resources are missing: ${RES_DIR}. Re-run ./build.sh."

export OCCT_BRIDGE_NATIVE_DIR="${RUNTIME_DIR}"
export OCCT_ROOT="${OCCT_DIR}"
export CASROOT="${OCCT_DIR}"
export LD_LIBRARY_PATH="${RUNTIME_DIR}${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}"
[[ -d "${RES_DIR}/SHMessage" ]] && export CSF_SHMessage="${RES_DIR}/SHMessage"
[[ -d "${RES_DIR}/XSMessage" ]] && export CSF_XSMessage="${RES_DIR}/XSMessage"
[[ -d "${RES_DIR}/StdResource" ]] && export CSF_StandardDefaults="${RES_DIR}/StdResource"
[[ -d "${RES_DIR}/XSTEPResource" ]] && export CSF_STEPDefaults="${RES_DIR}/XSTEPResource" && export CSF_IGESDefaults="${RES_DIR}/XSTEPResource"
[[ -d "${RES_DIR}/XCAFResources" ]] && export CSF_XCAFDefaults="${RES_DIR}/XCAFResources" && export CSF_PluginDefaults="${RES_DIR}/XCAFResources"
[[ -d "${RES_DIR}/Shaders" ]] && export CSF_ShadersDirectory="${RES_DIR}/Shaders"
[[ -d "${RES_DIR}/Textures" ]] && export CSF_MDTVTexturesDirectory="${RES_DIR}/Textures"

log "Application: ${APP_DIR}"
log "Runtime:     ${RUNTIME_DIR}"
log "OCCT:        ${OCCT_DIR}"

cd "${APP_DIR}"
if [[ -x "${APPHOST}" ]]; then
    exec "${APPHOST}" "$@"
fi
command -v dotnet >/dev/null 2>&1 || fail "dotnet was not found."
exec dotnet "${APP_DLL}" "$@"
