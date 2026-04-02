#!/usr/bin/env bash

set -euo pipefail

configuration="Release"
clean=0
no_restore=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      configuration="${2:?missing configuration value}"
      shift 2
      ;;
    --clean)
      clean=1
      shift
      ;;
    --no-restore)
      no_restore=1
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      echo "Usage: ./build.sh [-c|--configuration Debug|Release] [--clean] [--no-restore]" >&2
      exit 2
      ;;
  esac
done

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_path="$repo_root/Source/FoodPrinterSystem/FoodProcess.csproj"
output_assembly="$repo_root/1.6/Assemblies/FoodProcess.dll"

if command -v msbuild >/dev/null 2>&1; then
  build_tool="msbuild"
elif command -v xbuild >/dev/null 2>&1; then
  build_tool="xbuild"
elif command -v dotnet >/dev/null 2>&1; then
  build_tool="dotnet"
else
  echo "No supported build tool was found on PATH. Install mono + mono-msbuild, or dotnet-sdk as a fallback." >&2
  exit 1
fi

rimworld_managed_dir="${RimWorldManagedDir:-}"
harmony_reference_path="${HarmonyReferencePath:-}"
rimworld_install_dir="${RimWorldInstallDir:-}"

if [[ -z "$rimworld_install_dir" ]]; then
  for candidate in \
    "/mnt/NVME4TB/SteamLibrary/steamapps/common/RimWorld" \
    "$HOME/.local/share/Steam/steamapps/common/RimWorld" \
    "$HOME/.steam/steam/steamapps/common/RimWorld"
  do
    if [[ -d "$candidate" ]]; then
      rimworld_install_dir="$candidate"
      break
    fi
  done
fi

if [[ -z "$rimworld_managed_dir" && -n "$rimworld_install_dir" ]]; then
  for candidate in \
    "$rimworld_install_dir/RimWorldLinux_Data/Managed" \
    "$rimworld_install_dir/RimWorldLinux64_Data/Managed" \
    "$rimworld_install_dir/RimWorldWin64_Data/Managed"
  do
    if [[ -f "$candidate/Assembly-CSharp.dll" ]]; then
      rimworld_managed_dir="$candidate"
      break
    fi
  done
fi

if [[ -z "$harmony_reference_path" ]]; then
  for candidate in \
    "/mnt/NVME4TB/SteamLibrary/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll" \
    "$HOME/.local/share/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll" \
    "$HOME/.steam/steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll" \
    "${rimworld_install_dir:+$rimworld_install_dir/Mods/Harmony/Current/Assemblies/0Harmony.dll}"
  do
    if [[ -n "$candidate" && -f "$candidate" ]]; then
      harmony_reference_path="$candidate"
      break
    fi
  done
fi

if [[ -z "$rimworld_managed_dir" || ! -f "$rimworld_managed_dir/Assembly-CSharp.dll" ]]; then
  echo "RimWorld references not found. Set RimWorldManagedDir to your RimWorld Managed directory." >&2
  exit 1
fi

if [[ -z "$harmony_reference_path" || ! -f "$harmony_reference_path" ]]; then
  echo "Harmony reference not found. Set HarmonyReferencePath to your 0Harmony.dll." >&2
  exit 1
fi

mkdir -p "$repo_root/.dotnet"
export DOTNET_CLI_HOME="$repo_root/.dotnet"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1

if [[ "$build_tool" == "dotnet" ]]; then
  build_args=(
    build
    "$project_path"
    -c
    "$configuration"
    -nologo
    "-p:RimWorldManagedDir=$rimworld_managed_dir"
    "-p:HarmonyReferencePath=$harmony_reference_path"
  )

  if [[ $no_restore -eq 1 ]]; then
    build_args+=(--no-restore)
  fi

  if [[ $clean -eq 1 ]]; then
    clean_args=(
      clean
      "$project_path"
      -c
      "$configuration"
      -nologo
      "-p:RimWorldManagedDir=$rimworld_managed_dir"
      "-p:HarmonyReferencePath=$harmony_reference_path"
    )

    if [[ $no_restore -eq 1 ]]; then
      clean_args+=(--no-restore)
    fi

    dotnet "${clean_args[@]}"
  fi

  dotnet "${build_args[@]}"
else
  common_args=(
    "$project_path"
    /nologo
    "/p:Configuration=$configuration"
    "/p:RimWorldManagedDir=$rimworld_managed_dir"
    "/p:HarmonyReferencePath=$harmony_reference_path"
  )

  if [[ $clean -eq 1 ]]; then
    "$build_tool" "${common_args[@]}" /t:Clean
  fi

  "$build_tool" "${common_args[@]}" /t:Build
fi

if [[ -f "$output_assembly" ]]; then
  echo "Build succeeded: $output_assembly"
else
  echo "Build finished, but output assembly was not found at: $output_assembly" >&2
fi
