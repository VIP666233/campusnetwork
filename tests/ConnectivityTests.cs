using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using CampusNet;

class ConnectivityTests
{
    static int count;
    sealed class FakeBrowser : IVerificationBrowser
    {
        public readonly Queue<VerificationPage> Pages = new Queue<VerificationPage>();
        public string OpenedUrl;
        public bool Closed, Disposed, FailOpen, FailClose;
        public Func<CancellationToken, Task<VerificationPage>> Inspect;
        public Task OpenAsync(string url, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); OpenedUrl = url;
            if (FailOpen) throw new InvalidOperationException("fixture startup error");
            return Task.FromResult(0);
        }
        public Task<VerificationPage> InspectAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Inspect != null ? Inspect(token) : Task.FromResult(Pages.Dequeue());
        }
        public Task CloseAsync()
        {
            Closed = true;
            if (FailClose) throw new Exception("fixture close error");
            return Task.FromResult(0);
        }
        public void Dispose() { Disposed = true; }
    }

    static VerificationPage Html(string url = Settings.DefaultVerificationUrl)
    {
        return new VerificationPage { Url = url, Ready = true, HasContent = true, StatusCode = 200, MimeType = "text/html" };
    }

    static async Task<ConnectivityResult> Run(FakeBrowser browser)
    {
        var result = await ConnectivityCheck.VerifyAsync(Settings.DefaultVerificationUrl, CancellationToken.None,
            () => browser, (_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(0); });
        Check(browser.Closed && browser.Disposed, "Verification window always closes and disposes");
        return result;
    }

    static async Task Verify()
    {
        var fake = new FakeBrowser();
        fake.Pages.Enqueue(new VerificationPage { Url = "about:blank" });
        fake.Pages.Enqueue(new VerificationPage { Url = Settings.DefaultVerificationUrl });
        fake.Pages.Enqueue(Html());
        var result = await Run(fake);
        Check(result.Verified && result.Message == ConnectivityCheck.SuccessMessage, "Wait for browser document load before success");
        Check(fake.OpenedUrl == Settings.DefaultVerificationUrl, "Browser opens the configured verification URL");

        fake = new FakeBrowser(); fake.Pages.Enqueue(Html("https://www.bilibili.com/video/BV1xx411c7mD"));
        Check((await Run(fake)).Verified, "Configured website redirect within its HTTPS host verifies");
        foreach (var url in new[] { "http://www.cumt.edu.cn/", "http://10.2.5.251/login", "https://www.cumt.edu.cn.evil.test/", "https://evilcumt.edu.cn/", "https://user:pass@www.cumt.edu.cn/" })
        {
            fake = new FakeBrowser(); fake.Pages.Enqueue(Html(url));
            result = await Run(fake);
            Check(!result.Verified && result.Message.StartsWith("未进行官网验证："), "Authentication redirect / downgrade is not website success: " + url);
        }
        foreach (int status in new[] { 403, 404, 500, 503 })
        {
            fake = new FakeBrowser(); var page = Html(); page.StatusCode = status; fake.Pages.Enqueue(page);
            Check(!(await Run(fake)).Verified, "Error page is not website success: HTTP " + status);
        }
        fake = new FakeBrowser(); fake.Pages.Enqueue(new VerificationPage { BrowserError = true });
        Check(!(await Run(fake)).Verified, "Browser network / certificate error is not success");
        foreach (string scenario in new[] { "empty", "loading", "download", "no-response" })
        {
            fake = new FakeBrowser();
            var page = Html();
            if (scenario == "empty") page.HasContent = false;
            if (scenario == "loading") page.Ready = false;
            if (scenario == "download") page.MimeType = "application/octet-stream";
            if (scenario == "no-response") page.StatusCode = 0;
            int inspected = 0;
            fake.Inspect = token => {
                inspected++;
                return Task.FromResult(inspected == 1 ? page : new VerificationPage { BrowserError = true });
            };
            result = await Run(fake);
            Check(!result.Verified && inspected == 2, "Do not verify incomplete document: " + scenario);
        }
        fake = new FakeBrowser { FailOpen = true };
        Check(!(await Run(fake)).Verified, "Startup error preserves unverified status and cleanup");

        fake = new FakeBrowser { FailClose = true }; fake.Pages.Enqueue(Html());
        Check((await Run(fake)).Verified, "Cleanup error does not erase a confirmed webpage success");

        fake = new FakeBrowser();
        fake.Inspect = async token => { await Task.Delay(5000, token); return Html(); };
        result = await ConnectivityCheck.VerifyAsync(Settings.DefaultVerificationUrl, CancellationToken.None, () => fake, timeoutMilliseconds: 30);
        Check(!result.Verified && result.Message.Contains("未进行官网验证") && fake.Closed && fake.Disposed, "Timeout closes the verification browser and marks unverified");

        using (var stop = new CancellationTokenSource())
        {
            fake = new FakeBrowser();
            fake.Inspect = token => { stop.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(Html()); };
            bool canceled = false;
            try { await ConnectivityCheck.VerifyAsync(Settings.DefaultVerificationUrl, stop.Token, () => fake); }
            catch (OperationCanceledException) { canceled = true; }
            Check(canceled && fake.Closed && fake.Disposed, "User cancellation closes browser without a success notification");
        }
        using (var stop = new CancellationTokenSource())
        {
            stop.Cancel(); bool created = false, canceled = false;
            try { await ConnectivityCheck.VerifyAsync(Settings.DefaultVerificationUrl, stop.Token, () => { created = true; return new FakeBrowser(); }); }
            catch (OperationCanceledException) { canceled = true; }
            Check(canceled && !created, "Already canceled operation never creates a browser");
        }

        const string custom = "https://example.test/check?source=campus";
        fake = new FakeBrowser(); fake.Pages.Enqueue(Html(custom));
        result = await ConnectivityCheck.VerifyAsync(custom, CancellationToken.None, () => fake);
        Check(result.Verified && fake.OpenedUrl == custom && result.SiteName == "example.test", "Custom verification address is opened and its host is retained");
        var customNotice = ConnectionNotice.Create("中国移动", new LoginResult { Submitted = true }, result);
        Check(customNotice.Detail == result.Message && customNotice.Status == "example.test官网登录成功", "Notification names the configured website");

        var json = new JavaScriptSerializer();
        var settings = json.Deserialize<Settings>("{}");
        Check(settings.VerificationUrl == Settings.DefaultVerificationUrl, "Old settings without verification address use the default");
        Check(Settings.MigrateVerificationUrl("https://www.cumt.edu.cn/") == Settings.DefaultVerificationUrl, "Previous default verification address migrates to the current default");
        settings.VerificationUrl = custom;
        Check(json.Deserialize<Settings>(json.Serialize(settings)).VerificationUrl == custom, "Custom address survives settings serialization without touching real user files");
        foreach (var invalid in new[] { "", "file:///C:/test", "javascript:alert(1)", "https://user:pass@example.test/" })
        {
            bool rejected = false;
            try { Settings.NormalizeVerificationUrl(invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Reject invalid verification address: " + invalid);
        }
        Check(Settings.NormalizeVerificationUrl(" http://9.9.9.9.com/ ") == "http://9.9.9.9.com/", "Verification URL does not use authentication URL migration");

        foreach (string provider in new[] { "校园网", "中国移动", "中国联通", "中国电信" })
        {
            var submitted = new LoginResult { Submitted = true };
            var notice = ConnectionNotice.Create(provider, submitted, new ConnectivityResult { Verified = true, Message = ConnectivityCheck.SuccessMessage, SiteName = "www.bilibili.com" });
            Check(notice.Title == provider + "登录成功" && notice.Verified && notice.Detail == ConnectivityCheck.SuccessMessage && notice.Status == "www.bilibili.com官网登录成功", provider + ": website success notifies with website name");
            notice = ConnectionNotice.Create(provider, submitted, new ConnectivityResult());
            Check(notice.Title.Contains(provider) && !notice.Verified && notice.Status == "未进行官网验证" && notice.Detail.Contains("待确认"), provider + ": completed submission still notifies with unverified mark");
            notice = ConnectionNotice.Create(provider, new LoginResult { Confirmed = true }, new ConnectivityResult());
            Check(notice.Title == provider + "登录成功" && !notice.Verified && notice.Status == "未进行官网验证", provider + ": portal success remains success when website unavailable");
            foreach (string trigger in new[] { "timeout", "browser-closed", "portal-failed" })
            {
                foreach(bool reachable in new[] { true, false })
                {
                    int logins=0, verifications=0;
                    var messages=new List<string>();
                    notice = await ConnectionFlow.RunAsync(provider,
                        () => {
                            logins++;
                            if(trigger=="browser-closed")throw new InvalidOperationException("fixture browser closed");
                            return Task.FromResult(new LoginResult { Failed=true, Submitted=trigger=="portal-failed", Message=trigger });
                        },
                        () => { verifications++; Check(logins==1,"Website verification follows login: "+trigger); return Task.FromResult(new ConnectivityResult { Verified=reachable, Message=reachable?ConnectivityCheck.SuccessMessage:"未进行官网验证：fixture unavailable", SiteName=reachable?"www.bilibili.com":null }); },
                        s=>messages.Add(s), CancellationToken.None);
                    Check(verifications==1&&messages.Count==2,"Always verify once after "+trigger+" for "+provider);
                    if(reachable)
                        Check(notice.Verified&&!notice.Failed&&notice.Title=="当前已经连接校园网"&&notice.Status=="www.bilibili.com官网登录成功 · "+provider&&notice.Detail.Contains("请勿重复点击"),"Reachable website recovers "+trigger+" for "+provider);
                    else
                        Check(notice.Failed&&!notice.Verified&&notice.Title==provider+"连接网络失败"&&notice.Status=="连接网络失败","Unreachable website resolves "+trigger+" to connection failure for "+provider);
                }
            }
        }

        using(var stop=new CancellationTokenSource())
        {
            int verifications=0; bool canceled=false;
            try {
                await ConnectionFlow.RunAsync("校园网", () => { stop.Cancel(); throw new OperationCanceledException(stop.Token); },
                    () => { verifications++; return Task.FromResult(new ConnectivityResult { Verified=true }); }, s=>{},stop.Token);
            } catch(OperationCanceledException) { canceled=true; }
            Check(canceled&&verifications==0,"Cancel button stops without opening a verification browser");
        }
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false;
            try {
                await ConnectionFlow.RunAsync("校园网", () => Task.FromResult(new LoginResult { Failed=true }),
                    () => { stop.Cancel(); return Task.FromResult(new ConnectivityResult { Verified=true }); }, s=>{},stop.Token);
            } catch(OperationCanceledException) { canceled=true; }
            Check(canceled,"Cancellation during verification never produces a final notification");
        }
    }

    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label); count++;
    }
    static void Main()
    {
        try { Verify().GetAwaiter().GetResult(); Console.WriteLine("ALL " + count + " CHECKS PASSED (simulated browser only)"); }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}
