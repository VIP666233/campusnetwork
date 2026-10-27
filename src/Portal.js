(function () {
  'use strict';
  var cfg = __CONFIG__;
  function visible(el) { return el && !el.disabled && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden'; }
  function text(el) { return String(el.innerText || el.textContent || el.value || '').trim(); }
  function norm(s) { return String(s).replace(/\s+/g, '').toLowerCase(); }
  function result(state, message) { return { state: state, message: message }; }
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
    if (username.tagName !== 'INPUT' || password.tagName !== 'INPUT') return result('blocked', '账号或密码选择器必须指向 input 元素。');
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
      if (!option) return result('blocked', '运营商下拉框里没有对应选项，请检查运营商或高级设置的选项值。');
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
    if (!chosen && !cfg.accountSuffix) return result('blocked', '无法识别运营商控件。请在高级设置中填写运营商 CSS 选择器，或该校园明确使用的账号后缀。');
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
})()
