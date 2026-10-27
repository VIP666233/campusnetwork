const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const template = fs.readFileSync(path.join(__dirname, '../src/Portal.js'), 'utf8');
let passed = 0;
function test(name, fn) { fn(); passed++; console.log('PASS: ' + name); }

function fixture(href, overrides = {}) {
  let clicks = 0;
  class Input {
    constructor(type, name) { this.tagName = 'INPUT'; this.type = type; this.name = name; this.id = name; this.disabled = false; this._value = ''; }
    get value() { return this._value; }
    set value(v) { this._value = v; }
    getClientRects() { return [{}]; }
    scrollIntoView() {}
    getBoundingClientRect() { return {left: 10, top: 20, width: 120, height: 30}; }
    dispatchEvent() {}
  }
  class Select extends Input {
    constructor() { super('select', 'ISP_select'); this.tagName = 'SELECT'; this.selectedIndex = 0; this.options = [{ text: '校园网', value: 'campus' }, { text: '中国移动', value: 'cmcc' }, { text: '中国联通', value: 'unicom' }, { text: '中国电信', value: 'telecom' }]; }
    get value() { return this.options[this.selectedIndex] ? this.options[this.selectedIndex].value : ''; }
    set value(v) { this.selectedIndex = this.options.findIndex(o=>o.value===v); }
  }
  const user = new Input('text', 'DDDDD'), password = new Input('password', 'upass'), select = new Select();
  const button = { innerText: '登录', disabled: false, getClientRects: () => [{}], click: () => { clicks++; } };
  const elements = [user, password, select, button];
  const document = {
    readyState: 'complete', body: { innerText: '校园网登录' },
    documentElement: { contains: el => elements.includes(el) },
    querySelectorAll: selector => ({ 'input': [user,password], 'input[type=password]': [password], 'select': [select], 'input,button,a': [button], 'button,input[type=submit],input[type=button],a': [button], 'input[type=radio]': [] }[selector] || []),
    querySelector: selector => { if (selector === '[') throw new Error('Invalid selector'); if (selector === '#area') return select; return null; }
  };
  Object.assign(document, overrides);
  const cfg = { origin: 'http://9.9.9.9', username: 'test-user', password: "local-secret'\"\\汉字", provider: '中国移动', accountSuffix: '', usernameSelector: '', passwordSelector: '', providerSelector: '', providerValue: '', loginSelector: '', phase: 'submit' };
  const context = vm.createContext({ URL, location: new URL(href), document, window: {}, HTMLInputElement: Input, HTMLSelectElement: Select, getComputedStyle: () => ({ visibility: 'visible' }), Event: class {} });
  return { cfg, context, user, password, select, button, document, clicks: () => clicks, run: () => vm.runInContext(template.replace('__CONFIG__', JSON.stringify(cfg)), context) };
}

function addArea(f) {
  const area = new f.select.constructor(); area.name=area.id='area';
  const query=f.document.querySelectorAll;
  f.document.querySelectorAll=s=>s==='select'?[f.select,area]:query(s);
  const contains=f.document.documentElement.contains;
  f.document.documentElement.contains=el=>el===area||contains(el);
  return area;
}

test('Initial blank tab waits without filling a password', () => {
  const f = fixture('about:blank'); assert.equal(f.run().state, 'waiting'); assert.equal(f.password.value, ''); assert.equal(f.clicks(), 0);
});
test('Browser connection failure has a network error, not a redirect error', () => {
  const f = fixture('chrome-error://chromewebdata/'); const r = f.run(); assert.equal(r.state, 'failed'); assert.match(r.message, /无法打开认证页/); assert.equal(f.password.value, '');
});
test('Loading redirected document waits until the document completes', () => {
  const f = fixture('http://10.0.0.1:801/eportal/', { readyState: 'loading' }); assert.equal(f.run().state, 'waiting'); assert.equal(f.password.value, '');
  f.document.readyState = 'interactive'; assert.equal(f.run().state, 'waiting');
});
test('Completed cross-host redirect returns the exact URL without submitting', () => {
  const destination = 'http://10.0.0.1:801/eportal/?wlanacip=10.1.2.3&next=%2Fhome';
  const f = fixture(destination); const r = f.run(); assert.equal(r.state, 'redirected'); assert.equal(r.redirectUrl, destination); assert.equal(f.user.value, ''); assert.equal(f.password.value, ''); assert.equal(f.clicks(), 0); assert.ok(!r.message.includes('wlanacip'));
});
test('Discovery still works when the destination uses a custom login widget', () => {
  const f = fixture('http://10.0.0.1/login'); f.document.querySelectorAll = () => []; assert.equal(f.run().state, 'redirected');
});
test('Same-host port change and HTTPS upgrade can submit the form', () => {
  for (const destination of ['http://9.9.9.9:801/eportal/', 'https://9.9.9.9/login']) {
    const f = fixture(destination); assert.equal(f.run().state, 'submitted'); assert.equal(f.user.value, f.cfg.username); assert.equal(f.password.value, f.cfg.password); assert.equal(f.select.value, 'cmcc'); assert.equal(f.clicks(), 1);
  }
});
test('HTTPS downgrade requires review before credential filling', () => {
  const f = fixture('http://9.9.9.9/login'); f.cfg.origin = 'https://9.9.9.9'; assert.equal(f.run().state, 'redirected'); assert.equal(f.password.value, '');
});
test('Applying the stable destination in the same session submits once', () => {
  const f = fixture('http://10.0.0.1:801/login'); const r = f.run(); assert.equal(r.state, 'redirected'); f.cfg.origin = new URL(r.redirectUrl).origin; assert.equal(f.run().state, 'submitted'); assert.equal(f.clicks(), 1);
});
test('A cross-server redirect after submission continues verification without resubmitting', () => {
  const f = fixture('http://9.9.9.9/'); assert.equal(f.run().state, 'submitted'); f.cfg.phase = 'verify';
  f.context.location = new URL('http://10.2.5.251/a79.htm');
  const redirect = f.run(); assert.equal(redirect.state, 'redirected'); f.cfg.origin = new URL(redirect.redirectUrl).origin;
  assert.equal(f.run().state, 'waiting'); assert.equal(f.clicks(), 1);
  f.document.body.innerText = '您已登录 在线时长'; f.document.querySelectorAll = () => [];
  assert.equal(f.run().state, 'success'); assert.equal(f.clicks(), 1);
});
test('Verification phase never submits a second request', () => {
  const f = fixture('http://9.9.9.9/login'); assert.equal(f.run().state, 'submitted'); f.cfg.phase = 'verify'; assert.equal(f.run().state, 'waiting'); assert.equal(f.clicks(), 1);
  f.document.body.innerText = '密码错误'; assert.equal(f.run().state, 'failed'); assert.equal(f.clicks(), 1);
});
console.log('All ' + passed + ' v1.9.1 portal checks passed.');