using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CampusNet;
class SuccessTimingChecks {
    class Progress : IProgress<string> {
        public readonly Stopwatch Clock=Stopwatch.StartNew();
        public long Submitted=-1, Confirmed=-1;
        public void Report(string s) {
            Console.WriteLine(Clock.ElapsedMilliseconds + " ms: " + s);
            if(s.Contains("登录请求已提交") && Submitted<0)Submitted=Clock.ElapsedMilliseconds;
            if(s.Contains("认证页显示已登录"))Confirmed=Clock.ElapsedMilliseconds;
        }
    }
    static async Task Reply(TcpClient client) {
        using(client)using(var stream=client.GetStream()) {
            var reader=new StreamReader(stream);while(!String.IsNullOrEmpty(await reader.ReadLineAsync())){}
            string html="<!doctype html><meta charset='utf-8'><body><input name='username'><input type='password'><select><option>校园网</option></select><button onclick=\"setTimeout(function(){document.body.innerHTML='登录成功';},100)\">登录</button></body>";
            byte[] body=Encoding.UTF8.GetBytes(html);
            byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html;charset=utf-8\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header,0,header.Length);await stream.WriteAsync(body,0,body.Length);
        }
    }
    static async Task Run(string root) {
        var server=new TcpListener(IPAddress.Loopback,0);server.Start();
        var serving=Task.Run(async ()=>{try{while(true){var client=await server.AcceptTcpClientAsync();var pending=Reply(client);}}catch(ObjectDisposedException){}catch(SocketException){}});
        try {
            var cfg=new Settings { Url="http://127.0.0.1:"+((IPEndPoint)server.LocalEndpoint).Port+"/",Username="fixture-user",EncryptedPassword=Settings.Protect("fixture-secret"),Provider="校园网" };
            var progress=new Progress();
            using(var timeout=new CancellationTokenSource(30000)) {
                var result=await LoginService.RunAsync(cfg,false,progress,timeout.Token,Path.GetFullPath(root));
                if(!result.Confirmed || progress.Submitted<0 || progress.Confirmed<0)throw new Exception("Synthetic login did not confirm");
                Console.WriteLine("Submit-to-confirm: "+(progress.Confirmed-progress.Submitted)+" ms");
                Console.WriteLine("Confirmed-to-browser-cleanup: "+(progress.Clock.ElapsedMilliseconds-progress.Confirmed)+" ms");
            }
        }finally{server.Stop();}
    }
    static void Main(string[] args) {try{Run(args[0]).GetAwaiter().GetResult();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
}
