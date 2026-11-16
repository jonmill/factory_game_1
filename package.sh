#!/usr/bin/env bash
# Exports the Windows playtest build and zips it for sending to playtesters.
#   ./package.sh            -> exports/Factory-<date>-<commit>-windows.zip
# Needs Godot 4.7.2 .NET and its export templates (Editor > Manage Export Templates).
# Set GODOT to your Godot .NET binary if it is not in the default macOS location.
# This is a standard (JIT) .NET build with the runtime bundled; NativeAOT for Windows has to be built on Windows.
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
godot="${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
project="$root/src/Factory.Game"
out="$root/exports/windows"
name="Factory-$(date +%Y%m%d)-$(git -C "$root" rev-parse --short HEAD)"
dirty=""
if [[ -n "$(git -C "$root" status --porcelain)" ]]; then
    dirty="-dirty"
    echo "warning: uncommitted changes are included in this build" >&2
fi

if [[ ! -x "$godot" ]]; then
    echo "Godot .NET not found at $godot; set GODOT=/path/to/Godot" >&2
    exit 1
fi

# A fresh checkout has no .godot/ import cache yet; build it once before exporting.
if [[ ! -d "$project/.godot/imported" ]]; then
    "$godot" --headless --path "$project" --import >/dev/null 2>&1 || true
fi

rm -rf "$out"
mkdir -p "$out"
"$godot" --headless --path "$project" --export-release "Windows Desktop" "$out/Factory.exe" >"$root/exports/export.log" 2>&1 || {
    tail -30 "$root/exports/export.log" >&2
    exit 1
}

if [[ ! -f "$out/Factory.exe" ]] || [[ ! -f "$out/Factory.pck" ]] || [[ ! -d "$out/data_Factory.Game_windows_x86_64" ]]; then
    echo "export is missing files; see exports/export.log" >&2
    exit 1
fi

cp "$root/tools/playtest/README-PLAYTEST.txt" "$out/README.txt"
echo "$name$dirty" >"$out/VERSION.txt"

zip="$root/exports/$name$dirty-windows.zip"
rm -f "$zip"
staging="$root/exports/staging"
rm -rf "$staging"
mkdir -p "$staging"
cp -R "$out" "$staging/Factory"
(cd "$staging" && zip -qr -9 "$zip" Factory)
rm -rf "$staging"

echo "$zip ($(du -h "$zip" | cut -f1))"
