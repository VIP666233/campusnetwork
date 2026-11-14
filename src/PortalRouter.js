// Build template. build-portal.ps1 embeds the three adapters into Portal.js.
(function () {
  'use strict';
  var configuration = __CONFIG__;
  function visible(el) { return el && !el.disabled && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden'; }
  function imageForm(doc) {
    var root = doc.querySelector('#logininfo_hk'), link = doc.querySelector('#loginLink'), graphic = doc.querySelector('#loginLink_div');
    var user = doc.querySelector('#username'), pass = doc.querySelector('#pwd');
    return visible(root) && link && graphic && user && pass && root.contains(user) && root.contains(pass) &&
      root.contains(link) && link.contains(graphic) && link.tagName === 'A' && graphic.tagName === 'DIV' &&
      user.tagName === 'INPUT' && pass.tagName === 'INPUT' && pass.type === 'password';
  }
  function detect() {
    // Recognition reads structure only. Each adapter still checks origin before filling.
    var inputs = Array.from(document.querySelectorAll('input'));
    var selects = Array.from(document.querySelectorAll('select')).filter(visible);
    var body = document.body ? String(document.body.innerText || document.body.textContent || '') : '';
    if (location.pathname === '/portal.do' && /华电|华北电力/.test(body) && selects.length === 1 &&
        inputs.some(function (el) { return visible(el) && /^(text|email|tel|number)$/.test(el.type); }) &&
        inputs.some(function (el) { return visible(el) && el.type === 'password'; })) return 'huadian';
    if (imageForm(document)) return 'enhanced';
    var embedded = false, visited = 0;
    function scan(doc, depth) {
      if (depth > 4 || visited > 24) return;
      Array.from(doc.querySelectorAll('iframe,frame')).forEach(function (frame) {
        if (!visible(frame) || ++visited > 24) return;
        try {
          var child = frame.contentDocument;
          if (!child || !child.defaultView || (child.defaultView.location.origin !== location.origin && !/^about:(srcdoc|blank)$/.test(child.URL))) return;
          if (imageForm(child) || Array.from(child.querySelectorAll('input')).some(function (el) { return visible(el) && el.type === 'password'; })) embedded = true;
          scan(child, depth + 1);
        } catch (_) { /* Never bypass cross-origin restrictions. */ }
      });
    }
    scan(document, 0);
    if (embedded) return 'enhanced';
    return 'classic';
  }
  var adapter = window.__campusUnifiedAdapter;
  if (!adapter) adapter = detect();
  var state;
  if (adapter === 'huadian') state = __HUADIAN_ADAPTER__;
  else if (adapter === 'enhanced') state = __ENHANCED_ADAPTER__;
  else state = __CLASSIC_ADAPTER__;
  // Stay with the submitted adapter while success UI hides/replaces its fields.
  // Never try another submission merely because a portal reports failure.
  if (state.state === 'submitted') window.__campusUnifiedAdapter = adapter;
  state.adapter = adapter;
  if ((state.state === 'blocked' || state.state === 'waiting') && /CSS|选择器|在高级设置填写|高级设置[“「]/.test(state.message))
    state.message = '暂时无法自动匹配此登录页面，请在高级设置点击“点击检测”并保存报告。';
  return state;
})()
