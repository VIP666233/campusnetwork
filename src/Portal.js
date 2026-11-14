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
  if (adapter === 'huadian') state = (function () {
  'use strict';
  var cfg = configuration;
  function visible(el) { return el && !el.disabled && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden'; }
  function text(el) { return String(el.innerText || el.textContent || el.value || el.alt || el.title || '').trim(); }
  function norm(s) { return String(s).replace(/[\s\u200b-\u200d\ufeff]+/g, '').toLowerCase(); }
  function optionText(o) { return String(o.label || o.text || o.textContent || '').trim(); }
  function result(state, message) {
    var value = { state: state, message: message };
    // Structural diagnostics only: never include input values, option values,
    // arbitrary page text, URL parameters or account information in this report.
    if (sameServer && username && cfg.phase !== 'verify' && (state === 'waiting' || state === 'blocked')) {
      try {
        var controls = Array.from(document.querySelectorAll('select'));
        value.diagnostic = '下拉框识别[华电 v1.0.0]：' + (controls.length ? controls.slice(0, 8).map(function (el, index) {
          var opts = Array.from(el.options);
          var labels = opts.slice(0, 16).map(function (o) {
            var s = norm(optionText(o));
            if (!s) return '空白';
            var known = Object.keys(aliases || {}).filter(function (key) {
              return aliases[key].some(function (name) { return s.indexOf(norm(name)) >= 0; });
            });
            return (o.disabled ? '禁用:' : '') + (known.length ? known.join('/') : '其他文字');
          });
          return '#' + (index + 1) + '(' + (el.disabled ? '禁用' : visible(el) ? '可见' : '隐藏') + ',选项=' + opts.length + ',当前=' + el.selectedIndex + ')[' + labels.join('、') + ']';
        }).join('；') : '没有原生下拉框') + '；内嵌页面=' + document.querySelectorAll('iframe,frame').length;
      } catch (_) { /* Diagnostics must not change the login result. */ }
    }
    return value;
  }
  function query(selector) {
    try { return document.querySelector(selector); }
    catch (_) { throw new Error('高级设置中的 CSS 选择器格式不正确。'); }
  }
  function change(el, value) {
    var setter = Object.getOwnPropertyDescriptor(el.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype, 'value');
    if (setter && setter.set) setter.set.call(el, value); else el.value = value;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new Event('blur', { bubbles: true }));
  }
  function chooseOption(el, option) {
    // A repeated change can clear an asynchronously loaded dependent selector.
    // Reuse the actual option node, and identify it by index (values may repeat).
    if (el.options[el.selectedIndex] === option && el.__campusHelperSelectedOption === option) return;
    var index = Array.from(el.options).indexOf(option);
    if (index < 0) return;
    var setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'selectedIndex');
    if (setter && setter.set) setter.set.call(el, index); else el.selectedIndex = index;
    el.__campusHelperSelectedOption = option;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new Event('blur', { bubbles: true }));
  }
  function success() {
    var body = document.body ? text(document.body) : '';
    var logged = /登录成功|认证成功|您已登录|已经登录|在线时长|上网时长|累计在线|login\s+success|successfully\s+logged/i.test(body);
    var logout = Array.from(document.querySelectorAll('input,button,a')).some(function (el) {
      return visible(el) && /^(注销|退出登录|断开连接|下线|logout|log out)$/i.test(text(el));
    });
    var pass = Array.from(document.querySelectorAll('input[type=password]')).some(visible);
    return (logged || logout) && !pass;
  }
  function failures() {
    var body = document.body ? text(document.body) : '';
    return body.split(/\r?\n/).filter(function (line) {
      // Static help text is not the result of this login attempt.
      return !/用户办理|扫码|二维码|缴费或|(?:如果|若|如遇).*(?:失败|错误)/.test(line) &&
        /密码错误|账号或密码错误|用户名或密码错误|认证失败|登录失败|余额不足|账号欠费|用户不存在|账号不存在|用户已在线|设备数超限|password\s+incorrect|invalid\s+(?:password|credentials)/i.test(line);
    }).map(function (line) { return line.trim(); });
  }
  function fail() {
    var previous = window.__campusHelperErrorsBeforeSubmit || [];
    var error = failures().find(function (line) { return previous.indexOf(line) < 0; });
    return error ? error.slice(0, 100) : null;
  }
  try {
    // Chromium briefly exposes about:blank before a navigation completes.
    // Its internal error page is a connection failure, not a portal redirect.
    if (/^(chrome-error|edge-error):$/.test(location.protocol)) return result('failed', '无法打开认证页。请先连接校园 Wi-Fi 或有线网络，确认浏览器能访问认证地址后重试。');
    if (location.protocol === 'about:') return result('waiting', '等待认证页加载…');
    if (!/^https?:$/.test(location.protocol)) return result('blocked', '当前页面不是 HTTP/HTTPS 认证页，请检查认证地址。');
    if (!document.body || document.readyState === 'loading') return result('waiting', '等待认证页加载…');
    // A portal may switch ports or upgrade HTTP to HTTPS on the same host.
    // For a different server, only return the discovered URL; never fill credentials.
    var configured = new URL(cfg.origin);
    var sameServer = location.hostname === configured.hostname && !(configured.protocol === 'https:' && location.protocol === 'http:');
    if (!sameServer) {
      // Wait through intermediate redirects instead of stopping at the first document.
      if (document.readyState !== 'complete') return result('waiting', '等待认证页跳转完成…');
      var redirected = result('redirected', '检测到另一台认证服务器：' + location.origin + location.pathname);
      redirected.redirectUrl = location.href;
      return redirected;
    }
    if (success()) return result('success', '认证页显示已登录。');
    if (cfg.phase === 'verify') {
      var error = fail();
      return error ? result('failed', '认证页提示：' + error) : result('waiting', '等待认证结果…');
    }
    if (window.__campusHelperSubmitted) return result('submitted', '登录请求已提交，等待页面确认…');
    var inputs = Array.from(document.querySelectorAll('input'));
    var password = cfg.passwordSelector ? query(cfg.passwordSelector) : inputs.find(function (el) { return visible(el) && el.type === 'password'; });
    var username = cfg.usernameSelector ? query(cfg.usernameSelector) : inputs.find(function (el) {
      return visible(el) && el.type !== 'password' && /^(text|email|tel|number)$/.test(el.type) && /^(DDDDD|username|user_name|user_account|account|user|loginName|loginname|userid|user_id|loginid)$/i.test(el.name || el.id);
    });
    if (!username && !cfg.usernameSelector) username = inputs.find(function (el) { return visible(el) && /^(text|email|tel)$/.test(el.type); });
    if (!visible(username) || !visible(password)) return result('waiting', '暂未找到账号和密码输入框。');
    if (username.tagName !== 'INPUT' || password.tagName !== 'INPUT') return result('blocked', '账号或密码选择器必须指向 input 元素。');
    // Account-dependent portals populate services on input/change/blur. Do this
    // before inspecting services, and never reset them on each polling pass.
    if (username.value !== cfg.username + cfg.accountSuffix) change(username, cfg.username + cfg.accountSuffix);
    if (!document.documentElement.contains(username) || !document.documentElement.contains(password)) return result('waiting', '等待账号对应的登录表单加载…');
    var aliases = {
      '校园网': ['校园网', '校园', '教育网', '本校', 'campus', 'cernet'],
      '中国移动': ['中国移动', '移动', 'cmcc', 'china mobile', '铁通'],
      '中国联通': ['中国联通', '联通', 'unicom'],
      '中国电信': ['中国电信', '电信', 'telecom', 'chinanet', '天翼']
    };
    var names = aliases[cfg.provider] || [cfg.provider];
    function matches(s) { return names.some(function (n) { return norm(s).indexOf(norm(n)) >= 0; }); }
    function anyCarrier(s) { return Object.keys(aliases).some(function (key) { return aliases[key].some(function (n) { return norm(s).indexOf(norm(n)) >= 0; }); }); }
    function available(o) { return !o.disabled && !(o.parentElement && o.parentElement.disabled); }
    function requested(o) { return available(o) && (o.value === cfg.providerValue || norm(optionText(o)) === norm(cfg.providerValue)); }
    function meaningful(o) { return available(o) && !/^(|[-—]+|请选择.*|选择.*|select.*|please.*)$/i.test(norm(optionText(o))); }
    function huadianDefaultArea(el) {
      // Confirmed by the school's user: its single blank option is the valid
      // default. Preserve the server-provided value; never invent a realm/ISP.
      // Keep this exception confined to this school and this exact form shape.
      return (location.hostname === '10.99.0.16' || /华电|华北电力/.test(document.body ? text(document.body) : '')) && location.pathname === '/portal.do' &&
        !cfg.providerSelector && !cfg.providerValue && selects.length === 1 &&
        el.options.length === 1 && el.selectedIndex === 0 && available(el.options[0]) &&
        norm(optionText(el.options[0])) === '';
    }
    function describe(el) {
      return Array.from(el.options).filter(meaningful).slice(0, 8).map(function (o) { return optionText(o).slice(0, 30); }).join('、');
    }
    var selects = Array.from(document.querySelectorAll('select')).filter(visible);
    function isArea(el) {
      var labels = el.labels ? Array.from(el.labels).map(text).join(' ') : '';
      var hint = String(el.id || '') + ' ' + String(el.name || '') + ' ' + labels + ' ' + (el.parentElement ? text(el.parentElement).slice(0, 120) : '');
      return selects.length === 1 || /区域|校区|服务|运营商|domain|realm|service|area|region|zone|isp|operator|carrier/i.test(hint);
    }
    // Some schools fetch the option list only when the real dropdown is opened.
    // Activate it once, then poll; never guess a carrier or skip an empty field.
    function waitForOptions(pending) {
      var activation = window.__campusHelperSelectActivation;
      if (!activation) activation = window.__campusHelperSelectActivation = { started: Date.now(), elements: [] };
      if (Date.now() - activation.started >= 12000) return result('blocked', '已填写账号并展开区域/运营商下拉框，但仍未读到可选项。请检查认证页是否正常加载；若手动展开能看到选项，请提供展开后的截图。');
      if (activation.elements.indexOf(pending) < 0) {
        activation.elements.push(pending);
        pending.scrollIntoView({ block: 'center', inline: 'nearest' });
        var rect = pending.getBoundingClientRect();
        var activate = result('waiting', '正在展开区域/运营商下拉框，读取可选项…');
        activate.activateSelect = { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
        return activate;
      }
      return result('waiting', '已展开区域/运营商下拉框，等待选项加载…');
    }
    var pending = selects.find(function (el) {
      return (isArea(el) || (cfg.providerSelector && el === query(cfg.providerSelector))) &&
        !huadianDefaultArea(el) &&
        !Array.from(el.options).some(function (o) { return meaningful(o) || (cfg.providerValue && requested(o)); });
    });
    var chosen = false;
    var select;
    if (cfg.providerSelector) select = query(cfg.providerSelector);
    else {
      var candidates = selects.filter(function (el) { return Array.from(el.options).some(function (o) { return cfg.providerValue ? requested(o) : meaningful(o) && matches(optionText(o)); }); });
      if (candidates.length > 1) return result('blocked', '找到多个运营商/区域下拉框，请在高级设置填写对应控件的 CSS 选择器。');
      select = candidates[0];
    }
    if (select && select.tagName === 'SELECT' && visible(select)) {
      var options = Array.from(select.options).filter(function (o) { return cfg.providerValue ? requested(o) : meaningful(o) && matches(optionText(o)); });
      if (options.length > 1) return result('blocked', '有多个匹配选项（' + describe(select) + '），请在高级设置填写准确的选项值或文字。');
      var option = options[0];
      if (!option && !Array.from(select.options).some(meaningful)) return waitForOptions(select);
      if (!option) return result('blocked', '运营商/区域没有对应选项（' + describe(select) + '），请检查选择或填写准确的选项值/文字。');
      chooseOption(select, option); chosen = true;
    } else if (cfg.providerSelector) {
      if (!visible(select)) return result('waiting', '暂未找到运营商控件。');
      if (!select.__campusHelperClicked) { select.click(); select.__campusHelperClicked = true; }
      chosen = true;
    } else {
      var radio = Array.from(document.querySelectorAll('input[type=radio]')).find(function (el) {
        var label = el.closest('label');
        if (!label && el.id) label = Array.from(document.querySelectorAll('label')).find(function (l) { return l.htmlFor === el.id; });
        return !el.disabled && ((label && visible(label) && matches(text(label))) || (visible(el) && matches(el.value)));
      });
      if (radio) {
        if (!radio.checked || !radio.__campusHelperClicked) { radio.click(); radio.dispatchEvent(new Event('change', { bubbles: true })); radio.__campusHelperClicked = true; }
        chosen = true;
      }
    }
    // Choose a loaded carrier first: its change handler may be responsible for
    // loading a second, initially empty service/area dropdown.
    var usingHuadianDefault = !chosen && selects.length === 1 && huadianDefaultArea(selects[0]);
    if (usingHuadianDefault) chosen = true;
    if (!chosen && pending) return waitForOptions(pending);
    if (!chosen && cfg.providerValue) return result('blocked', '没有找到所填写的运营商/区域选项，请检查选项文字或值，必要时填写 CSS 选择器。');
    if (!chosen && !cfg.accountSuffix) {
      var carrierPresent = selects.some(function (el) { return Array.from(el.options).some(function (o) { return anyCarrier(optionText(o)); }); }) ||
        Array.from(document.querySelectorAll('input[type=radio]')).some(function (el) {
          var label = el.closest('label');
          if (!label && el.id) label = Array.from(document.querySelectorAll('label')).find(function (l) { return l.htmlFor === el.id; });
          return (visible(el) && anyCarrier(el.value)) || (label && visible(label) && anyCarrier(text(label)));
        });
      if (cfg.provider !== '校园网' || carrierPresent) return result('blocked', '未找到所选运营商。没有运营商选项的学校请选择“校园网”；有区域选项时可在高级设置填写选项文字或值。');
      // Campus-only portals need no carrier selection. Keep the school's valid default.
      chosen = true;
    }
    // An area/realm is independent of an ISP. Never guess between multiple areas.
    for (var si = 0; si < selects.length; si++) {
      var area = selects[si];
      if (!document.documentElement.contains(area)) return result('waiting', '等待运营商/区域表单切换完成…');
      if (!visible(area)) continue;
      if (huadianDefaultArea(area)) continue;
      if (area === select) continue;
      if (!isArea(area)) continue;
      var usable = Array.from(area.options).filter(meaningful);
      if (usable.length === 0) return waitForOptions(area);
      var selected = area.selectedIndex >= 0 ? area.options[area.selectedIndex] : null;
      if (selected && meaningful(selected)) continue;
      if (usable.length === 1) chooseOption(area, usable[0]);
      else return result('blocked', '请选择区域/服务：' + describe(area) + '。在高级设置“区域/选项值”填写准确的选项文字或值。');
    }
    // Synchronous change handlers can clear or replace earlier controls. Do not
    // submit until the next poll has inspected the new, stable form.
    for (var ci = 0; ci < selects.length; ci++) {
      var currentSelect = selects[ci];
      if (!document.documentElement.contains(currentSelect)) return result('waiting', '等待运营商/区域表单切换完成…');
      if (!visible(currentSelect) || (currentSelect !== select && !isArea(currentSelect))) continue;
      if (huadianDefaultArea(currentSelect)) continue;
      var currentOption = currentSelect.options[currentSelect.selectedIndex];
      if (currentSelect === select && currentOption && currentOption !== option) return result('waiting', '运营商选项被页面更新，正在重新确认选择…');
      if (!currentOption || !(meaningful(currentOption) || (currentSelect === select && cfg.providerValue && requested(currentOption)))) {
        if (!Array.from(currentSelect.options).some(meaningful)) return waitForOptions(currentSelect);
        return result('waiting', '区域选项已更新，正在重新确认选择…');
      }
    }
    // Some portals replace account inputs when the operator changes.
    if (!document.documentElement.contains(username) || !document.documentElement.contains(password)) {
      inputs = Array.from(document.querySelectorAll('input'));
      password = cfg.passwordSelector ? query(cfg.passwordSelector) : inputs.find(function (el) { return visible(el) && el.type === 'password'; });
      username = cfg.usernameSelector ? query(cfg.usernameSelector) : inputs.find(function (el) { return visible(el) && /^(text|email|tel)$/.test(el.type); });
      if (!visible(username) || !visible(password)) return result('waiting', '等待运营商账号表单加载…');
    }
    var buttons = Array.from(document.querySelectorAll('button,input[type=submit],input[type=button],input[type=image],a'));
    var button = cfg.loginSelector ? query(cfg.loginSelector) : buttons.find(function (el) { return visible(el) && /^(登\s*录|登录上网|连接|立即登录|login|log in|sign in)$/i.test(text(el)); });
    if (!visible(button)) return result('waiting', '暂未找到登录按钮。');
    if (window.__campusHelperSubmitted) return result('submitted', '登录请求已提交，等待页面确认…');
    if (username.value !== cfg.username + cfg.accountSuffix) {
      change(username, cfg.username + cfg.accountSuffix);
      return result('waiting', '账号表单已更新，正在重新确认区域选项…');
    }
    change(password, cfg.password);
    window.__campusHelperErrorsBeforeSubmit = failures();
    window.__campusHelperSubmitted = true;
    button.click();
    return result('submitted', usingHuadianDefault ? '华电认证页仅有一个空白区域，已保留网页默认值，填写账号密码并提交登录，等待认证结果…' : '登录请求已提交，等待页面确认…');
  } catch (error) { return result('blocked', error.message || '操作认证表单失败。'); }
})();
  else if (adapter === 'enhanced') state = (function (window, document, location) {
  'use strict';
  var cfg = configuration;
  var rootWindow = window, rootDocument = document;
  function hint(el) {
    return [el.name, el.id, el.placeholder, el.autocomplete, el.getAttribute ? el.getAttribute('aria-label') : ''].join(' ');
  }
  function passwordLike(el) {
    return visible(el) && (el.type === 'password' ||
      (/^(text|email|tel)$/.test(el.type) && /(?:^|[\s_-])(?:upass|password|passwd|pass|pwd|userpassword|userpwd|loginpwd)(?:$|[\s_-])|密码/i.test(hint(el))));
  }
  function fields(doc) {
    function configuredQuery(selector) {
      try { return doc.querySelector(selector); }
      catch (_) { throw new Error('高级设置中的 CSS 选择器格式不正确。'); }
    }
    var all = Array.from(doc.querySelectorAll('input'));
    var pw = cfg.passwordSelector ? configuredQuery(cfg.passwordSelector) :
      all.find(function (el) { return visible(el) && el.type === 'password'; }) || all.find(passwordLike);
    var candidates = all.filter(function (el) {
      return visible(el) && el !== pw && !passwordLike(el) && /^(text|email|tel|number)$/.test(el.type) &&
        (!pw || !pw.form || el.form === pw.form);
    });
    var user = cfg.usernameSelector ? configuredQuery(cfg.usernameSelector) : candidates.find(function (el) {
      return /(?:^|[\s_-])(?:DDDDD|username|user_name|user_account|account|user|loginname|userid|user_id|loginid)(?:$|[\s_-])|账号|帐号|用户名|学号/i.test(hint(el));
    });
    if (!user && !cfg.usernameSelector) user = candidates[0];
    return { username: user, password: pw };
  }
  function selectDocument() {
    // Only inspect visible, same-origin frames. Never send credentials into an
    // unrelated embedded origin or guess between multiple embedded login forms.
    if (cfg.phase === 'verify' && rootWindow.__campusHelperLoginDocument) {
      var old = rootWindow.__campusHelperLoginDocument;
      try { if (old.defaultView && old.defaultView.document === old && old.defaultView.frameElement && old.defaultView.frameElement.isConnected) return old; } catch (_) { }
    }
    var main = fields(rootDocument);
    if (visible(main.username) && visible(main.password)) return rootDocument;
    var matches = [], visited = 0;
    function scan(doc, depth) {
      if (depth > 4 || visited > 24) return;
      Array.from(doc.querySelectorAll('iframe,frame')).forEach(function (frame) {
        if (!visible(frame) || ++visited > 24) return;
        try {
          var child = frame.contentDocument;
          if (!child || !child.defaultView || (child.defaultView.location.origin !== location.origin && !/^about:(srcdoc|blank)$/.test(child.URL))) return;
          var pair = fields(child);
          if (visible(pair.username) && visible(pair.password)) matches.push(child);
          scan(child, depth + 1);
        } catch (_) { /* Inaccessible embedded origins are intentionally excluded. */ }
      });
    }
    scan(rootDocument, 0);
    if (matches.length > 1) throw new Error('发现多个内嵌登录表单，请在高级设置填写账号、密码选择器以明确指定。');
    return matches[0] || rootDocument;
  }
  function visible(el) { return el && !el.disabled && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden'; }
  function text(el) { return String(el.innerText || el.textContent || el.value || el.alt || el.title || '').trim(); }
  function norm(s) { return String(s).replace(/[\s\u200b-\u200d\ufeff]+/g, '').toLowerCase(); }
  function attr(el, name) { return el && el.getAttribute ? el.getAttribute(name) || '' : ''; }
  function actionable(el) {
    if (!visible(el)) return false;
    for (var p = el; p && p !== document.body; p = p.parentElement) {
      if (p.disabled || attr(p, 'aria-disabled') === 'true' || p.inert) return false;
    }
    return true;
  }
  function outermost(nodes) {
    return nodes.filter(function (el) { return !nodes.some(function (other) { return other !== el && other.contains && other.contains(el); }); });
  }
  function controlPoint(el) {
    var view = window;
    while (view !== rootWindow && view.frameElement) { view.frameElement.scrollIntoView({ block: 'center', inline: 'nearest' }); view = view.parent; }
    el.scrollIntoView({ block: 'center', inline: 'nearest' });
    var rect = el.getBoundingClientRect();
    var x = (Math.max(0, rect.left) + Math.min(window.innerWidth, rect.right)) / 2;
    var y = (Math.max(0, rect.top) + Math.min(window.innerHeight, rect.bottom)) / 2;
    var hit = document.elementFromPoint(x, y);
    if (!hit || (hit !== el && !el.contains(hit))) return null;
    view = window;
    while (view !== rootWindow && view.frameElement) {
      var frame = view.frameElement, bounds = frame.getBoundingClientRect();
      x = bounds.left + (frame.clientLeft + x) * (frame.offsetWidth ? bounds.width / frame.offsetWidth : 1);
      y = bounds.top + (frame.clientTop + y) * (frame.offsetHeight ? bounds.height / frame.offsetHeight : 1);
      view = view.parent;
      if (view.document.elementFromPoint(x, y) !== frame) return null;
    }
    return { x: x, y: y };
  }
  function clickCustom(el, message) {
    var point = controlPoint(el);
    if (!point) return result('waiting', '自定义控件暂时被遮挡，等待页面就绪…');
    var action = result('waiting', message);
    action.clickControl = point;
    return action;
  }
  function loginLabel(el) {
    var labels = [text(el), attr(el, 'aria-label'), el.alt, el.title];
    return labels.some(function (label) {
      return /^(登录|登录上网|连接|立即登录|login|signin|连接login|登录login|login连接|login登录)$/.test(norm(label || '').replace(/[\/／|｜()（）]/g, ''));
    });
  }
  function loginButtons() {
    var native = Array.from(document.querySelectorAll('button,input[type=submit],input[type=button],input[type=image],a')).filter(function (el) { return actionable(el) && loginLabel(el); });
    var custom = Array.from(document.querySelectorAll('[role=button],div,span,label,p,img')).filter(function (el) {
      if (!actionable(el) || !loginLabel(el)) return false;
      // Custom buttons can have listeners registered by addEventListener. Require
      // an interaction cue rather than treating headings/instructions as buttons.
      for (var p = el, depth = 0; p && depth < 4 && loginLabel(p); p = p.parentElement, depth++) {
        if (attr(p, 'role') === 'button' || typeof p.onclick === 'function' || p.tabIndex >= 0 ||
            /login|connect|submit|denglu|btn|button/i.test(String(p.id || '') + ' ' + String(p.className || '')) ||
            getComputedStyle(p).cursor === 'pointer') return true;
      }
      return false;
    }).map(function (el) {
      while (el.parentElement && /^(DIV|SPAN|A|BUTTON)$/.test(el.parentElement.tagName) && actionable(el.parentElement) && loginLabel(el.parentElement)) el = el.parentElement;
      return el;
    });
    return outermost(native.concat(custom).filter(function (el, index, all) { return all.indexOf(el) === index; }));
  }
  function optionText(o) { return String(o.label || o.text || o.textContent || '').trim(); }
  function imageLoginPortal() {
    // Fingerprint confirmed by both diagnostic reports: an empty anchor wraps
    // the login background image; tip inputs are separate from real fields.
    var root = query('#logininfo_hk'), link = query('#loginLink'), graphic = query('#loginLink_div');
    var user = query('#username'), pass = query('#pwd');
    if (!root || !link || !graphic || !user || !pass || link.tagName !== 'A' || graphic.tagName !== 'DIV' ||
        user.tagName !== 'INPUT' || pass.tagName !== 'INPUT' || pass.type !== 'password' ||
        !root.contains(link) || !link.contains(graphic) || !root.contains(user) || !root.contains(pass)) return null;
    return { root: root, button: graphic, username: user, password: pass };
  }
  function result(state, message) {
    var value = { state: state, message: message };
    // Structural diagnostics only: never include input values, option values,
    // arbitrary page text, URL parameters or account information in this report.
    if (sameServer && username && cfg.phase !== 'verify' && (state === 'waiting' || state === 'blocked')) {
      try {
        var controls = Array.from(document.querySelectorAll('select'));
        value.diagnostic = '下拉框识别[v1.9.8]：' + (controls.length ? controls.slice(0, 8).map(function (el, index) {
          var opts = Array.from(el.options);
          var labels = opts.slice(0, 16).map(function (o) {
            var s = norm(optionText(o));
            if (!s) return '空白';
            var known = Object.keys(aliases || {}).filter(function (key) {
              return aliases[key].some(function (name) { return s.indexOf(norm(name)) >= 0; });
            });
            return (o.disabled ? '禁用:' : '') + (known.length ? known.join('/') : '其他文字');
          });
          return '#' + (index + 1) + '(' + (el.disabled ? '禁用' : visible(el) ? '可见' : '隐藏') + ',选项=' + opts.length + ',当前=' + el.selectedIndex + ')[' + labels.join('、') + ']';
        }).join('；') : '没有原生下拉框') + '；内嵌页面=' + document.querySelectorAll('iframe,frame').length;
      } catch (_) { /* Diagnostics must not change the login result. */ }
    }
    return value;
  }
  function query(selector) {
    try { return document.querySelector(selector); }
    catch (_) { throw new Error('高级设置中的 CSS 选择器格式不正确。'); }
  }
  function change(el, value) {
    var realm = el.ownerDocument ? el.ownerDocument.defaultView : null;
    var setter = Object.getOwnPropertyDescriptor(el.tagName === 'SELECT' ? (realm ? realm.HTMLSelectElement : HTMLSelectElement).prototype : (realm ? realm.HTMLInputElement : HTMLInputElement).prototype, 'value');
    if (setter && setter.set) setter.set.call(el, value); else el.value = value;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new Event('blur', { bubbles: true }));
  }
  function chooseOption(el, option) {
    // A repeated change can clear an asynchronously loaded dependent selector.
    // Reuse the actual option node, and identify it by index (values may repeat).
    if (el.options[el.selectedIndex] === option && el.__campusHelperSelectedOption === option) return;
    var index = Array.from(el.options).indexOf(option);
    if (index < 0) return;
    var setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'selectedIndex');
    if (setter && setter.set) setter.set.call(el, index); else el.selectedIndex = index;
    el.__campusHelperSelectedOption = option;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new Event('blur', { bubbles: true }));
  }
  function success() {
    var body = document.body ? text(document.body) : '';
    var logged = /登录成功|认证成功|您已登录|已经登录|在线时长|上网时长|累计在线|login\s+success|successfully\s+logged/i.test(body);
    var logout = Array.from(document.querySelectorAll('input,button,a')).some(function (el) {
      return visible(el) && /^(注销|退出登录|断开连接|下线|logout|log out)$/i.test(text(el));
    });
    var pass = Array.from(document.querySelectorAll('input')).some(passwordLike);
    return (logged || logout) && !pass;
  }
  function failures() {
    var body = document.body ? text(document.body) : '';
    return body.split(/\r?\n/).filter(function (line) {
      // Static help text is not the result of this login attempt.
      return !/用户办理|扫码|二维码|缴费或|(?:如果|若|如遇).*(?:失败|错误)/.test(line) &&
        /密码错误|账号或密码错误|用户名或密码错误|认证失败|登录失败|余额不足|账号欠费|用户不存在|账号不存在|用户已在线|设备数超限|password\s+incorrect|invalid\s+(?:password|credentials)/i.test(line);
    }).map(function (line) { return line.trim(); });
  }
  function fail() {
    var previous = window.__campusHelperErrorsBeforeSubmit || [];
    var error = failures().find(function (line) { return previous.indexOf(line) < 0; });
    return error ? error.slice(0, 100) : null;
  }
  try {
    // Chromium briefly exposes about:blank before a navigation completes.
    // Its internal error page is a connection failure, not a portal redirect.
    if (/^(chrome-error|edge-error):$/.test(location.protocol)) return result('failed', '无法打开认证页。请先连接校园 Wi-Fi 或有线网络，确认浏览器能访问认证地址后重试。');
    if (location.protocol === 'about:') return result('waiting', '等待认证页加载…');
    if (!/^https?:$/.test(location.protocol)) return result('blocked', '当前页面不是 HTTP/HTTPS 认证页，请检查认证地址。');
    if (!document.body || document.readyState === 'loading') return result('waiting', '等待认证页加载…');
    // A portal may switch ports or upgrade HTTP to HTTPS on the same host.
    // For a different server, only return the discovered URL; never fill credentials.
    var configured = new URL(cfg.origin);
    var sameServer = location.hostname === configured.hostname && !(configured.protocol === 'https:' && location.protocol === 'http:');
    if (!sameServer) {
      // Wait through intermediate redirects instead of stopping at the first document.
      if (document.readyState !== 'complete') return result('waiting', '等待认证页跳转完成…');
      var redirected = result('redirected', '检测到另一台认证服务器：' + location.origin + location.pathname);
      redirected.redirectUrl = location.href;
      return redirected;
    }
    document = selectDocument();
    if (document !== rootDocument) window = document.defaultView;
    if (success()) return result('success', '认证页显示已登录。');
    if (cfg.phase === 'verify') {
      var error = fail();
      return error ? result('failed', '认证页提示：' + error) : result('waiting', '等待认证结果…');
    }
    if (window.__campusHelperSubmitted) return result('submitted', '登录请求已提交，等待页面确认…');
    var imagePortal = imageLoginPortal();
    if (imagePortal && !cfg.usernameSelector && !cfg.passwordSelector) {
      if (!visible(imagePortal.username)) {
        var userTip = query('#username_tip');
        if (visible(userTip)) userTip.focus();
        if (!visible(imagePortal.username)) return result('waiting', '正在激活账号输入框…');
      }
      // Keep the real account nonempty before blurring: this template restores
      // its placeholder when an empty real input loses focus.
      if (imagePortal.username.value !== cfg.username + cfg.accountSuffix) change(imagePortal.username, cfg.username + cfg.accountSuffix);
      if (!visible(imagePortal.password)) {
        var passTip = query('#pwd_tip');
        if (visible(passTip)) passTip.focus();
        if (!visible(imagePortal.password)) return result('waiting', '正在激活密码输入框…');
      }
    }
    var pair = fields(document);
    if (imagePortal && !cfg.usernameSelector && !cfg.passwordSelector) pair = { username: imagePortal.username, password: imagePortal.password };
    var password = pair.password, username = pair.username;
    if (!visible(username) || !visible(password)) {
      var missing = result('waiting', '暂未找到账号和密码输入框。');
      var inspected = Array.from(document.querySelectorAll('input'));
      missing.diagnostic = '输入框识别[v1.9.8]：输入框=' + inspected.length + '，可见=' + inspected.filter(visible).length +
        '，密码候选=' + inspected.filter(passwordLike).length + '，内嵌页面=' + rootDocument.querySelectorAll('iframe,frame').length;
      return missing;
    }
    if (username === password) return result('blocked', '账号和密码不能指向同一个输入框，请检查高级设置。');
    if (username.tagName !== 'INPUT' || password.tagName !== 'INPUT') return result('blocked', '账号或密码选择器必须指向 input 元素。');
    // Legacy pages may display a text placeholder, revealing the real password
    // input only on focus. Re-read the form before writing any secret.
    if (password.type !== 'password' && password.focus && !password.__campusHelperFocused) {
      password.__campusHelperFocused = true;
      password.focus();
      pair = fields(document);
      if (pair.password !== password || pair.username !== username || !visible(password)) return result('waiting', '密码输入框已激活，正在重新识别登录表单…');
    }
    // Account-dependent portals populate services on input/change/blur. Do this
    // before inspecting services, and never reset them on each polling pass.
    if (username.value !== cfg.username + cfg.accountSuffix) change(username, cfg.username + cfg.accountSuffix);
    if (!document.documentElement.contains(username) || !document.documentElement.contains(password)) return result('waiting', '等待账号对应的登录表单加载…');
    var aliases = {
      '校园网': ['校园网', '校园', '教育网', '本校', 'campus', 'cernet'],
      '中国移动': ['中国移动', '移动', 'cmcc', 'china mobile', '铁通'],
      '中国联通': ['中国联通', '联通', 'unicom'],
      '中国电信': ['中国电信', '电信', 'telecom', 'chinanet', '天翼']
    };
    var names = aliases[cfg.provider] || [cfg.provider];
    function matches(s) { return names.some(function (n) { return norm(s).indexOf(norm(n)) >= 0; }); }
    function anyCarrier(s) { return Object.keys(aliases).some(function (key) { return aliases[key].some(function (n) { return norm(s).indexOf(norm(n)) >= 0; }); }); }
    function carrierName(s) {
      var value = norm(s);
      return Object.keys(aliases).find(function (key) {
        return aliases[key].some(function (name) { return value === norm(name) || value === norm(name) + '用户'; });
      });
    }
    function customCarrier() {
      // Only use identifiable selector/list controls. Static carrier names in
      // help text are not options, and selecting a menu item must be confirmed.
      var nodes = Array.from(document.querySelectorAll('[role=combobox],[role=option],li,div,span,a,button'));
      function menuItem(el) {
        for (var p = el, depth = 0; p && p !== document.body && depth < 4; p = p.parentElement, depth++) {
          if (imagePortal && imagePortal.root.contains(p) && /^bch_service_\d+$/.test(p.id || '')) return true;
          if (attr(p, 'role') === 'option' || p.tagName === 'LI' || /option|listbox|dropdown-menu|select-list|select_list/i.test(String(p.className || '') + ' ' + String(p.id || ''))) return true;
        }
        return false;
      }
      function trigger(el) {
        if (menuItem(el)) return false;
        for (var p = el, depth = 0; p && p !== document.body && depth < 4; p = p.parentElement, depth++) {
          if (attr(p, 'role') === 'combobox' || attr(p, 'aria-haspopup') === 'listbox' ||
              /select|dropdown|combo|carrier|operator|isp|service/i.test(String(p.className || '') + ' ' + String(p.id || ''))) return true;
        }
        return false;
      }
      var triggers = nodes.filter(function (el) { return actionable(el) && carrierName(text(el)) && trigger(el); });
      if (imagePortal) {
        // The current service sits beside net_access_type in an otherwise
        // unlabelled wrapper. Locate that wrapper structurally, not by a guessed
        // numeric provider code; all choices still come from visible text.
        var marker = imagePortal.root.querySelector('#net_access_type');
        for (var p = marker && marker.parentElement; p && p !== imagePortal.root; p = p.parentElement) {
          if (actionable(p) && carrierName(text(p))) { if (triggers.indexOf(p) < 0) triggers.push(p); break; }
        }
      }
      triggers = triggers.filter(function (el) { return !triggers.some(function (other) { return other !== el && el.contains(other); }); });
      var options = outermost(nodes.filter(function (el) { return actionable(el) && carrierName(text(el)) && menuItem(el); }));
      var state = window.__campusHelperCustomCarrier;
      if (!triggers.length && !options.length && !state) return null;
      if (triggers.length > 1) return result('blocked', '找到多个自定义运营商菜单，请在高级设置指定运营商选项。');
      var control = triggers[0];
      if (state && state.provider !== cfg.provider) state = window.__campusHelperCustomCarrier = null;
      function desired(el) { return cfg.providerValue ? norm(text(el)) === norm(cfg.providerValue) : carrierName(text(el)) === cfg.provider; }
      if (control && desired(control)) return { chosen: true };
      var selected = options.filter(function (el) { return attr(el, 'aria-selected') === 'true' && desired(el); });
      if (!control && selected.length === 1) return { chosen: true };
      if (!state) state = window.__campusHelperCustomCarrier = { provider: cfg.provider, started: Date.now() };
      if (Date.now() - state.started > 10000) return result('blocked', '自定义运营商菜单未能确认选择，请在高级设置填写对应选项的 CSS 选择器。');
      var matches = options.filter(function (el) { return cfg.providerValue ? norm(text(el)) === norm(cfg.providerValue) : carrierName(text(el)) === cfg.provider; });
      if (matches.length > 1) return result('blocked', '自定义菜单中有多个匹配运营商选项，请在高级设置指定选项。');
      if (matches.length === 1 && !state.clicked) {
        var choose = clickCustom(matches[0], '正在选择自定义菜单中的运营商，随后确认…');
        if (choose.clickControl) state.clicked = true;
        return choose;
      }
      if (!state.opened && control && !state.clicked) {
        var open = clickCustom(control, '正在展开自定义运营商菜单…');
        if (open.clickControl) state.opened = true;
        return open;
      }
      return result('waiting', '等待自定义运营商菜单确认选择…');
    }
    function available(o) { return !o.disabled && !(o.parentElement && o.parentElement.disabled); }
    function requested(o) { return available(o) && (o.value === cfg.providerValue || norm(optionText(o)) === norm(cfg.providerValue)); }
    function meaningful(o) { return available(o) && !/^(|[-—]+|请选择.*|选择.*|select.*|please.*)$/i.test(norm(optionText(o))); }
    function describe(el) {
      return Array.from(el.options).filter(meaningful).slice(0, 8).map(function (o) { return optionText(o).slice(0, 30); }).join('、');
    }
    var selects = Array.from(document.querySelectorAll('select')).filter(visible);
    function isArea(el) {
      var labels = el.labels ? Array.from(el.labels).map(text).join(' ') : '';
      var hint = String(el.id || '') + ' ' + String(el.name || '') + ' ' + labels + ' ' + (el.parentElement ? text(el.parentElement).slice(0, 120) : '');
      return selects.length === 1 || /区域|校区|服务|运营商|domain|realm|service|area|region|zone|isp|operator|carrier/i.test(hint);
    }
    // Some schools fetch the option list only when the real dropdown is opened.
    // Activate it once, then poll; never guess a carrier or skip an empty field.
    function waitForOptions(pending) {
      var activation = window.__campusHelperSelectActivation;
      if (!activation) activation = window.__campusHelperSelectActivation = { started: Date.now(), elements: [] };
      if (Date.now() - activation.started >= 12000) return result('blocked', '已填写账号并展开区域/运营商下拉框，但仍未读到可选项。请检查认证页是否正常加载；若手动展开能看到选项，请提供展开后的截图。');
      if (activation.elements.indexOf(pending) < 0) {
        activation.elements.push(pending);
        pending.scrollIntoView({ block: 'center', inline: 'nearest' });
        var rect = pending.getBoundingClientRect();
        var activate = result('waiting', '正在展开区域/运营商下拉框，读取可选项…');
        var x = rect.left + rect.width / 2, y = rect.top + rect.height / 2, view = window;
        while (view !== rootWindow && view.frameElement) {
          var frame = view.frameElement, bounds = frame.getBoundingClientRect();
          var sx = frame.offsetWidth ? bounds.width / frame.offsetWidth : 1;
          var sy = frame.offsetHeight ? bounds.height / frame.offsetHeight : 1;
          x = bounds.left + (frame.clientLeft + x) * sx;
          y = bounds.top + (frame.clientTop + y) * sy;
          view = view.parent;
        }
        activate.activateSelect = { x: x, y: y };
        return activate;
      }
      return result('waiting', '已展开区域/运营商下拉框，等待选项加载…');
    }
    var pending = selects.find(function (el) {
      return (isArea(el) || (cfg.providerSelector && el === query(cfg.providerSelector))) &&
        !Array.from(el.options).some(function (o) { return meaningful(o) || (cfg.providerValue && requested(o)); });
    });
    var chosen = false;
    var select;
    if (cfg.providerSelector) select = query(cfg.providerSelector);
    else {
      var candidates = selects.filter(function (el) { return Array.from(el.options).some(function (o) { return cfg.providerValue ? requested(o) : meaningful(o) && matches(optionText(o)); }); });
      if (candidates.length > 1) return result('blocked', '找到多个运营商/区域下拉框，请在高级设置填写对应控件的 CSS 选择器。');
      select = candidates[0];
    }
    if (select && select.tagName === 'SELECT' && visible(select)) {
      var options = Array.from(select.options).filter(function (o) { return cfg.providerValue ? requested(o) : meaningful(o) && matches(optionText(o)); });
      if (options.length > 1) return result('blocked', '有多个匹配选项（' + describe(select) + '），请在高级设置填写准确的选项值或文字。');
      var option = options[0];
      if (!option && !Array.from(select.options).some(meaningful)) return waitForOptions(select);
      if (!option) return result('blocked', '运营商/区域没有对应选项（' + describe(select) + '），请检查选择或填写准确的选项值/文字。');
      chooseOption(select, option); chosen = true;
    } else if (cfg.providerSelector) {
      if (!visible(select)) return result('waiting', '暂未找到运营商控件。');
      if (!select.__campusHelperClicked) { select.click(); select.__campusHelperClicked = true; }
      chosen = true;
    } else {
      var radio = Array.from(document.querySelectorAll('input[type=radio]')).find(function (el) {
        var label = el.closest('label');
        if (!label && el.id) label = Array.from(document.querySelectorAll('label')).find(function (l) { return l.htmlFor === el.id; });
        return !el.disabled && ((label && visible(label) && matches(text(label))) || (visible(el) && matches(el.value)));
      });
      if (radio) {
        if (!radio.checked || !radio.__campusHelperClicked) { radio.click(); radio.dispatchEvent(new Event('change', { bubbles: true })); radio.__campusHelperClicked = true; }
        chosen = true;
      }
    }
    // Choose a loaded carrier first: its change handler may be responsible for
    // loading a second, initially empty service/area dropdown.
    if (!chosen && pending) return waitForOptions(pending);
    if (!chosen && !cfg.providerSelector) {
      var custom = customCarrier();
      if (custom && !custom.chosen) return custom;
      if (custom && custom.chosen) chosen = true;
    }
    if (!chosen && cfg.providerValue) return result('blocked', '没有找到所填写的运营商/区域选项，请检查选项文字或值，必要时填写 CSS 选择器。');
    if (!chosen && !cfg.accountSuffix) {
      var carrierPresent = selects.some(function (el) { return Array.from(el.options).some(function (o) { return anyCarrier(optionText(o)); }); }) ||
        Array.from(document.querySelectorAll('input[type=radio]')).some(function (el) {
          var label = el.closest('label');
          if (!label && el.id) label = Array.from(document.querySelectorAll('label')).find(function (l) { return l.htmlFor === el.id; });
          return (visible(el) && anyCarrier(el.value)) || (label && visible(label) && anyCarrier(text(label)));
        });
      if (cfg.provider !== '校园网' || carrierPresent) return result('blocked', '未找到所选运营商。没有运营商选项的学校请选择“校园网”；有区域选项时可在高级设置填写选项文字或值。');
      // Campus-only portals need no carrier selection. Keep the school's valid default.
      chosen = true;
    }
    // An area/realm is independent of an ISP. Never guess between multiple areas.
    for (var si = 0; si < selects.length; si++) {
      var area = selects[si];
      if (!document.documentElement.contains(area)) return result('waiting', '等待运营商/区域表单切换完成…');
      if (!visible(area)) continue;
      if (area === select) continue;
      if (!isArea(area)) continue;
      var usable = Array.from(area.options).filter(meaningful);
      if (usable.length === 0) return waitForOptions(area);
      var selected = area.selectedIndex >= 0 ? area.options[area.selectedIndex] : null;
      if (selected && meaningful(selected)) continue;
      if (usable.length === 1) chooseOption(area, usable[0]);
      else return result('blocked', '请选择区域/服务：' + describe(area) + '。在高级设置“区域/选项值”填写准确的选项文字或值。');
    }
    // Synchronous change handlers can clear or replace earlier controls. Do not
    // submit until the next poll has inspected the new, stable form.
    for (var ci = 0; ci < selects.length; ci++) {
      var currentSelect = selects[ci];
      if (!document.documentElement.contains(currentSelect)) return result('waiting', '等待运营商/区域表单切换完成…');
      if (!visible(currentSelect) || (currentSelect !== select && !isArea(currentSelect))) continue;
      var currentOption = currentSelect.options[currentSelect.selectedIndex];
      if (currentSelect === select && currentOption && currentOption !== option) return result('waiting', '运营商选项被页面更新，正在重新确认选择…');
      if (!currentOption || !(meaningful(currentOption) || (currentSelect === select && cfg.providerValue && requested(currentOption)))) {
        if (!Array.from(currentSelect.options).some(meaningful)) return waitForOptions(currentSelect);
        return result('waiting', '区域选项已更新，正在重新确认选择…');
      }
    }
    // Some portals replace account inputs when the operator changes.
    if (!document.documentElement.contains(username) || !document.documentElement.contains(password)) {
      pair = fields(document); password = pair.password; username = pair.username;
      if (!visible(username) || !visible(password)) return result('waiting', '等待运营商账号表单加载…');
    }
    if (imagePortal) {
      var additional = ['operatorUserId_tip', 'operatorPwd_tip', 'operatorUserId', 'operatorPwd'].map(function(id) { return query('#' + id); });
      if (additional.some(function(el) { return visible(el) && (/_tip$/.test(el.id) || !el.value); }))
        return result('waiting', '此运营商需要额外的账号或密码，请在认证页补充填写；软件不会把校园账号重复填入运营商账号框。');
    }
    var buttons = cfg.loginSelector ? [] : loginButtons();
    if (!cfg.loginSelector && imagePortal && actionable(imagePortal.button)) {
      buttons = buttons.filter(function(el) { return el !== imagePortal.button && !el.contains(imagePortal.button) && !imagePortal.button.contains(el); });
      buttons.push(imagePortal.button);
    }
    if (buttons.length > 1) return result('blocked', '找到多个登录按钮，请在高级设置填写登录按钮 CSS 选择器。');
    var button = cfg.loginSelector ? query(cfg.loginSelector) : buttons[0];
    if (!actionable(button)) {
      var missingButton = result('waiting', '暂未找到可用的登录按钮，密码尚未填写。');
      missingButton.diagnostic = '按钮识别[v1.9.8]：标准控件=' + Array.from(document.querySelectorAll('button,input[type=submit],input[type=button],input[type=image],a')).filter(visible).length +
        '，自定义按钮=' + Array.from(document.querySelectorAll('[role=button],[onclick]')).filter(visible).length + '，匹配=' + buttons.length +
        '，表单位置=' + (document === rootDocument ? '主页面' : '内嵌页面') + '，内嵌页面=' + document.querySelectorAll('iframe,frame').length;
      return missingButton;
    }
    if (window.__campusHelperSubmitted) return result('submitted', '登录请求已提交，等待页面确认…');
    if (username.value !== cfg.username + cfg.accountSuffix) {
      change(username, cfg.username + cfg.accountSuffix);
      return result('waiting', '账号表单已更新，正在重新确认区域选项…');
    }
    var loginPoint = null;
    if (button.ownerDocument && !/^(BUTTON|INPUT|A)$/.test(button.tagName)) {
      loginPoint = controlPoint(button);
      if (!loginPoint) return result('waiting', '登录按钮暂时被遮挡，等待页面就绪…');
    }
    change(password, cfg.password);
    if (loginPoint) {
      if (!document.documentElement.contains(button) || !actionable(button)) return result('waiting', '登录表单已更新，正在重新识别按钮…');
      loginPoint = controlPoint(button);
      if (!loginPoint) return result('waiting', '登录按钮暂时被遮挡，等待页面就绪…');
    }
    window.__campusHelperErrorsBeforeSubmit = failures();
    window.__campusHelperSubmitted = true;
    rootWindow.__campusHelperLoginDocument = document;
    var submittedResult = result('submitted', '登录请求已提交，等待页面确认…');
    if (loginPoint) submittedResult.clickControl = loginPoint;
    else button.click();
    return submittedResult;
  } catch (error) { return result('blocked', error.message || '操作认证表单失败。'); }
})(window, document, location);
  else state = (function () {
  'use strict';
  var cfg = configuration;
  function visible(el) { return el && !el.disabled && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden'; }
  function text(el) { return String(el.innerText || el.textContent || el.value || '').trim(); }
  function norm(s) { return String(s).replace(/\s+/g, '').toLowerCase(); }
  function result(state, message) { return { state: state, message: message }; }
  function query(selector) {
    try { return document.querySelector(selector); }
    catch (_) { throw new Error('页面识别配置无效，请在高级设置检测登录页面。'); }
  }
  function change(el, value) {
    var setter = Object.getOwnPropertyDescriptor(el.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype, 'value');
    if (setter && setter.set) setter.set.call(el, value); else el.value = value;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new Event('blur', { bubbles: true }));
  }
  function success() {
    var body = document.body ? text(document.body) : '';
    var logged = /登录成功|认证成功|您已登录|已经登录|在线时长|上网时长|累计在线|login\s+success|successfully\s+logged/i.test(body);
    var logout = Array.from(document.querySelectorAll('input,button,a')).some(function (el) {
      return visible(el) && /^(注销|退出登录|断开连接|下线|logout|log out)$/i.test(text(el));
    });
    var pass = Array.from(document.querySelectorAll('input[type=password]')).some(visible);
    return (logged || logout) && !pass;
  }
  function fail() {
    var body = document.body ? text(document.body) : '';
    var match = body.match(/(?:密码错误|账号或密码错误|用户名或密码错误|认证失败|登录失败|余额不足|账号欠费|用户不存在|账号不存在|用户已在线|设备数超限|password\s+incorrect|invalid\s+(?:password|credentials))[^\n]{0,70}/i);
    return match ? match[0] : null;
  }
  try {
    // Chromium briefly exposes about:blank before a navigation completes.
    // Its internal error page is a connection failure, not a portal redirect.
    if (/^(chrome-error|edge-error):$/.test(location.protocol)) return result('failed', '无法打开认证页。请先连接校园 Wi-Fi 或有线网络，确认浏览器能访问认证地址后重试。');
    if (location.protocol === 'about:') return result('waiting', '等待认证页加载…');
    if (!/^https?:$/.test(location.protocol)) return result('blocked', '当前页面不是 HTTP/HTTPS 认证页，请检查认证地址。');
    if (!document.body || document.readyState === 'loading') return result('waiting', '等待认证页加载…');
    // A portal may switch ports or upgrade HTTP to HTTPS on the same host.
    // For a different server, only return the discovered URL; never fill credentials.
    var configured = new URL(cfg.origin);
    var sameServer = location.hostname === configured.hostname && !(configured.protocol === 'https:' && location.protocol === 'http:');
    if (!sameServer) {
      // Wait through intermediate redirects instead of stopping at the first document.
      if (document.readyState !== 'complete') return result('waiting', '等待认证页跳转完成…');
      var redirected = result('redirected', '检测到另一台认证服务器：' + location.origin + location.pathname);
      redirected.redirectUrl = location.href;
      return redirected;
    }
    if (success()) return result('success', '认证页显示已登录。');
    if (cfg.phase === 'verify') {
      var error = fail();
      return error ? result('failed', '认证页提示：' + error) : result('waiting', '等待认证结果…');
    }
    var inputs = Array.from(document.querySelectorAll('input'));
    var password = cfg.passwordSelector ? query(cfg.passwordSelector) : inputs.find(function (el) { return visible(el) && el.type === 'password'; });
    var username = cfg.usernameSelector ? query(cfg.usernameSelector) : inputs.find(function (el) {
      return visible(el) && el.type !== 'password' && /^(text|email|tel|number)$/.test(el.type) && /^(DDDDD|username|user_name|user_account|account|user|loginName|loginname)$/i.test(el.name || el.id);
    });
    if (!username && !cfg.usernameSelector) username = inputs.find(function (el) { return visible(el) && /^(text|email|tel)$/.test(el.type); });
    if (!visible(username) || !visible(password)) return result('waiting', '暂未找到账号和密码输入框。');
    if (username.tagName !== 'INPUT' || password.tagName !== 'INPUT') return result('blocked', '识别到的输入框与页面不符，请在高级设置检测登录页面。');
    var aliases = {
      '校园网': ['校园网', '校园', '教育网', '本校', 'campus'],
      '中国移动': ['中国移动', '移动', 'cmcc'],
      '中国联通': ['中国联通', '联通', 'unicom'],
      '中国电信': ['中国电信', '电信', 'telecom']
    };
    var names = aliases[cfg.provider] || [cfg.provider];
    function matches(s) { return names.some(function (n) { return norm(s).indexOf(norm(n)) >= 0; }); }
    var chosen = false;
    var select = cfg.providerSelector ? query(cfg.providerSelector) : Array.from(document.querySelectorAll('select')).find(function (el) { return visible(el) && Array.from(el.options).some(function (o) { return matches(o.text); }); });
    if (select && select.tagName === 'SELECT' && visible(select)) {
      var option = Array.from(select.options).find(function (o) { return !o.disabled && (cfg.providerValue ? o.value === cfg.providerValue : matches(o.text)); });
      if (!option) return result('blocked', '运营商下拉框里没有对应选项，请确认所选运营商；仍失败时请在高级设置检测登录页面。');
      change(select, option.value); chosen = true;
    } else if (cfg.providerSelector) {
      if (!visible(select)) return result('waiting', '暂未找到运营商控件。');
      select.click(); chosen = true;
    } else {
      var radio = Array.from(document.querySelectorAll('input[type=radio]')).find(function (el) {
        var label = el.closest('label');
        if (!label && el.id) label = Array.from(document.querySelectorAll('label')).find(function (l) { return l.htmlFor === el.id; });
        return !el.disabled && ((label && visible(label) && matches(text(label))) || (visible(el) && matches(el.value)));
      });
      if (radio) { radio.click(); radio.dispatchEvent(new Event('change', { bubbles: true })); chosen = true; }
    }
    if (!chosen && !cfg.accountSuffix) return result('blocked', '无法识别运营商控件，请在高级设置点击“检测登录页面”，保存报告用于适配。');
    // Some portals replace account inputs when the operator changes.
    if (!document.documentElement.contains(username) || !document.documentElement.contains(password)) {
      inputs = Array.from(document.querySelectorAll('input'));
      password = cfg.passwordSelector ? query(cfg.passwordSelector) : inputs.find(function (el) { return visible(el) && el.type === 'password'; });
      username = cfg.usernameSelector ? query(cfg.usernameSelector) : inputs.find(function (el) { return visible(el) && /^(text|email|tel)$/.test(el.type); });
      if (!visible(username) || !visible(password)) return result('waiting', '等待运营商账号表单加载…');
    }
    var buttons = Array.from(document.querySelectorAll('button,input[type=submit],input[type=button],a'));
    var button = cfg.loginSelector ? query(cfg.loginSelector) : buttons.find(function (el) { return visible(el) && /^(登\s*录|登录上网|连接|立即登录|login|log in|sign in)$/i.test(text(el)); });
    if (!visible(button)) return result('waiting', '暂未找到登录按钮。');
    if (window.__campusHelperSubmitted) return result('submitted', '登录请求已提交，等待页面确认…');
    change(username, cfg.username + cfg.accountSuffix);
    change(password, cfg.password);
    window.__campusHelperSubmitted = true;
    button.click();
    return result('submitted', '登录请求已提交，等待页面确认…');
  } catch (error) { return result('blocked', error.message || '操作认证表单失败。'); }
})();
  // Stay with the submitted adapter while success UI hides/replaces its fields.
  // Never try another submission merely because a portal reports failure.
  if (state.state === 'submitted') window.__campusUnifiedAdapter = adapter;
  state.adapter = adapter;
  if ((state.state === 'blocked' || state.state === 'waiting') && /CSS|选择器|在高级设置填写|高级设置[“「]/.test(state.message))
    state.message = '暂时无法自动匹配此登录页面，请在高级设置点击“点击检测”并保存报告。';
  return state;
})()
