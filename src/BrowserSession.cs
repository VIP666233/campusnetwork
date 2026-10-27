using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CampusNet
{
	internal sealed class BrowserSession : IDisposable
	{
		private readonly JavaScriptSerializer json = new JavaScriptSerializer();

		private readonly HttpClient http;

		private readonly string profile;

		private Process process;

		private LoopbackWebSocket socket;

		private int sequence;

		private int port;

		internal event Action<Dictionary<string, object>> ProtocolEvent;

		public static string FindBrowser()
		{
			string[] array = new string[3]
			{
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
			};
			string[] array2 = array;
			string[] array3 = array2;
			foreach (string path in array3)
			{
				string[] array4 = new string[2] { "Microsoft\\Edge\\Application\\msedge.exe", "Google\\Chrome\\Application\\chrome.exe" };
				string[] array5 = array4;
				foreach (string path2 in array5)
				{
					string text = Path.Combine(path, path2);
					if (File.Exists(text))
					{
						return text;
					}
				}
			}
			throw new InvalidOperationException("没有找到 Microsoft Edge 或 Google Chrome。请安装其中一个浏览器后重试。");
		}

		public BrowserSession(string sessionRoot = null)
		{
			profile = Path.Combine(sessionRoot ?? Settings.DirectoryPath, "Sessions", Guid.NewGuid().ToString("N"));
			http = new HttpClient(new HttpClientHandler
			{
				UseProxy = false
			});
			http.Timeout = TimeSpan.FromSeconds(3.0);
		}

		public async Task StartAsync(string url, bool visible, CancellationToken token, IProgress<string> progress = null, bool verification = false)
		{
			TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			port = ((IPEndPoint)listener.LocalEndpoint).Port;
			listener.Stop();
			Directory.CreateDirectory(profile);
			string flags = "--remote-debugging-address=127.0.0.1 --remote-debugging-port=" + port + " --user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-sync --disable-background-mode ";
			if (!visible)
			{
				flags += "--headless=new --disable-gpu ";
			}
			url = (verification ? Settings.NormalizeVerificationUrl(url) : Settings.NormalizeUrl(url));
			if (progress != null)
			{
				progress.Report("正在启动浏览器并打开认证地址…");
			}
			process = Process.Start(new ProcessStartInfo(FindBrowser(), flags + "--new-window " + BrowserProtocol.QuoteArgument(verification ? "about:blank" : url))
			{
				UseShellExecute = false,
				CreateNoWindow = true
			});
			DateTime launched = DateTime.UtcNow;
			string lastConnectError = null;
			for (int i = 0; i < 40; i++)
			{
				token.ThrowIfCancellationRequested();
				if (process.HasExited)
				{
					throw new InvalidOperationException("登录浏览器已关闭。");
				}
				string endpoint = null;
				try
				{
					string response = await http.GetStringAsync("http://127.0.0.1:" + port + "/json/list");
					object[] pages = (object[])json.DeserializeObject(response);
					endpoint = BrowserProtocol.SelectEndpoint(pages, url);
				}
				catch (HttpRequestException)
				{
				}
				catch (TaskCanceledException)
				{
					token.ThrowIfCancellationRequested();
				}
				if (endpoint != null)
				{
					if (progress != null)
					{
						progress.Report("已找到浏览器标签页，正在连接自动填写接口…");
					}
					socket = new LoopbackWebSocket();
					try
					{
						using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token))
						{
							linked.CancelAfter(5000);
							await socket.ConnectAsync(new Uri(endpoint), linked.Token);
						}
						while (!verification)
						{
							Dictionary<string, object> dictionary = (Dictionary<string, object>)(await CommandAsync("Page.getFrameTree", new { }, token))["frameTree"];
							Dictionary<string, object> root = dictionary;
							Dictionary<string, object> frame = (Dictionary<string, object>)root["frame"];
							string currentUrl = Convert.ToString(frame["url"]);
							if (currentUrl != "about:blank")
							{
								break;
							}
							if (BrowserProtocol.NeedsBlankRecovery(currentUrl, DateTime.UtcNow - launched))
							{
								if (progress != null)
								{
									progress.Report("启动标签页仍为空白，正在停止未完成加载并重新打开认证地址…");
								}
								await CommandAsync("Page.stopLoading", new { }, token);
								await BrowserProtocol.NavigateAsync(url, CommandAsync, token);
								break;
							}
							if (progress != null)
							{
								progress.Report("浏览器接口已连接，等待首次网页加载…");
							}
							await Task.Delay(500, token);
						}
						if (progress != null)
						{
							progress.Report("浏览器接口已连接，正在识别认证页…");
						}
						return;
					}
					catch (WebSocketException ex3)
					{
						lastConnectError = DescribeTransportError(ex3);
						if (progress != null)
						{
							progress.Report("浏览器标签页连接暂时断开，正在重新连接…");
						}
						socket.Dispose();
						socket = null;
					}
					catch (OperationCanceledException)
					{
						token.ThrowIfCancellationRequested();
						socket.Dispose();
						socket = null;
						lastConnectError = "连接浏览器接口超时";
					}
				}
				if ((DateTime.UtcNow - launched).TotalSeconds >= 35.0)
				{
					break;
				}
				await Task.Delay(500, token);
			}
			throw new InvalidOperationException("未能连接浏览器自动填写接口。" + ((lastConnectError == null) ? "请确认浏览器允许本机调试连接。" : lastConnectError));
		}

		private static string DescribeTransportError(Exception ex)
		{
			while (ex.InnerException != null)
			{
				ex = ex.InnerException;
			}
			return ex.GetType().Name + ": " + ex.Message;
		}

		internal async Task<Dictionary<string, object>> CommandAsync(string method, object parameters, CancellationToken token)
		{
			int num = default(int);
			int num2 = num;
			int num3 = 0;
			try
			{
				return await SendCommandAsync(method, parameters, token);
			}
			catch (OperationCanceledException)
			{
				token.ThrowIfCancellationRequested();
				throw new TimeoutException((method == "Page.navigate") ? "浏览器打开认证地址超时，请确认已经连接校园网络。" : "浏览器自动填写接口响应超时，请重试。");
			}
			catch (WebSocketException ex2)
			{
				throw new WebSocketException("浏览器控制连接断开（" + method + "）：" + DescribeTransportError(ex2), ex2);
			}
		}

		private async Task<Dictionary<string, object>> SendCommandAsync(string method, object parameters, CancellationToken token)
		{
			int id = ++sequence;
			var request = new
			{
				id = id,
				method = method,
				@params = parameters
			};
			using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token))
			{
				linked.CancelAfter((method == "Page.navigate") ? 30000 : 8000);
				await socket.SendTextAsync(json.Serialize(request), linked.Token);
				Dictionary<string, object> result;
				do
				{
					string response = await socket.ReceiveTextAsync(linked.Token);
					Dictionary<string, object> reply = (Dictionary<string, object>)json.DeserializeObject(response);
					if (!reply.ContainsKey("id") && this.ProtocolEvent != null)
					{
						this.ProtocolEvent(reply);
					}
					result = BrowserProtocol.ReadReply(reply, id, method);
				}
				while (result == null);
				return result;
			}
		}

		public async Task<Dictionary<string, object>> EvaluateAsync(string script, CancellationToken token)
		{
			int num = default(int);
			int num2 = num;
			int num3 = 0;
			Dictionary<string, object> result;
			try
			{
				result = await CommandAsync("Runtime.evaluate", new
				{
					expression = script,
					returnByValue = true,
					awaitPromise = false
				}, token);
			}
			catch (InvalidOperationException ex)
			{
				if (ex.Message.IndexOf("Execution context was destroyed", StringComparison.OrdinalIgnoreCase) < 0 && ex.Message.IndexOf("Cannot find context", StringComparison.OrdinalIgnoreCase) < 0 && ex.Message.IndexOf("Inspected target navigated", StringComparison.OrdinalIgnoreCase) < 0)
				{
					throw;
				}
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				dictionary.Add("state", "waiting");
				dictionary.Add("message", "等待认证页切换…");
				return dictionary;
			}
			if (result.ContainsKey("exceptionDetails"))
			{
				Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
				dictionary2.Add("state", "failed");
				dictionary2.Add("message", "认证页面脚本执行失败，请检查登录页或高级设置。");
				return dictionary2;
			}
			Dictionary<string, object> remote = (Dictionary<string, object>)result["result"];
			if (!remote.ContainsKey("value"))
			{
				Dictionary<string, object> dictionary3 = new Dictionary<string, object>();
				dictionary3.Add("state", "waiting");
				dictionary3.Add("message", "等待认证页加载…");
				return dictionary3;
			}
			return (Dictionary<string, object>)remote["value"];
		}

		public async Task CloseAsync()
		{
			if (socket != null && socket.IsOpen)
			{
				try
				{
					string data = json.Serialize(new
					{
						id = ++sequence,
						method = "Browser.close"
					});
					using (CancellationTokenSource timeout = new CancellationTokenSource(2000))
					{
						await socket.SendTextAsync(data, timeout.Token);
					}
				}
				catch
				{
				}
			}
			if (process == null)
			{
				return;
			}
			try
			{
				if (!process.HasExited && (socket == null || !socket.IsOpen))
				{
					process.CloseMainWindow();
				}
				if (!(await Task.Run(() => process.WaitForExit(4000))) && !process.HasExited)
				{
					process.Kill();
				}
			}
			catch
			{
			}
		}

		public void Dispose()
		{
			if (socket != null)
			{
				socket.Dispose();
			}
			http.Dispose();
			if (process != null)
			{
				process.Dispose();
			}
			try
			{
				if (Directory.Exists(profile))
				{
					Directory.Delete(profile, true);
				}
			}
			catch
			{
			}
		}
	}
}
