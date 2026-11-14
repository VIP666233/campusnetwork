using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using CampusNet;
class DiagnosticTests {
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static async Task Run(string root) {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        string url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/?token=PRIVATE_QUERY";
        var serving = Task.Run(async () => {
            while (true) {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(); } catch { break; }
                var response = Task.Run(async () => {
                    using (client) using (var stream = client.GetStream()) {
                        var reader = new StreamReader(stream); while (!String.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                        byte[] body = Encoding.UTF8.GetBytes("<!doctype html><meta charset='utf-8'><body>fixture</body>");
                        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html;charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header,0,header.Length); await stream.WriteAsync(body,0,body.Length);
                    }
                });
            }
        });
        using (var browser = new BrowserSession(root)) using (var timeout = new CancellationTokenSource(30000)) {
            Exception failure = null;
            try {
                await browser.StartAsync(url, false, timeout.Token);
                for (int i=0;i<50;i++) { var state=await browser.EvaluateAsync("({ready:document.readyState==='complete'&&location.protocol==='http:'})", timeout.Token); if(Convert.ToBoolean(state["ready"])) break; await Task.Delay(100); }
                string html = "<input type='text' name='username' value='PRIVATE_ACCOUNT'><input type='password' name='password' value='PRIVATE_SECRET'><input type='hidden' value='PRIVATE_HIDDEN'><div id='loginButton' style='cursor:pointer' onclick='window.clickCount++'><span>连接</span><span> Login</span></div><input type='button' value='连接 Login'><div style='cursor:pointer'>PRIVATE_BODY</div><iframe></iframe>";
                var json = new JavaScriptSerializer();
                await browser.EvaluateAsync("(function(){document.body.innerHTML="+json.Serialize(html)+";window.clickCount=0;document.cookie='secret=PRIVATE_COOKIE';localStorage.setItem('secret','PRIVATE_STORAGE');var d=document.querySelector('iframe').contentDocument;d.body.innerHTML='<button>连接 Login</button>';document.querySelectorAll('input').forEach(function(el){if(el.type!=='button')Object.defineProperty(el,'value',{get:function(){throw new Error('Credential value read');}});});var host=document.createElement('div');document.body.appendChild(host);host.attachShadow({mode:'open'}).innerHTML='<input type=password value=PRIVATE_SHADOW><button>登录</button>';return {ok:true};})()", timeout.Token);
                var report=await browser.EvaluateAsync(PortalDiagnosticsReport.Script(),timeout.Token);
                Check(report.ContainsKey("pages"),"Inspector executes without reading credential field values");
                string output=json.Serialize(report);
                foreach (string secret in new[]{"PRIVATE_ACCOUNT","PRIVATE_SECRET","PRIVATE_HIDDEN","PRIVATE_BODY","PRIVATE_COOKIE","PRIVATE_STORAGE","PRIVATE_QUERY","PRIVATE_SHADOW"}) Check(!output.Contains(secret),"Excluded "+secret);
                Check(output.Contains("loginButton") && output.Contains("连接") && output.Contains("Login"),"Custom button structure and caption retained");
                Check(output.Contains("main/shadow0"),"Open shadow DOM controls included");
                Check(output.Contains("main/frame0"),"Embedded page structure included");
                Check(output.Contains("html ") && output.Contains("nth-of-type"),"Exact structural selectors retained");
                var clicked=await browser.EvaluateAsync("({count:window.clickCount})",timeout.Token);
                Check(Convert.ToInt32(clicked["count"])==0,"Read-only inspection never clicks login");
                var samples=new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string,object>> { report };
                string formatted=PortalDiagnosticsReport.Export(samples); Check(!formatted.Contains("PRIVATE_SECRET") && formatted.Contains("控件结构"),"Export is readable and excludes private values");
                File.WriteAllText(Path.Combine(root,"synthetic-report.txt"),formatted);
            } catch(Exception ex) { failure=ex; }
            await browser.CloseAsync(); listener.Stop();
            if(failure!=null) throw failure;
        }
    }
    static void Main(string[] args) { try{Run(args[0]).GetAwaiter().GetResult();}catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;} }
}
