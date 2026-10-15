const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '../../..');
const html = fs.readFileSync(path.join(root, 'Assets/WebGLTemplates/ScrollHunter/index.html'), 'utf8');
const script = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].at(-1)[1]
  .replace(/\{\{\{[\s\S]*?\}\}\}/g, token => token.includes('JSON.stringify') ? '"test"' : 'mock');
const bridge = fs.readFileSync(path.join(root, 'Assets/_Project/Plugins/WebGL/ScrollHunterLanguage.jslib'), 'utf8');
const key = 'ScrollHunter.Options.Language';
let passed = 0;
function check(actual, expected, message) { assert.equal(actual, expected, message); passed++; }

function page(saved, blocked = false, fail = false) {
  const nodes = {};
  for (const id of ['unity-canvas','play','status','progress','language','language-label','credits','fullscreen','loading'])
    nodes[id] = { value: id === 'language' ? '1' : '', style: {}, disabled: false, addEventListener() {}, focus() {} };
  const values = new Map(saved === undefined ? [] : [[key, saved]]);
  let launches = 0;
  const context = vm.createContext({
    document: { getElementById: id => nodes[id], documentElement: {} },
    window: {}, console: { error() {} },
    localStorage: {
      getItem(k) { if (blocked) throw Error('blocked'); return values.get(k) ?? null; },
      setItem(k,v) { if (blocked) throw Error('blocked'); values.set(k,v); }
    },
    async createUnityInstance(canvas, config, progress) {
      launches++; progress(.5); if (fail) throw Error('load failed'); return {};
    },
    LibraryManager: { library: {} }, mergeInto: Object.assign
  });
  vm.runInContext(script, context);
  vm.runInContext(bridge, context);
  return { context, nodes, values, library: context.LibraryManager.library, launches: () => launches };
}

(async () => {
  for (const saved of [undefined, '', '99', '-1', '1x', 'NaN'])
    check(page(saved).nodes.language.value, '1', 'missing/invalid preference defaults to English');
  const buttonNames = ['플레이','Play','プレイ','开始','Jugar'];
  for (let lang = 0; lang < 5; lang++) {
    const p = page(String(lang));
    check(p.nodes.language.value, String(lang), 'saved selection');
    check(p.nodes.play.textContent, buttonNames[lang], 'localized launcher');
    check(p.library.ScrollHunter_GetLaunchLanguage(), -1, 'no selection before Play');
    const load = p.nodes.play.onclick();
    check(p.library.ScrollHunter_GetLaunchLanguage(), lang, 'startup bridge selection');
    check(p.nodes.language.disabled, true, 'language locked during load');
    check(p.nodes.play.disabled, true, 'Play locked during load');
    check(p.values.get(key), String(lang), 'Play remembers selection');
    await load; await p.nodes.play.onclick();
    check(p.launches(), 1, 'repeated click cannot create another player');
    check(p.nodes.loading.style.display, 'none', 'successful load reveals game');
    const next = (lang + 1) % 5;
    p.library.ScrollHunter_RememberLanguage(next);
    check(p.nodes.language.value, String(next), 'in-game Apply updates launcher');
    check(page(p.values.get(key)).nodes.language.value, String(next), 'Apply persists after reload');
    p.library.ScrollHunter_RememberLanguage(99);
    check(p.values.get(key), String(next), 'invalid in-game value rejected');
  }
  for (const invalid of [undefined, null, '1', -1, 5, 1.5, NaN]) {
    const p = page(); p.context.window.scrollHunterLaunchLanguage = invalid;
    check(p.library.ScrollHunter_GetLaunchLanguage(), -1, 'bridge validates startup value');
  }
  const blocked = page(undefined, true);
  blocked.nodes.language.value = '2'; blocked.nodes.language.onchange();
  check(blocked.nodes.play.textContent, 'プレイ', 'change previews language');
  await blocked.nodes.play.onclick();
  check(blocked.library.ScrollHunter_GetLaunchLanguage(), 2, 'blocked storage still allows selection');
  const failed = page('4', false, true); await failed.nodes.play.onclick();
  check(failed.nodes.status.textContent.startsWith('No se pudo'), true, 'localized load failure');
  const result = { passed, failures: 0, scope: 'launcher + JS bridge; real Unity startup verified separately' };
  fs.writeFileSync(path.join(__dirname, 'preload-checks.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
})().catch(error => { console.error(error); process.exitCode = 1; });
