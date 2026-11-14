using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet {
    public static class NavigationProtocolTests {
        static readonly List<string> Results = new List<string>();
        static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Results.Add("PASS: " + name); }
        static Dictionary<string, object> Page(string url, string endpoint, string type = "page") {
            return new Dictionary<string, object> { { "url", url }, { "type", type }, { "webSocketDebuggerUrl", endpoint } };
        }
        static bool Fails(Action action, string message) {
            try { action(); return false; } catch (InvalidOperationException error) { return error.Message.Contains(message); }
        }
        public static string[] RunAll() {
            Results.Clear();
            const string url = "http://9.9.9.9/";
            Check(BrowserProtocol.QuoteArgument(url) == "\"http://9.9.9.9/\"", "Portal URL is passed as a startup argument, with no blank page");
            Check(BrowserProtocol.QuoteArgument("C:\\test\\") == "\"C:\\test\\\\\"", "Windows trailing backslash quoting");
            Check(BrowserProtocol.QuoteArgument("a\"b") == "\"a\\\"b\"", "Windows embedded quote escaping");
            Check(BrowserProtocol.SelectEndpoint(new object[] { Page("about:blank", "blank"), Page(url, "portal") }, url) == "portal", "Attach to the portal instead of an unrelated blank tab");
            Check(BrowserProtocol.SelectEndpoint(new object[] { Page("edge://welcome", "welcome"), Page("http://10.0.0.1:801/eportal/", "redirect") }, url) == "redirect", "Attach to the real redirected portal");
            Check(BrowserProtocol.SelectEndpoint(new object[] { Page("about:blank", "blank") }, url) == "blank", "A leftover blank tab is still navigable");
            Check(BrowserProtocol.SelectEndpoint(new object[] { Page("http://9.9.9.9/", "worker", "service_worker"), Page("devtools://devtools", "tools") }, url) == null, "Ignore workers and developer tools");
            var eventReply = new Dictionary<string, object> { { "method", "Page.frameNavigated" } };
            Check(BrowserProtocol.ReadReply(eventReply, 2, "Page.navigate") == null, "Ignore navigation events while awaiting a command reply");
            var stale = new Dictionary<string, object> { { "id", 1 }, { "result", new Dictionary<string, object>() } };
            Check(BrowserProtocol.ReadReply(stale, 2, "Page.navigate") == null, "Ignore replies for another command");
            var frame = new Dictionary<string, object> { { "frameId", "portal-frame" } };
            var reply = new Dictionary<string, object> { { "id", 2 }, { "result", frame } };
            Check(Object.ReferenceEquals(BrowserProtocol.ReadReply(reply, 2, "Page.navigate"), frame), "Consume the acknowledged navigation reply");
            var denied = new Dictionary<string, object> { { "id", 2 }, { "error", new Dictionary<string, object> { { "message", "Navigation blocked" } } } };
            Check(Fails(() => BrowserProtocol.ReadReply(denied, 2, "Page.navigate"), "Navigation blocked"), "Do not silently ignore rejected browser commands");
            string destination = "http://9.9.9.9/?next=%2Feportal%2F&operator=cmcc";
            int commands = 0;
            Func<string, object, CancellationToken, Task<Dictionary<string, object>>> mock = (method, parameters, token) => {
                Check(method == "Page.navigate", "Navigate with the browser protocol instead of page JavaScript");
                Check((string)parameters.GetType().GetProperty("url").GetValue(parameters, null) == destination, "Navigate to the complete configured URL with its query intact");
                commands++; return Task.FromResult(frame);
            };
            BrowserProtocol.NavigateAsync(destination, mock, CancellationToken.None).GetAwaiter().GetResult();
            Check(commands == 1, "Issue one explicit navigation command");
            Check(!BrowserProtocol.NeedsBlankRecovery("about:blank", TimeSpan.FromSeconds(3)), "Do not interrupt a pending startup navigation");
            Check(!BrowserProtocol.NeedsBlankRecovery("http://10.0.0.1/login", TimeSpan.FromSeconds(15)), "Do not navigate again after a portal redirect");
            Check(BrowserProtocol.NeedsBlankRecovery("about:blank", TimeSpan.FromSeconds(10)), "Recover only a persistently blank startup tab");
            var aborted = new Dictionary<string, object> { { "frameId", "frame" }, { "errorText", "net::ERR_ABORTED" } };
            BrowserProtocol.NavigateAsync(url, (m,p,t) => Task.FromResult(aborted), CancellationToken.None).GetAwaiter().GetResult();
            Check(true, "An aborted provisional navigation proceeds to page inspection");
            var networkFailure = new Dictionary<string, object> { { "frameId", "frame" }, { "errorText", "net::ERR_CONNECTION_TIMED_OUT" } };
            Check(Fails(() => BrowserProtocol.NavigateAsync(url, (m,p,t) => Task.FromResult(networkFailure), CancellationToken.None).GetAwaiter().GetResult(), "ERR_CONNECTION_TIMED_OUT"), "Report a real connection failure instead of waiting on blank");
            Check(Fails(() => BrowserProtocol.NavigateAsync(url, (m,p,t) => Task.FromResult(new Dictionary<string, object>()), CancellationToken.None).GetAwaiter().GetResult(), "浏览器未确认"), "Do not claim an unacknowledged navigation succeeded");
            var download = new Dictionary<string, object> { { "frameId", "frame" }, { "isDownload", true } };
            Check(Fails(() => BrowserProtocol.NavigateAsync(url, (m,p,t) => Task.FromResult(download), CancellationToken.None).GetAwaiter().GetResult(), "文件下载"), "Reject a download instead of treating it as a login page");
            var inputEvents = new List<string>();
            Func<string, object, CancellationToken, Task<Dictionary<string, object>>> inputMock = (method, parameters, token) => {
                var type = (string)parameters.GetType().GetProperty("type").GetValue(parameters, null);
                inputEvents.Add(method + ":" + type);
                var key = parameters.GetType().GetProperty("key");
                if (key != null) Check((string)key.GetValue(parameters, null) == "Escape", "Dropdown dismissal never selects with Enter or arrows");
                return Task.FromResult(new Dictionary<string, object>());
            };
            BrowserProtocol.ActivateSelectAsync(30, 50, inputMock, CancellationToken.None).GetAwaiter().GetResult();
            Check(String.Join(",", inputEvents) == "Input.dispatchMouseEvent:mouseMoved,Input.dispatchMouseEvent:mousePressed,Input.dispatchMouseEvent:mouseReleased,Input.dispatchKeyEvent:keyDown,Input.dispatchKeyEvent:keyUp", "Open and dismiss dropdown in the correct order");
            inputEvents.Clear();
            Check(Fails(() => BrowserProtocol.ActivateSelectAsync(Double.NaN, 50, inputMock, CancellationToken.None).GetAwaiter().GetResult(), "位置无效"), "Invalid coordinates cannot click another control");
            Check(inputEvents.Count == 0, "Invalid activation sends no input");
            bool canceled = false;
            try { BrowserProtocol.ActivateSelectAsync(30, 50, inputMock, new CancellationToken(true)).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { canceled = true; }
            Check(canceled && inputEvents.Count == 0, "Canceled login sends no dropdown input");
            inputEvents.Clear();
            BrowserProtocol.ClickControlAsync(30, 50, inputMock, CancellationToken.None).GetAwaiter().GetResult();
            Check(String.Join(",", inputEvents) == "Input.dispatchMouseEvent:mouseMoved,Input.dispatchMouseEvent:mousePressed,Input.dispatchMouseEvent:mouseReleased", "Custom controls receive one mouse click without dismissing their menu");
            inputEvents.Clear();
            Check(Fails(() => BrowserProtocol.ClickControlAsync(Double.PositiveInfinity, 50, inputMock, CancellationToken.None).GetAwaiter().GetResult(), "位置无效"), "Custom clicks reject invalid coordinates");
            canceled = false;
            try { BrowserProtocol.ClickControlAsync(30, 50, inputMock, new CancellationToken(true)).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { canceled = true; }
            Check(canceled && inputEvents.Count == 0, "Canceled custom control clicks send no input");
            Results.Add("ALL " + Results.Count + " PROTOCOL TESTS PASSED");
            return Results.ToArray();
        }
    }
}
