#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATION="Release"
OUTPUT_ROOT="${ROOT_DIR}/artifacts/publish"
SELF_CONTAINED=true
CREATE_ARCHIVE=true
DEFAULT_SDK_ROOT="${HOME:-}/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64"
BRIDGE_SDK="${OCCTCSHARPBRIDGE_SDK:-${DEFAULT_SDK_ROOT}}"
PROJECT="${ROOT_DIR}/src/OCCAD.Avalonia/OCCAD.Avalonia.csproj"
PACKAGE_NAME="OCCAD-linux-x64"

usage() {
    cat <<'EOF'
Usage: ./publish.sh [Debug|Release] [options]

Options:
  --framework-dependent   Do not bundle the .NET runtime
  --self-contained        Bundle the .NET runtime (default)
  --output <directory>    Publish root (default: artifacts/publish)
  --no-archive            Do not create the .tar.gz package
  -h, --help              Show this help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        Debug|Release) CONFIGURATION="$1"; shift ;;
        --framework-dependent) SELF_CONTAINED=false; shift ;;
        --self-contained) SELF_CONTAINED=true; shift ;;
        --output) [[ $# -ge 2 ]] || { echo "Missing value for --output" >&2; exit 2; }; OUTPUT_ROOT="$2"; shift 2 ;;
        --no-archive) CREATE_ARCHIVE=false; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage; exit 2 ;;
    esac
done

fail() { printf '[publish] ERROR: %s\n' "$*" >&2; exit 1; }
log() { printf '[publish] %s\n' "$*"; }
require_command() { command -v "$1" >/dev/null 2>&1 || fail "Required command was not found: $1"; }

[[ "$(uname -s)" == "Linux" ]] || fail "Linux is required; use publish.ps1 on Windows."
case "$(uname -m)" in x86_64|amd64) ;; *) fail "Linux x64 is required; detected $(uname -m)." ;; esac
[[ -n "${OCCTCSHARPBRIDGE_SDK:-}" || -n "${HOME:-}" ]] || fail "HOME is not set. Set OCCTCSHARPBRIDGE_SDK explicitly."
require_command dotnet
require_command tar

# Managed assemblies and Bridge metadata live at the SDK root. Support both an
# SDK/portable package and an SDK root that is itself the portable package.
for name in OcctNet.dll OcctNet.Avalonia.dll bridge-contract.json bridge-manifest.json; do
    [[ -f "${BRIDGE_SDK}/${name}" ]] || fail "Installed OcctCSharpBridge SDK is missing '${name}' at '${BRIDGE_SDK}'. Run OcctCSharpBridge ./publish.sh or set OCCTCSHARPBRIDGE_SDK."
done

if [[ -f "${BRIDGE_SDK}/portable/package-manifest.json" ]]; then
    PORTABLE_ROOT="${BRIDGE_SDK}/portable"
elif [[ -f "${BRIDGE_SDK}/package-manifest.json" ]]; then
    PORTABLE_ROOT="${BRIDGE_SDK}"
else
    fail "Installed SDK has no portable package manifest under '${BRIDGE_SDK}/portable' or '${BRIDGE_SDK}'."
fi

[[ -f "${PORTABLE_ROOT}/runtime/libOcctNative.so" ]] || fail "Installed SDK is missing portable/runtime/libOcctNative.so: ${PORTABLE_ROOT}"
[[ -d "${PORTABLE_ROOT}/occt/resources" ]] || fail "Installed SDK is missing portable OCCT resources: ${PORTABLE_ROOT}/occt/resources"

export OCCTCSHARPBRIDGE_SDK="${BRIDGE_SDK}"

bash "${ROOT_DIR}/build.sh" "${CONFIGURATION}"

mkdir -p "${OUTPUT_ROOT}"
OUTPUT_ROOT="$(cd "${OUTPUT_ROOT}" && pwd)"
PACKAGE_DIR="${OUTPUT_ROOT}/${PACKAGE_NAME}"
STAGING_DIR="${OUTPUT_ROOT}/.${PACKAGE_NAME}-staging-$$"
ARCHIVE_PATH="${OUTPUT_ROOT}/${PACKAGE_NAME}.tar.gz"
rm -rf "${PACKAGE_DIR}" "${STAGING_DIR}"
mkdir -p "${STAGING_DIR}"

publish_args=(
    publish "${PROJECT}"
    -c "${CONFIGURATION}"
    -r linux-x64
    -p:Platform=x64
    --self-contained "${SELF_CONTAINED}"
    --nologo
    -o "${STAGING_DIR}"
)
dotnet "${publish_args[@]}"

[[ -f "${STAGING_DIR}/OCCAD.dll" ]] || fail "Publish output is missing OCCAD.dll."
[[ -f "${STAGING_DIR}/runtime/libOcctNative.so" ]] || fail "Publish output is missing runtime/libOcctNative.so."
[[ -d "${STAGING_DIR}/occt/resources" ]] || fail "Publish output is missing OCCT resources."
[[ -f "${STAGING_DIR}/bridge-portable-manifest.json" ]] || fail "Publish output is missing bridge-portable-manifest.json."

cat > "${STAGING_DIR}/run.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNTIME_DIR="${APP_DIR}/runtime"
OCCT_DIR="${APP_DIR}/occt"
RES_DIR="${OCCT_DIR}/resources"
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
if [[ -x "${APP_DIR}/OCCAD" ]]; then exec "${APP_DIR}/OCCAD" "$@"; fi
exec dotnet "${APP_DIR}/OCCAD.dll" "$@"
EOF
chmod +x "${STAGING_DIR}/run.sh"
[[ -f "${STAGING_DIR}/OCCAD" ]] && chmod +x "${STAGING_DIR}/OCCAD"

mv "${STAGING_DIR}" "${PACKAGE_DIR}"

if [[ "${CREATE_ARCHIVE}" == true ]]; then
    rm -f "${ARCHIVE_PATH}"
    tar -C "${OUTPUT_ROOT}" -czf "${ARCHIVE_PATH}" "${PACKAGE_NAME}"
    log "Archive: ${ARCHIVE_PATH}"
fi

log "Bridge SDK: ${BRIDGE_SDK}"
log "Portable:   ${PORTABLE_ROOT}"
log "Package:    ${PACKAGE_DIR}"
log "Run:        ${PACKAGE_DIR}/run.sh"
