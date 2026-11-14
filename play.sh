#!/usr/bin/env bash
# Builds the Godot client and launches the game.
#   ./play.sh            play
#   ./play.sh --editor   open the project in the Godot editor
# Set GODOT to your Godot .NET binary if it is not in the default macOS location.
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
godot="${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
project="$root/src/Factory.Game"

if [[ ! -x "$godot" ]]; then
    echo "Godot .NET not found at $godot; set GODOT=/path/to/Godot" >&2
    exit 1
fi

dotnet build "$project/Factory.Game.csproj" -v quiet -nologo

# A fresh checkout has no .godot/ import cache yet; build it once before running.
if [[ ! -d "$project/.godot/imported" ]]; then
    "$godot" --headless --path "$project" --import >/dev/null 2>&1 || true
fi

if [[ "${1:-}" == "--editor" ]]; then
    exec "$godot" --editor --path "$project"
fi

exec "$godot" --path "$project"
