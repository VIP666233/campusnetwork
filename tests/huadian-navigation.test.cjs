const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const template = fs.readFileSync(path.join(__dirname, '../src/adapters/Huadian.js'), 'utf8');
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
    querySelectorAll: selector => ({ 'input': [user,password], 'input[type=password]': [password], 'select': [select], 'input,button,a': [button], 'button,input[type=submit],input[type=button],input[type=image],a': [button], 'input[type=radio]': [] }[selector] || []),
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
test('Region field with carrier names selects all four providers by label, not numeric guesses', () => {
  for (const provider of ['校园网','中国移动','中国联通','中国电信']) {
    const f = fixture('http://10.99.0.16/portal.do'); f.cfg.origin='http://10.99.0.16'; f.cfg.provider=provider;
    f.select.name = f.select.id = 'domain';
    f.select.options.forEach((o,i) => { o.value=String(91+i); });
    assert.equal(f.run().state, 'submitted');
    assert.equal(f.select.value, String(91+['校园网','中国移动','中国联通','中国电信'].indexOf(provider)));
  }
});
test('Campus-only portal without a carrier widget submits once', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.provider='校园网';
  const query=f.document.querySelectorAll; f.document.querySelectorAll=s=>s==='select'?[]:query(s);
  assert.equal(f.run().state,'submitted'); assert.equal(f.run().state,'submitted'); assert.equal(f.clicks(),1);
});
test('Named carrier is never silently skipped on a campus-only portal', () => {
  const f=fixture('http://9.9.9.9/'); const query=f.document.querySelectorAll;
  f.document.querySelectorAll=s=>s==='select'?[]:query(s);
  assert.equal(f.run().state,'blocked'); assert.equal(f.password.value,''); assert.equal(f.clicks(),0);
});
test('A valid default region is preserved for campus login', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.provider='校园网';
  f.select.options=[{text:'一区',value:'a'},{text:'二区',value:'b'}]; f.select.selectedIndex=1; f.select.value='b';
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'b');
});
test('Single available region is selected after a placeholder', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.provider='校园网';
  f.select.options=[{text:'请选择',value:''},{text:'主校区',value:'area1'}];
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'area1');
});
test('Multiple regions with no default require a choice before filling credentials', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.provider='校园网';
  f.select.options=[{text:'请选择区域',value:''},{text:'一区',value:'a'},{text:'二区',value:'b'}];
  const r=f.run(); assert.equal(r.state,'blocked'); assert.match(r.message,/一区、二区/); assert.equal(f.password.value,'');
  f.cfg.providerValue='二区'; assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'b');
});
test('Explicit region selector supports actual option value', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.providerSelector='#area'; f.cfg.providerValue='b';
  f.select.options=[{text:'一区',value:'a'},{text:'二区',value:'b'}];
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'b');
});
test('Unknown explicit option cannot silently fall back to another selection', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.providerValue='does-not-exist';
  assert.equal(f.run().state,'blocked'); assert.equal(f.clicks(),0);
});
test('Two carrier matches require an explicit option, disabled matches are excluded', () => {
  const f=fixture('http://9.9.9.9/');
  f.select.options=[{text:'中国移动一区',value:'a'},{text:'中国移动二区',value:'b'}];
  assert.equal(f.run().state,'blocked'); assert.equal(f.clicks(),0);
  f.select.options[0].disabled=true;
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'b');
});
test('Campus mode cannot override a real carrier selector that lacks campus', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.provider='校园网'; f.select.options.shift();
  assert.equal(f.run().state,'blocked'); assert.equal(f.password.value,'');
});
test('Empty region options wait for page data to load', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.provider='校园网'; f.select.options=[];
  assert.equal(f.run().state,'waiting'); assert.equal(f.clicks(),0);
});
test('Account is filled before reading dependent carrier options and never reset while polling', () => {
  const f=fixture('http://9.9.9.9/'); f.select.options=[];
  let changes=0; f.user.dispatchEvent=()=>changes++;
  const first=f.run(); assert.equal(first.state,'waiting'); assert.ok(first.activateSelect);
  assert.equal(f.user.value,f.cfg.username); assert.equal(changes,3); assert.equal(f.password.value,'');
  assert.equal(f.run().activateSelect,undefined); assert.equal(changes,3);
  f.select.options=[{text:'中国移动',value:'actual-service'}];
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'actual-service'); assert.equal(changes,3);
  f.run(); assert.equal(f.clicks(),1);
});
test('Account input can populate options without needing dropdown activation', () => {
  const f=fixture('http://9.9.9.9/'); f.select.options=[];
  f.user.dispatchEvent=()=>{ f.select.options=[{text:'中国移动',value:'dynamic'}]; };
  const r=f.run(); assert.equal(r.state,'submitted'); assert.equal(r.activateSelect,undefined); assert.equal(f.select.value,'dynamic');
});
test('Explicit empty selector activates before reporting a missing requested value', () => {
  const f=fixture('http://9.9.9.9/'); f.cfg.providerSelector='#area'; f.cfg.providerValue='service-b'; f.select.options=[];
  assert.ok(f.run().activateSelect);
  f.select.options=[{text:'二区',value:'service-b'}]; assert.equal(f.run().state,'submitted');
});
test('Permanently empty region has a bounded wait and never guesses or submits', () => {
  const f=fixture('http://9.9.9.9/'); f.select.options=[]; f.run();
  f.context.window.__campusHelperSelectActivation.started-=12001;
  const r=f.run(); assert.equal(r.state,'blocked'); assert.match(r.message,/仍未读到可选项/); assert.equal(f.password.value,''); assert.equal(f.clicks(),0);
});
test('Late options still require the selected carrier to match', () => {
  const f=fixture('http://9.9.9.9/'); f.select.options=[]; f.run();
  f.select.options=[{text:'中国联通',value:'another'}];
  assert.equal(f.run().state,'blocked'); assert.equal(f.password.value,''); assert.equal(f.clicks(),0);
});
test('HTML option label attributes are recognized when text content is empty', () => {
  const f=fixture('http://9.9.9.9/');
  f.select.options=[{text:'',label:'请选择',value:''},{text:'',label:'移动用户',value:'label-mobile'}];
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'label-mobile');
});
test('Common carrier wording and invisible spacing do not require exact labels', () => {
  for (const [provider,label] of [['中国移动','校园移动宽带'],['中国移动','中\u200b国移动'],['中国联通','联通用户'],['中国电信','天翼宽带'],['校园网','校园网用户']]) {
    const f=fixture('http://9.9.9.9/'); f.cfg.provider=provider;
    f.select.options=[{text:'请选择',value:''},{text:label,value:'actual-option'}];
    assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'actual-option');
  }
});
test('Select by actual option index even when placeholder and carrier values repeat', () => {
  const f=fixture('http://9.9.9.9/');
  f.select.options=[{text:'请选择',value:''},{text:'中国移动',value:''}];
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.selectedIndex,1);
});
test('Loaded carrier is chosen before waiting for its initially empty service', () => {
  const f=fixture('http://9.9.9.9/'), area=addArea(f); area.options=[];
  f.select.dispatchEvent=()=>{ area.options=[{text:'一区',value:'a'}]; area.selectedIndex=0; };
  assert.equal(f.run().state,'submitted'); assert.equal(area.value,'a');
});
test('Polling never retriggers carrier change to clear an asynchronous dependent area', () => {
  const f=fixture('http://9.9.9.9/'), area=addArea(f);
  area.options=[{text:'一区',value:'a'}]; let events=0;
  f.select.dispatchEvent=()=>{ events++; area.options=[]; };
  const first=f.run(); assert.equal(first.state,'waiting'); assert.ok(first.activateSelect); assert.equal(events,3);
  assert.equal(f.run().state,'waiting'); assert.equal(events,3);
  area.options=[{text:'二区',value:'b'}]; area.selectedIndex=0;
  assert.equal(f.run().state,'submitted'); assert.equal(events,3); assert.equal(f.clicks(),1);
});
test('Explicit carrier selector does not exclude an empty secondary area from bounded activation', () => {
  const f=fixture('http://9.9.9.9/'), area=addArea(f); f.cfg.providerSelector='#area'; area.options=[];
  assert.ok(f.run().activateSelect);
  f.context.window.__campusHelperSelectActivation.started-=12001;
  assert.equal(f.run().state,'blocked'); assert.equal(f.clicks(),0);
});
test('Dropdown diagnostics exclude values, arbitrary text, credentials and query parameters', () => {
  const f=fixture('http://9.9.9.9/?token=private-token'); f.cfg.provider='校园网';
  f.select.options=[{text:'请选择',value:f.cfg.password},{text:f.cfg.username,value:'secret-option'},{text:'other '+f.cfg.password,value:'v'}];
  const r=f.run(); assert.equal(r.state,'blocked'); assert.match(r.diagnostic,/#1.*选项=3/);
  for (const secret of [f.cfg.username,f.cfg.password,'secret-option','private-token','other ']) assert.ok(!r.diagnostic.includes(secret));
});
test('Placeholder containing a carrier name is never selected as an actual service', () => {
  const f=fixture('http://9.9.9.9/'); f.select.options=[{text:'请选择中国移动服务',value:''}];
  assert.equal(f.run().state,'waiting'); assert.equal(f.clicks(),0); assert.equal(f.password.value,'');
});
test('Static failure instructions are not treated as this attempt failing', () => {
  const f=fixture('http://9.9.9.9/'); f.document.body.innerText='用户办理缴费或登录提示认证失败需刷新号可用微信扫描下方二维码自助办理';
  assert.equal(f.run().state,'submitted'); f.cfg.phase='verify';
  assert.equal(f.run().state,'waiting'); f.document.body.innerText+='\n密码错误';
  assert.equal(f.run().state,'failed');
});
test('Existing failure help is ignored while a newly added error is reported', () => {
  const f=fixture('http://9.9.9.9/'); f.document.body.innerText='认证失败可联系服务台';
  f.run(); f.cfg.phase='verify'; assert.equal(f.run().state,'waiting');
  f.document.body.innerText+='\n账号欠费'; assert.equal(f.run().state,'failed');
});
function huadianFixture() {
  const f=fixture('http://10.99.0.16/portal.do?fixture=true');
  f.cfg.origin='http://10.99.0.16'; f.cfg.provider='校园网';
  f.select.options=[{text:'',value:'server-default-realm'}]; f.select.selectedIndex=0;
  return f;
}
test('Huadian single blank default fills password and submits once without changing the realm', () => {
  const f=huadianFixture(); let events=0; f.select.dispatchEvent=()=>events++;
  const r=f.run(); assert.equal(r.state,'submitted'); assert.match(r.message,/保留网页默认值/);
  assert.equal(f.user.value,f.cfg.username); assert.equal(f.password.value,f.cfg.password);
  assert.equal(f.select.value,'server-default-realm'); assert.equal(events,0); assert.equal(r.activateSelect,undefined);
  f.run(); assert.equal(f.clicks(),1);
  f.cfg.phase='verify'; assert.equal(f.run().state,'waiting');
  f.document.body.innerText='密码错误'; assert.equal(f.run().state,'failed'); assert.equal(f.clicks(),1);
});
test('Huadian blank default also preserves an empty value and whitespace-only label', () => {
  const f=huadianFixture(); f.select.options=[{text:' \u200b ',value:''}];
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,''); assert.equal(f.clicks(),1);
});
test('Huadian default never invents a carrier suffix or changes the school value', () => {
  const f=huadianFixture(); f.cfg.provider='中国移动';
  assert.equal(f.run().state,'submitted'); assert.equal(f.user.value,f.cfg.username); assert.equal(f.select.value,'server-default-realm');
});
test('Huadian exception requires its exact host and path', () => {
  for(const url of ['http://9.9.9.9/portal.do','http://10.99.0.16/other','http://10.99.0.16.evil.test/portal.do']) {
    const f=huadianFixture(); f.context.location=new URL(url); f.cfg.origin=new URL(url).origin;
    assert.equal(f.run().state,'waiting'); assert.equal(f.password.value,''); assert.equal(f.clicks(),0);
  }
});
test('Huadian explicit user selection is never silently bypassed', () => {
  for(const setting of ['providerSelector','providerValue']) {
    const f=huadianFixture(); f.cfg[setting]=setting==='providerSelector'?'#area':'requested-region';
    assert.equal(f.run().state,'waiting'); assert.equal(f.password.value,''); assert.equal(f.clicks(),0);
  }
});
test('Huadian zero options, multiple blank options, placeholder or disabled option cannot submit', () => {
  for(const opts of [[],[{text:'',value:'a'},{text:'',value:'b'}],[{text:'请选择区域',value:''}],[{text:'',value:'a',disabled:true}]]) {
    const f=huadianFixture(); f.select.options=opts;
    assert.equal(f.run().state,'waiting'); assert.equal(f.password.value,''); assert.equal(f.clicks(),0);
  }
});
test('Huadian multiple dropdowns cannot use the single-default exception', () => {
  const f=huadianFixture(); addArea(f).options=[{text:'一区',value:'a'}];
  assert.equal(f.run().state,'waiting'); assert.equal(f.clicks(),0);
});
test('Huadian real carrier options still select the requested carrier', () => {
  const f=huadianFixture(); f.cfg.provider='中国联通';
  f.select.options=[{text:'',value:''},{text:'移动用户',value:'m'},{text:'联通用户',value:'u'}];
  assert.equal(f.run().state,'submitted'); assert.equal(f.select.value,'u');
});
console.log('All ' + passed + ' navigation and portal regression tests passed.');
