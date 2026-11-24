# Translate the Valley Keepsake interface

The desktop apps read one JSON file per interface language. `en.json` is the complete English template; `pt-BR.json` shows the Brazilian Portuguese translations. This setting is separate from the language of the mod translations.

1. Copy `en.json` to a file named after your language code, for example `es.json`.
2. Change `language` to that same code and `name` to the name shown in the app's Interface selector.
3. Translate the **values** in `strings`. Keep every key exactly as it is, including spaces and punctuation. Keep placeholders such as `{0}` and `{1}` in the translated value; they insert mod names, counts, and paths. You may reorder the placeholders to suit your language.
4. Put the file in the `Languages` folder beside the app. For a translation that survives replacing the app folder, put it in your personal Languages folder instead:
   - Windows: `%LOCALAPPDATA%\TradutorModsStardew\Languages`
   - Linux: `~/.local/share/TradutorModsStardew/Languages`
5. Restart the app and select the language under **Interface**.

The personal folder takes precedence if the same language exists in both places. Invalid files are ignored. Missing or empty translations fall back to English. The app keeps your selected interface language between launches; if its file is removed, it temporarily displays English.

When the app adds new interface text, `node tools/generate_language_catalog.js` refreshes the English and Portuguese templates from the desktop source. A community translator can copy any new keys into their own file.
