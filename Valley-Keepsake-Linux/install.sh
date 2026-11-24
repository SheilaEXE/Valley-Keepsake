#!/usr/bin/env bash
# Instala a versao portatil no perfil do usuario. Nao usa sudo.
set -euo pipefail

source_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
app_dir="${HOME}/.local/share/TradutorModsDeck"
applications_dir="${HOME}/.local/share/applications"
launcher="${applications_dir}/valley-keepsake.desktop"

mkdir -p "$app_dir" "$applications_dir"
cp -a "$source_dir"/. "$app_dir"/
chmod +x "$app_dir/ValleyKeepsake"

cat > "$launcher" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Valley Keepsake
Comment=Gerencie traduções e configurações dos mods de Stardew Valley
Exec=$app_dir/ValleyKeepsake
Path=$app_dir
Icon=$app_dir/Assets/app.png
Terminal=false
Categories=Utility;Game;
StartupNotify=true
EOF

chmod +x "$launcher"
rm -f "${applications_dir}/tradutor-mods-deck.desktop" "$app_dir/TradutorModsDeck"

desktop_dir="${HOME}/Desktop"
if command -v xdg-user-dir >/dev/null 2>&1; then
  detected_desktop_dir="$(xdg-user-dir DESKTOP 2>/dev/null)" || detected_desktop_dir=""
  if [[ -n "$detected_desktop_dir" ]]; then
    desktop_dir="$detected_desktop_dir"
  fi
fi
if [[ -d "$desktop_dir" ]]; then
  desktop_shortcut="${desktop_dir}/Valley Keepsake.desktop"
  cp -f "$launcher" "$desktop_shortcut"
  chmod +x "$desktop_shortcut"
  rm -f "${desktop_dir}/Valley Modkeeper.desktop" "${desktop_dir}/Transferencia de Arquivos de Traducoes de Mods.desktop"
fi

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications_dir" >/dev/null 2>&1 || true
fi

echo
echo "Instalacao concluida!"
echo "Abra o Valley Keepsake pelo menu de aplicativos ou pelo atalho da Area de Trabalho, se disponivel."
