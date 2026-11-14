using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CampusNet {
    // Only the login orchestration is under test; use a fixture password so the
    // sandbox test neither decrypts user settings nor requires a loaded profile.
    public sealed class Settings {
        public string Url,Username,Provider="校园网",UsernameSelector="",PasswordSelector="",ProviderSelector="",ProviderValue="",LoginSelector="",AccountSuffix="";
        public string GetPassword() { return "local-fixture-secret"; }
        public static string NormalizeUrl(string value) { var u=new Uri(value); if((u.Scheme!="http"&&u.Scheme!="https")||u.UserInfo.Length>0)throw new ArgumentException("Invalid URL"); return u.AbsoluteUri; }
    }
    // Compile this fixture instead of BrowserSession. No processes or web requests.
    sealed class BrowserSession : IDisposable {
        public static int Starts,Closes,Submissions;
        public static bool RedirectAfterSubmit,FailPassword;
        public static bool WaitDuringStartup,WaitForForm,CloseOnEvaluate;
        bool secondPortal;
        public BrowserSession(string root=null) { Starts=Closes=Submissions=0; }
        public async Task StartAsync(string url,bool visible,CancellationToken token,IProgress<string> progress) { Starts++; if(WaitDuringStartup)await Task.Delay(5000,token); }
        public Task<Dictionary<string,object>> EvaluateAsync(string script,CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if(CloseOnEvaluate)throw new InvalidOperationException("fixture browser closed");
            if(WaitForForm)return Reply("waiting",null);
            var cfg=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(script);
            string current=(string)cfg["origin"],phase=(string)cfg["phase"];
            string target=secondPortal?"http://10.2.5.252/home":"http://10.2.5.251/a79.htm";
            if(current!=new Uri(target).GetLeftPart(UriPartial.Authority))return Reply("redirected",target);
            if(phase=="submit") {
                if(Submissions>0)throw new Exception("Repeated credential submission");
                Submissions++; if(RedirectAfterSubmit)secondPortal=true;
                return Reply("submitted",null);
            }
            return Reply(FailPassword?"failed":"success",null);
        }
        static Task<Dictionary<string,object>> Reply(string kind,string target) { var d=new Dictionary<string,object> { {"state",kind},{"message","fixture "+kind} }; if(target!=null)d.Add("redirectUrl",target); return Task.FromResult(d); }
        public Task CloseAsync() { Closes++; return Task.FromResult(0); }
        public void Dispose() {}
    }
    public static class LoginFlowTests {
        sealed class Progress : IProgress<string> { public void Report(string value) {} }
        public static string[] RunAll() {
            var results=new List<string>();
            var cfg=new Settings { Url="http://9.9.9.9.com/",Username="local-test-user" };
            BrowserSession.RedirectAfterSubmit=false; BrowserSession.FailPassword=false;
            var fastCfg=new Settings { Url="http://10.2.5.251/a79.htm",Username="local-test-user" };
            var elapsed=System.Diagnostics.Stopwatch.StartNew();
            var quick=LoginService.RunAsync(fastCfg,false,new Progress(),CancellationToken.None).GetAwaiter().GetResult();
            elapsed.Stop();
            Check(quick.Confirmed && BrowserSession.Submissions==1 && BrowserSession.Closes==1 && elapsed.ElapsedMilliseconds<1200,"Confirmed login closes promptly without repeating submission ("+elapsed.ElapsedMilliseconds+" ms)",results);
            var r=LoginService.RunAsync(cfg,false,new Progress(),CancellationToken.None).GetAwaiter().GetResult();
            Check(r.Confirmed&&r.Submitted&&BrowserSession.Submissions==1,"One call discovers campus origin and completes login",results);
            Check(BrowserSession.Starts==1&&BrowserSession.Closes==1,"Discovery reuses and closes one browser session",results);
            Check(r.PortalUrl=="http://10.2.5.251/a79.htm"&&cfg.Url=="http://9.9.9.9.com/","Keep configured entry point after discovering school portal",results);
            BrowserSession.RedirectAfterSubmit=true;
            r=LoginService.RunAsync(cfg,false,new Progress(),CancellationToken.None).GetAwaiter().GetResult();
            Check(r.Confirmed&&BrowserSession.Submissions==1&&r.PortalUrl=="http://10.2.5.252/home","Post-submit redirect preserves verification phase and submits once",results);
            BrowserSession.RedirectAfterSubmit=false; BrowserSession.FailPassword=true;
            r=LoginService.RunAsync(cfg,false,new Progress(),CancellationToken.None).GetAwaiter().GetResult();
            Check(!r.Confirmed&&r.Submitted&&BrowserSession.Submissions==1,"Failed credentials stop without retrying submission",results);
            using(var cancelled=new CancellationTokenSource()) {
                cancelled.Cancel(); bool stopped=false;
                try { LoginService.RunAsync(cfg,false,new Progress(),cancelled.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { stopped=true; }
                Check(stopped&&BrowserSession.Closes==1,"Cancellation closes the session",results);
            }
            Check(LoginService.LoginTimeoutMilliseconds==40000,"Default total login deadline is 40 seconds",results);
            BrowserSession.WaitForForm=true;
            r=LoginService.RunAsync(cfg,false,new Progress(),CancellationToken.None,timeoutMilliseconds:30).GetAwaiter().GetResult();
            Check(r.Failed&&!r.Completed&&r.Message.Contains("40 秒")&&BrowserSession.Closes==1,"Unrecognized form timeout closes login browser and returns a provisional failure",results);
            BrowserSession.WaitForForm=false; BrowserSession.WaitDuringStartup=true;
            r=LoginService.RunAsync(cfg,false,new Progress(),CancellationToken.None,timeoutMilliseconds:30).GetAwaiter().GetResult();
            Check(r.Failed&&BrowserSession.Closes==1,"40-second deadline also bounds browser startup",results);
            using(var cancelled=new CancellationTokenSource(20)) {
                bool stopped=false;
                try { LoginService.RunAsync(cfg,false,new Progress(),cancelled.Token,timeoutMilliseconds:500).GetAwaiter().GetResult(); } catch(OperationCanceledException) { stopped=true; }
                Check(stopped&&BrowserSession.Closes==1,"Explicit app cancel is never converted to login timeout",results);
            }
            BrowserSession.WaitDuringStartup=false; BrowserSession.CloseOnEvaluate=true;
            bool closed=false;
            try { LoginService.RunAsync(cfg,false,new Progress(),CancellationToken.None).GetAwaiter().GetResult(); } catch(InvalidOperationException) { closed=true; }
            Check(closed&&BrowserSession.Closes==1,"Closed browser cleans up before the outer flow starts website verification",results);
            BrowserSession.CloseOnEvaluate=false;
            return results.ToArray();
        }
        static void Check(bool condition,string name,List<string> results) { if(!condition)throw new Exception("FAIL: "+name); results.Add("PASS: "+name); }
    }
}
