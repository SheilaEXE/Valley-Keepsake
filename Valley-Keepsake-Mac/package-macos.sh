#!/usr/bin/env bash
set -euo pipefail

rid="${1:?Usage: package-macos.sh osx-arm64|osx-x64}"
case "$rid" in
  osx-arm64) label="Apple-Silicon"; expected_arch="arm64" ;;
  osx-x64) label="Intel"; expected_arch="x86_64" ;;
  *) echo "Unsupported runtime: $rid" >&2; exit 2 ;;
esac

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
output_root="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/valley-keepsake-${rid}"
publish_dir="${output_root}/publish"
stage_dir="${output_root}/Valley-Keepsake-macOS-${label}"
app_dir="${stage_dir}/Valley Keepsake.app"
archive="${repo_root}/Valley-Keepsake-v1.0.0-macOS-${label}-EXPERIMENTAL.zip"

mkdir -p "$publish_dir" "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"
dotnet publish "$repo_root/Valley-Keepsake-Mac/ValleyKeepsake.Mac.csproj" \
  --configuration Release --runtime "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:PublishReadyToRun=false \
  --output "$publish_dir" --nologo

cp -R "$publish_dir"/. "$app_dir/Contents/MacOS/"
mkdir -p "$app_dir/Contents/Resources/Assets"
mv "$app_dir/Contents/MacOS/Assets/app.png" "$app_dir/Contents/Resources/Assets/app.png"
rmdir "$app_dir/Contents/MacOS/Assets"
rm -f "$app_dir/Contents/MacOS"/*.pdb
chmod +x "$app_dir/Contents/MacOS/ValleyKeepsake"
cp "$repo_root/Valley-Keepsake-Mac/Info.plist" "$app_dir/Contents/Info.plist"
cp "$repo_root/Valley-Keepsake-Mac/README-MAC.txt" "$stage_dir/README-MAC.txt"

icon_source="$repo_root/Valley-Keepsake-Linux/Assets/app.png"
icon_target="$app_dir/Contents/Resources/AppIcon.icns"
if ! sips -s format icns "$icon_source" --out "$icon_target" >/dev/null 2>&1; then
  iconset="$output_root/AppIcon.iconset"
  mkdir -p "$iconset"
  for spec in \
    16:icon_16x16.png 32:icon_16x16@2x.png \
    32:icon_32x32.png 64:icon_32x32@2x.png \
    128:icon_128x128.png 256:icon_128x128@2x.png \
    256:icon_256x256.png 512:icon_256x256@2x.png \
    512:icon_512x512.png 1024:icon_512x512@2x.png; do
    size="${spec%%:*}"
    name="${spec#*:}"
    sips -z "$size" "$size" "$icon_source" --out "$iconset/$name" >/dev/null
  done
  iconutil -c icns -o "$icon_target" "$iconset"
fi

plutil -lint "$app_dir/Contents/Info.plist"
file "$app_dir/Contents/MacOS/ValleyKeepsake"
lipo -archs "$app_dir/Contents/MacOS/ValleyKeepsake" | grep -w "$expected_arch"

while IFS= read -r -d '' dylib; do
  codesign --force --sign - "$dylib"
done < <(find "$app_dir/Contents/MacOS" -type f -name '*.dylib' -print0)
codesign --force --sign - "$app_dir/Contents/MacOS/ValleyKeepsake"
codesign --force --sign - "$app_dir"
codesign --verify --deep --strict --verbose=2 "$app_dir"

ditto -c -k --sequesterRsrc --keepParent "$stage_dir" "$archive"
unzip -t "$archive" | tail -n 1
echo "Created $archive"
