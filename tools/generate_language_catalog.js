// Refresh the editable desktop language catalogs from T(pt, en) calls.
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const call = /\b(?:T|UiLanguage\.Text)\s*\(\s*(?<pt>\$?"(?:\\.|[^"\\])*")\s*,\s*(?<en>\$?"(?:\\.|[^"\\])*")/gs;
const expression = /(?<!\{)\{[^{}]+\}(?!\})/g;

function template(token) {
  const interpolated = token.startsWith('$');
  let value = JSON.parse(interpolated ? token.slice(1) : token);
  if (interpolated) {
    let index = 0;
    value = value.replace(expression, () => `{${index++}}`);
  }
  return value;
}

const entries = new Map();
for (const folder of ['Valley-Keepsake', 'Valley-Keepsake-Linux']) {
  for (const filename of fs.readdirSync(path.join(root, folder)).filter(name => name.endsWith('.cs')).sort()) {
    const source = fs.readFileSync(path.join(root, folder, filename), 'utf8').replace(/^\uFEFF/, '');
    for (const match of source.matchAll(call)) {
      const english = template(match.groups.en);
      const portuguese = template(match.groups.pt);
      if (!entries.has(english)) entries.set(english, portuguese);
    }
  }
}

if (!entries.size) throw new Error('No interface strings found');
const target = path.join(root, 'Languages');
fs.mkdirSync(target, { recursive: true });
for (const [language, name, strings] of [
  ['en', 'English', Object.fromEntries([...entries.keys()].map(key => [key, key]))],
  ['pt-BR', 'Português (Brasil)', Object.fromEntries(entries)]
]) {
  const sorted = Object.fromEntries(Object.entries(strings).sort(([a], [b]) => a.localeCompare(b, 'en')));
  fs.writeFileSync(path.join(target, `${language}.json`), JSON.stringify({ language, name, strings: sorted }, null, 2) + '\n');
}
console.log(`Wrote ${entries.size} desktop interface strings for English and Portuguese`);
