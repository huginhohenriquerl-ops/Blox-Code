#!/usr/bin/env bash
# Builds dist/BloxCode-x86_64.AppImage from BloxCode.Linux. Runs on Linux; CI uses ubuntu-22.04, the
# oldest base supported, so the result also runs on every newer Arch- and Debian-based distro.
#   linux/packaging/build-appimage.sh
set -euo pipefail

cd "$(dirname "$0")/../.."
root="$PWD"
appdir="$root/build/AppDir"
tools="$root/build/tools"

rm -rf "$appdir"
mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$tools" "$root/dist"

dotnet publish linux/BloxCode.Linux.csproj -c Release -o "$appdir/usr/bin"
chmod +x "$appdir/usr/bin/BloxCode"

install -m 755 linux/packaging/AppRun "$appdir/AppRun"
install -m 644 linux/packaging/bloxcode.desktop "$appdir/bloxcode.desktop"
install -m 644 linux/packaging/bloxcode.desktop "$appdir/usr/share/applications/bloxcode.desktop"
install -m 644 linux/Assets/bloxcode-256.png "$appdir/bloxcode.png"
cp "$appdir/bloxcode.png" "$appdir/.DirIcon"
for size in 48 64 128 256; do
  install -D -m 644 "linux/Assets/bloxcode-$size.png" "$appdir/usr/share/icons/hicolor/${size}x${size}/apps/bloxcode.png"
done

# appimagetool embeds the static type2 runtime: the AppImage needs fusermount (fuse2 or fuse3) but
# not libfuse2 itself.
if [ ! -x "$tools/appimagetool" ]; then
  curl -fsSL -o "$tools/appimagetool" \
    https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
  chmod +x "$tools/appimagetool"
fi

# Extract-and-run: CI machines and containers often have no FUSE.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH=x86_64 "$tools/appimagetool" --no-appstream "$appdir" "$root/dist/BloxCode-x86_64.AppImage"
chmod +x "$root/dist/BloxCode-x86_64.AppImage"
ls -l "$root/dist/BloxCode-x86_64.AppImage"
