using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CampusNet
{
	internal static class SelfTest
	{
		private sealed class LocalPortal : IDisposable
		{
			private TcpListener listener = new TcpListener(IPAddress.Loopback, 0);

			private CancellationTokenSource stop = new CancellationTokenSource();

			private Task serving;

			public string Url { get; private set; }

			public void Start()
			{
				listener.Start();
				Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
				serving = Serve();
			}

			private async Task Serve()
			{
				while (!stop.IsCancellationRequested)
				{
					TcpClient client;
					try
					{
						client = await listener.AcceptTcpClientAsync();
					}
					catch
					{
						break;
					}
					Handle(client);
				}
			}

			private async Task Handle(TcpClient client)
			{
				try
				{
					using (client)
					{
						using (NetworkStream stream = client.GetStream())
						{
							byte[] bytes = new byte[4096];
							StringBuilder header = new StringBuilder();
							while (header.Length < 16384)
							{
								int i = await stream.ReadAsync(bytes, 0, bytes.Length);
								if (i == 0)
								{
									return;
								}
								header.Append(Encoding.ASCII.GetString(bytes, 0, i));
								if (header.ToString().Contains("\r\n\r\n"))
								{
									break;
								}
							}
							string page = "<!doctype html><html><meta charset='utf-8'><body>\n<input name='DDDDD' type='text'><input name='upass' type='password'>\n<select id='ISP_select'><option value='campus'>校园网</option><option value='cmcc'>中国移动</option><option value='unicom'>中国联通</option><option value='telecom'>中国电信</option></select>\n<button onclick='submitLogin()'>登录</button><script>\nwindow.submitCount=0;function submitLogin(){window.submitCount++;window.testUser=document.querySelector('[name=DDDDD]').value;window.testPassword=document.querySelector('[name=upass]').value;window.testProvider=document.querySelector('select').value;document.body.innerHTML='<h1>登录成功</h1><p>在线时长 1分钟</p><button>注销</button>';}\n</script></body></html>";
							byte[] body = Encoding.UTF8.GetBytes(page);
							byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
							await stream.WriteAsync(response, 0, response.Length);
							await stream.WriteAsync(body, 0, body.Length);
						}
					}
				}
				catch
				{
				}
			}

			public void Dispose()
			{
				stop.Cancel();
				listener.Stop();
				stop.Dispose();
			}
		}

		private static List<string> results = new List<string>();

		private static void Check(bool condition, string name)
		{
			if (!condition)
			{
				throw new Exception("FAIL: " + name);
			}
			results.Add("PASS: " + name);
		}

		public static void Run(string output)
		{
			Directory.CreateDirectory(output);
			try
			{
				string text = "本地测试密码'\"\\123";
				Settings settings = new Settings();
				settings.EncryptedPassword = Settings.Protect(text);
				Settings settings2 = settings;
				Check(settings2.GetPassword() == text, "DPAPI password roundtrip");
				Check(!settings2.EncryptedPassword.Contains(text), "Password is encrypted");
				Check(Settings.NormalizeUrl(" http://9.9.9.9/ ") == "http://9.9.9.9/", "Portal URL normalization");
				bool condition = false;
				try
				{
					Settings.NormalizeUrl("file:///C:/test");
				}
				catch (ArgumentException)
				{
					condition = true;
				}
				Check(condition, "Reject non-HTTP URLs");
				using (MainForm mainForm = new MainForm(false))
				{
					mainForm.CreateControl();
					foreach (Control control in mainForm.Controls)
					{
						control.CreateControl();
					}
					using (Bitmap bitmap = new Bitmap(mainForm.Width, mainForm.Height))
					{
						mainForm.DrawToBitmap(bitmap, new Rectangle(Point.Empty, mainForm.Size));
						bitmap.Save(Path.Combine(output, "ui-preview.png"));
					}
				}
				Check(File.Exists(Path.Combine(output, "ui-preview.png")), "Render settings window");
				BrowserTests(output).GetAwaiter().GetResult();
				results.Add("ALL TESTS PASSED");
			}
			catch (Exception ex2)
			{
				results.Add(ex2.ToString());
				Environment.ExitCode = 1;
			}
			File.WriteAllLines(Path.Combine(output, "test-results.txt"), results, Encoding.UTF8);
		}

		private static async Task BrowserTests(string output)
		{
			using (LocalPortal fixture = new LocalPortal())
			{
				fixture.Start();
				using (BrowserSession session = new BrowserSession(output))
				{
					using (CancellationTokenSource timeout = new CancellationTokenSource(45000))
					{
						await session.StartAsync(fixture.Url, false, timeout.Token);
						string secret = "local-test'\"\\汉字";
						string name = "test-user";
						Settings cfg = new Settings
						{
							Url = fixture.Url,
							Username = name,
							EncryptedPassword = Settings.Protect(secret),
							Provider = "中国移动"
						};
						Dictionary<string, object> response = null;
						for (int i = 0; i < 15; i++)
						{
							response = await session.EvaluateAsync(LoginService.BuildScript(cfg, secret, false), timeout.Token);
							if ((string)response["state"] != "waiting")
							{
								break;
							}
							await Task.Delay(500);
						}
						Check((string)response["state"] == "submitted", "Submit actual browser form");
						Dictionary<string, object> values = await session.EvaluateAsync("({user:window.testUser,password:window.testPassword,provider:window.testProvider,count:window.submitCount})", timeout.Token);
						Check((string)values["user"] == name && (string)values["password"] == secret, "Fill credentials with safe JSON escaping");
						Check((string)values["provider"] == "cmcc", "Select China Mobile by label / actual option value");
						Check((string)(await session.EvaluateAsync(LoginService.BuildScript(cfg, secret, true), timeout.Token))["state"] == "success", "Confirm portal success");
						await session.EvaluateAsync(LoginService.BuildScript(cfg, secret, true), timeout.Token);
						Check(Convert.ToInt32((await session.EvaluateAsync("({count:window.submitCount})", timeout.Token))["count"]) == 1, "Never submit twice while verifying");
						await session.EvaluateAsync("(function(){document.body.innerHTML='<input type=password><p>密码错误</p>';return {ok:true};})()", timeout.Token);
						Check((string)(await session.EvaluateAsync(LoginService.BuildScript(cfg, secret, true), timeout.Token))["state"] == "failed", "Recognize incorrect password");
						await session.EvaluateAsync("(function(){document.body.innerHTML='<input id=u type=text><input id=p type=password><button>登录</button>';return {ok:true};})()", timeout.Token);
						Check((string)(await session.EvaluateAsync(LoginService.BuildScript(cfg, secret, false), timeout.Token))["state"] == "blocked", "Refuse unknown operator controls");
						cfg.ProviderSelector = "[";
						Check((string)(await session.EvaluateAsync(LoginService.BuildScript(cfg, secret, false), timeout.Token))["state"] == "blocked", "Handle invalid CSS selector");
						cfg.ProviderSelector = "";
						cfg.Url = "http://9.9.9.9/";
						Check((string)(await session.EvaluateAsync(LoginService.BuildScript(cfg, secret, false), timeout.Token))["state"] == "redirected", "Discover another server without filling credentials");
					}
					await session.CloseAsync();
				}
			}
		}
	}
}
