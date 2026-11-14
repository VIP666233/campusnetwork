(function () {
  'use strict';
  var pages = [], seen = [], truncated = false, total = 0;
  function visible(el) { return el.getClientRects().length > 0 && el.ownerDocument.defaultView.getComputedStyle(el).visibility !== 'hidden'; }
  function path(el) {
    var parts = [];
    for (var n = el; n && n.nodeType === 1; n = n.parentElement) {
      var siblings = n.parentElement ? Array.from(n.parentElement.children).filter(function(s) { return s.tagName === n.tagName; }) : [n];
      parts.unshift(n.tagName.toLowerCase() + (siblings.length > 1 ? ':nth-of-type(' + (siblings.indexOf(n) + 1) + ')' : ''));
    }
    return parts.join(' > ');
  }
  // Report only known interface words and spacing, not arbitrary page text.
  // Account/password input values, cookies, storage, URLs and HTML are never read.
  function label(value) {
    return String(value || '').slice(0, 180).replace(/连接网络|连接|登录|登|录|立即|上网|认证|校园网|校园|中国|电信|移动|联通|密码|账号|帐号|用户名|记住|保存|取消|退出|注销|网络|Login|login|LOGIN|Log|log|In|in|Sign|sign|Connect|connect|Submit|submit|button|[​-‍\s\/|（）()：:._-]+|[^\s]/g,
      function (part) { return /^(?:连接网络|连接|登录|登|录|立即|上网|认证|校园网|校园|中国|电信|移动|联通|密码|账号|帐号|用户名|记住|保存|取消|退出|注销|网络|Login|login|LOGIN|Log|log|In|in|Sign|sign|Connect|connect|Submit|submit|button|[​-‍\s\/|（）()：:._-]+)$/.test(part) ? part : '■'; }).replace(/■+/g, '[其他]');
  }
  function identifier(value) { return String(value || '').slice(0, 140).replace(/[0-9]{4,}|[a-f0-9]{24,}/ig, '[编号]'); }
  function scan(doc, where, depth, containingVisible) {
    if (!doc || seen.indexOf(doc) >= 0) return;
    if (depth > 4 || pages.length >= 20 || total >= 800) { truncated = true; return; }
    seen.push(doc);
    var owner = doc.ownerDocument || doc, view = owner.defaultView, records = [], page = { frame: where, ready: owner.readyState, visible: containingVisible, records: records, frames: [] };
    pages.push(page);
    var elements = Array.from(doc.querySelectorAll('*'));
    if (elements.length > 6000) truncated = true;
    elements = elements.slice(0, 6000);
    elements.forEach(function (el) {
      if (records.length >= 220 || total >= 800) { truncated = true; return; }
      if (/^(SCRIPT|STYLE|META|LINK|HEAD|HTML|BODY|NOSCRIPT)$/.test(el.tagName)) return;
      var style = view.getComputedStyle(el);
      var native = /^(INPUT|SELECT|BUTTON|A|TEXTAREA|IFRAME|FRAME|CANVAS|IMG)$/.test(el.tagName);
      var interactive = el.hasAttribute('onclick') || typeof el.onclick === 'function' || el.hasAttribute('role') || style.cursor === 'pointer';
      var isInput = /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName);
      var raw = isInput ? '' : String(el.innerText || el.textContent || '');
      var loginWord = !isInput && raw.length < 100 && /连接|登录|login|connect|校园网|移动|联通|电信/i.test(raw);
      var structural = /login|submit|connect|service|operator|dropdown|select|menu/i.test(String(el.id || '') + ' ' + String(el.className || ''));
      if (!native && !interactive && !loginWord && !structural) return;
      if (records.length >= 220 || total >= 800) { truncated = true; return; }
      var r = {
        selector: path(el), tag: el.tagName, type: el.type || '', id: identifier(el.id), name: identifier(el.name),
        classes: identifier(typeof el.className === 'string' ? el.className : ''), visible: containingVisible && visible(el), disabled: !!el.disabled, readOnly: !!el.readOnly,
        role: identifier(el.getAttribute('role')), ariaDisabled: el.getAttribute('aria-disabled') === 'true',
        onclick: el.hasAttribute('onclick') || typeof el.onclick === 'function', cursor: style.cursor,
        backgroundImage: style.backgroundImage !== 'none', children: Array.from(el.children).slice(0, 12).map(function(c) { return c.tagName; })
      };
      if (!isInput) r.label = label(raw);
      // Button value is its caption; credential and other input values are not inspected.
      if (el.tagName === 'INPUT' && /^(button|submit|reset)$/.test(el.type)) r.buttonCaption = label(el.value);
      r.title = label(el.getAttribute('title')); r.alt = label(el.getAttribute('alt')); r.ariaLabel = label(el.getAttribute('aria-label'));
      r.placeholder = label(el.getAttribute('placeholder'));
      r.display = style.display;
      r.tipField = /_tip$/i.test(el.id || '');
      if (r.tipField) {
        var real = doc.querySelector('#' + view.CSS.escape(el.id.slice(0, -4)));
        if (real && real.tagName === 'INPUT') r.pairedInput = path(real);
      }
      r.ancestors = [];
      for (var p = el.parentElement, level = 0; p && level < 3; p = p.parentElement, level++)
        r.ancestors.push({ tag: p.tagName, id: identifier(p.id), classes: identifier(typeof p.className === 'string' ? p.className : '') });
      r.before = label(view.getComputedStyle(el, '::before').content); r.after = label(view.getComputedStyle(el, '::after').content);
      if (el.tagName === 'SELECT') r.options = Array.from(el.options).slice(0, 20).map(function(o) { return label(o.label || o.text); });
      records.push(r); total++;
    });
    Array.from(doc.querySelectorAll('iframe,frame')).forEach(function(frame, i) {
      var item = { index: i, selector: path(frame), visible: containingVisible && visible(frame), accessible: false };
      page.frames.push(item);
      try {
        var child = frame.contentDocument;
        if (child && child.defaultView) { item.accessible = true; scan(child, where + '/frame' + i, depth + 1, item.visible); }
      } catch (_) { }
    });
    // Open shadow roots may contain controls that document.querySelectorAll omits.
    var shadows = elements.filter(function(el) { return !!el.shadowRoot; });
    page.openShadowRoots = shadows.map(path).slice(0, 20);
    shadows.slice(0,20).forEach(function(el,i) { scan(el.shadowRoot, where + '/shadow' + i, depth + 1, containingVisible && visible(el)); });
  }
  scan(document, 'main', 0, true);
  return { diagnosticVersion: '2.0', pages: pages, truncated: truncated };
})()
