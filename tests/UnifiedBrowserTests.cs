using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using CampusNet;

// Real Chromium, local synthetic pages and fixture credentials only.
class UnifiedBrowserTests
{
    static int checks;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
    sealed class Server : IDisposable
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        bool stopped;
        public string Url;
        public Server() { listener.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/portal.do"; Task.Run((Func<Task>)Serve); }
        async Task Serve() { while (!stopped) { try { var client = await listener.AcceptTcpClientAsync(); var pending = Reply(client); } catch { if (!stopped) throw; } } }
        async Task Reply(TcpClient client)
        {
            try { using (client) using (var stream = client.GetStream()) {
                var reader = new StreamReader(stream); while (!String.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                byte[] body = Encoding.UTF8.GetBytes("<!doctype html><meta charset='utf-8'><body>Fixture</body>");
                byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html;charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header,0,header.Length); await stream.WriteAsync(body,0,body.Length);
            }} catch (IOException) { }
        }
        public void Dispose() { stopped = true; listener.Stop(); }
    }
    static async Task Reset(BrowserSession browser, string html, CancellationToken token)
    {
        await browser.EvaluateAsync("(function(){Object.keys(window).filter(function(k){return k.indexOf('__campus')===0;}).forEach(function(k){delete window[k];});document.open();document.write(" + Json.Serialize(html) + ");document.close();window.submitCount=0;return {ok:true};})()",token);
    }
    static string Native(string school, bool blank)
    {
        string options = blank ? "<option value='school-default'></option>" : "<option value='c'>校园网</option><option value='m'>中国移动</option><option value='u'>中国联通</option><option value='t'>中国电信</option>";
        return "<!doctype html><meta charset='utf-8'><body><p>" + school + "</p><input name='loginName' id='account'><input type='password' id='secret'><label>区域<select id='domain'>" + options + "</select></label><button onclick='window.submitCount++'>登录</button></body>";
    }
    static string Code(string provider) { return provider == "校园网" ? "c" : provider == "中国移动" ? "m" : provider == "中国联通" ? "u" : "t"; }
    static async Task Run(string root, string imageFile)
    {
        using (var server = new Server()) using (var browser = new BrowserSession(Path.GetFullPath(root))) using (var timeout = new CancellationTokenSource(90000))
        {
            Exception failure = null;
            try {
                var token = timeout.Token;
                await browser.StartAsync(server.Url,false,token);
                for (int i=0;i<100;i++) {
                    var ready = await browser.EvaluateAsync("({ready:document.readyState==='complete' && location.pathname==='/portal.do'})",token);
                    if (Convert.ToBoolean(ready["ready"])) break;
                    if(i==99) throw new Exception("Local fixture failed to load");
                    await Task.Delay(50,token);
                }
                foreach (string kind in new[] { "classic", "huadian-blank", "huadian-carriers" }) foreach (string provider in new[] { "校园网","中国移动","中国联通","中国电信" }) {
                    bool huadian = kind.StartsWith("huadian"), blank = kind.EndsWith("blank");
                    await Reset(browser,Native(huadian ? "华北电力大学 华电" : "校园网认证",blank),token);
                    var cfg = new Settings { Url=server.Url, Username="fixture-user", Provider=provider };
                    var result = await browser.EvaluateAsync(LoginService.BuildScript(cfg,"fixture-secret",false),token);
                    Check((string)result["adapter"]==(huadian?"huadian":"classic") && (string)result["state"]=="submitted",kind+" / "+provider+" automatically routes and submits");
                    await browser.EvaluateAsync(LoginService.BuildScript(cfg,"fixture-secret",false),token);
                    var values = await browser.EvaluateAsync("({count:submitCount,user:account.value,pass:secret.value,carrier:domain.value})",token);
                    Check(Convert.ToInt32(values["count"])==1 && (string)values["user"]=="fixture-user" && (string)values["pass"]=="fixture-secret",kind+" fills real fields and submits once");
                    Check((string)values["carrier"]==(blank?"school-default":Code(provider)),kind+" preserves default or selects requested carrier");
                    await browser.EvaluateAsync("(function(){document.body.innerHTML='<p>登录成功</p>';return {ok:true};})()",token);
                    result = await browser.EvaluateAsync(LoginService.BuildScript(cfg,"fixture-secret",true),token);
                    Check((string)result["state"]=="success" && (string)result["adapter"]==(huadian?"huadian":"classic"),kind+" confirms with submitted adapter after form disappears");
                }
                await Reset(browser,Native("另一学校",true),token);
                var unknown = new Settings { Url=server.Url, Username="fixture-user", Provider="校园网" };
                var unknownResult = await browser.EvaluateAsync(LoginService.BuildScript(unknown,"fixture-secret",false),token);
                var unknownValues = await browser.EvaluateAsync("({count:submitCount,pass:secret.value})",token);
                Check((string)unknownResult["adapter"]=="classic" && Convert.ToInt32(unknownValues["count"])==0 && (string)unknownValues["pass"]=="", "Unknown blank region never inherits Huadian exception");
                await Reset(browser,Native("华电",true).Replace("name='loginName'","name='schoolAccountField'"),token);
                var renamed = await browser.EvaluateAsync(LoginService.BuildScript(unknown,"fixture-secret",false),token);
                Check((string)renamed["adapter"]=="huadian" && (string)renamed["state"]=="submitted","Huadian identification does not depend on guessed input names");

                string imageTemplate = File.ReadAllText(imageFile,Encoding.UTF8);
                foreach (string scenario in new[] { "campus","mobile","unicom","telecom","iframe","hidden-image","extra-credentials","wrong-fingerprint" }) {
                    bool embedded = scenario=="iframe", negative=scenario=="hidden-image" || scenario=="extra-credentials" || scenario=="wrong-fingerprint";
                    string html = imageTemplate;
                    if (scenario=="wrong-fingerprint") html=html.Replace("id=\"logininfo_hk\"","id=\"unrelated\"");
                    await Reset(browser,embedded ? "<!doctype html><body><iframe id='school' style='width:700px;height:650px'></iframe></body>" : html,token);
                    if (embedded) {
                        await browser.EvaluateAsync("(function(){school.srcdoc="+Json.Serialize(html)+";return {ok:true};})()",token);
                        await Task.Delay(200,token);
                    }
                    if(scenario=="hidden-image") await browser.EvaluateAsync("(function(){loginLink.style.display='none';return {ok:true};})()",token);
                    if(scenario=="extra-credentials") await browser.EvaluateAsync("(function(){extra.style.display='';return {ok:true};})()",token);
                    var cfg = new Settings { Url=server.Url, Username="fixture-user", Provider=scenario=="mobile"?"中国移动":scenario=="unicom"?"中国联通":scenario=="telecom"?"中国电信":"校园网" };
                    var result = await browser.EvaluateAsync(LoginService.BuildScript(cfg,"fixture-secret",false),token);
                    for(int i=0;(string)result["state"]=="waiting" && i<12;i++) { await Task.Delay(100,token); result=await browser.EvaluateAsync(LoginService.BuildScript(cfg,"fixture-secret",false),token); }
                    Check(negative ? (string)result["state"]!="submitted" : (string)result["state"]=="submitted" && (string)result["adapter"]=="enhanced","Image / "+scenario+" auto detection result: "+result["state"]);
                    string target = embedded?"school.contentWindow":"window";
                    await browser.EvaluateAsync(LoginService.BuildScript(cfg,"fixture-secret",false),token);
                    var values=await browser.EvaluateAsync("(function(){var w="+target+";return {count:w.submitCount,remember:w.rememberClicks,userTip:w.username_tip.value,passTip:w.pwd_tip.value,sentUser:w.sentUser||'',sentPass:w.sentPass||'',service:w.net_access_type.value,extraUser:w.operatorUserId.value,extraPass:w.operatorPwd.value};})()",token);
                    Check(Convert.ToInt32(values["count"])==(negative?0:1) && Convert.ToInt32(values["remember"])==0,scenario+" never resubmits or clicks remember-password");
                    Check((string)values["extraUser"]=="" && (string)values["extraPass"]=="",scenario+" keeps independent carrier credentials untouched");
                    if(!negative) {
                        Check((string)values["sentUser"]=="fixture-user" && (string)values["sentPass"]=="fixture-secret" && (string)values["service"]==Code(cfg.Provider),scenario+" image handler receives correct credentials and carrier");
                        Check((string)values["userTip"]=="账号" && (string)values["passTip"]=="密码",scenario+" never fills decorative tip fields");
                        await browser.EvaluateAsync("(function(){"+target+".document.body.innerHTML='<p>登录成功</p>';return {ok:true};})()",token);
                        result=await browser.EvaluateAsync(LoginService.BuildScript(cfg,"fixture-secret",true),token);
                        Check((string)result["state"]=="success" && (string)result["adapter"]=="enhanced",scenario+" verifies success after losing fingerprint");
                    }
                }
                // A failed submitted flow stays on its adapter instead of trying another login.
                await Reset(browser,Native("校园网认证",false),token);
                await browser.EvaluateAsync(LoginService.BuildScript(unknown,"fixture-secret",false),token);
                await browser.EvaluateAsync("(function(){document.body.insertAdjacentHTML('beforeend','<p>华电 密码错误</p>');return {ok:true};})()",token);
                var failed=await browser.EvaluateAsync(LoginService.BuildScript(unknown,"fixture-secret",true),token);
                Check((string)failed["state"]=="failed" && (string)failed["adapter"]=="classic","Failure does not switch adapter or retry credentials");
            } catch(Exception ex) { failure=ex; }
            await browser.CloseAsync();
            if(failure!=null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        Console.WriteLine("ALL "+checks+" UNIFIED BROWSER CHECKS PASSED");
    }
    static void Main(string[] args) { try { Run(args[0],args[1]).GetAwaiter().GetResult(); } catch(Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode=1; } }
}
