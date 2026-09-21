#!/bin/sh
# Install Switcher for the current user (Linux X11).
set -e
ROOT="$(CDPATH= cd -- "$(dirname "$0")" && pwd)"
BIN="${XDG_DATA_HOME:-$HOME/.local/share}/Switcher"
mkdir -p "$BIN"
cp -a "$ROOT/Switcher" "$ROOT/dict" "$BIN/"
chmod +x "$BIN/Switcher"
"$BIN/Switcher" --autostart
echo "Installed: $BIN/Switcher"
echo "Autostart: ~/.config/autostart/switcher.desktop"
echo "Settings:  ~/.config/Switcher/"
echo "Start now: $BIN/Switcher"
