(function () {
  'use strict';
  var cfg = __CONFIG__;
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
})()
