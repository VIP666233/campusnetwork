using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CampusNet
{
	public static class LoginService
	{
		public const int LoginTimeoutMilliseconds = 40000;

		public static string BuildScript(Settings cfg, string password, bool verify, string portalUrl = null)
		{
			string text;
			using (StreamReader streamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Portal.js")))
			{
				text = streamReader.ReadToEnd();
			}
			string newValue = new JavaScriptSerializer().Serialize(new
			{
				origin = new Uri(portalUrl ?? cfg.Url).GetLeftPart(UriPartial.Authority),
				username = cfg.Username,
				password = password,
				provider = cfg.Provider,
				usernameSelector = cfg.UsernameSelector,
				passwordSelector = cfg.PasswordSelector,
				providerSelector = cfg.ProviderSelector,
				providerValue = cfg.ProviderValue,
				loginSelector = cfg.LoginSelector,
				accountSuffix = cfg.AccountSuffix,
				phase = (verify ? "verify" : "submit")
			});
			return text.Replace("__CONFIG__", newValue);
		}

		public static async Task<LoginResult> RunAsync(Settings cfg, bool showBrowser, IProgress<string> progress, CancellationToken token, string sessionRoot = null, int timeoutMilliseconds = 40000)
		{
			using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
			{
				timeout.CancelAfter(timeoutMilliseconds);
				try
				{
					return await RunCoreAsync(cfg, showBrowser, progress, timeout.Token, sessionRoot);
				}
				catch (OperationCanceledException)
				{
					token.ThrowIfCancellationRequested();
					LoginResult loginResult = new LoginResult();
					loginResult.Failed = true;
					loginResult.Message = "40 秒内未能确认校园网登录状态，将进行官网验证。";
					return loginResult;
				}
			}
		}

		private static async Task<LoginResult> RunCoreAsync(Settings cfg, bool showBrowser, IProgress<string> progress, CancellationToken token, string sessionRoot)
		{
			string password = cfg.GetPassword();
			if (string.IsNullOrWhiteSpace(cfg.Username) || string.IsNullOrEmpty(password))
			{
				throw new InvalidOperationException("请先填写并保存账号和密码。");
			}
			bool submitted = false;
			PortalRedirect redirect = new PortalRedirect(cfg.Url);
			using (BrowserSession session = new BrowserSession(sessionRoot))
			{
				LoginResult outcome = null;
				Exception failure = null;
				try
				{
					progress.Report("正在启动登录浏览器…");
					await session.StartAsync(cfg.Url, showBrowser, token, progress);
					DateTime deadline = DateTime.UtcNow.AddSeconds(40.0);
					string script = null, scriptUrl = null;
					bool scriptVerify = false;
					string previousAdapter = null;
					while (DateTime.UtcNow < deadline)
					{
						token.ThrowIfCancellationRequested();
						if (script == null || scriptUrl != redirect.CurrentUrl || scriptVerify != submitted)
						{
							script = BuildScript(cfg, password, submitted, redirect.CurrentUrl);
							scriptUrl = redirect.CurrentUrl;
							scriptVerify = submitted;
						}
						Dictionary<string, object> state = await session.EvaluateAsync(script, token);
						string kind = (string)state["state"];
						string message = (string)state["message"];
						if (state.ContainsKey("adapter") && kind != "redirected")
						{
							string adapter = (string)state["adapter"];
							if (adapter != previousAdapter)
							{
								progress.Report("自动适配：" + (adapter == "huadian" ? "华电认证表单" : adapter == "enhanced" ? "图片按钮 / 内嵌表单" : "通用认证表单"));
								previousAdapter = adapter;
							}
						}
						progress.Report(message);
						if (kind == "redirected")
						{
							if (redirect.Observe((string)state["redirectUrl"], DateTime.UtcNow))
							{
								progress.Report("已找到学校认证页，正在继续登录，无需再次点击…");
							}
						}
						else
						{
							redirect.ResetPending();
						}
						switch (kind)
						{
						case "success":
							outcome = new LoginResult
							{
								Confirmed = true,
								Submitted = submitted,
								Message = message
							};
							break;
						case "failed":
						case "blocked":
							outcome = new LoginResult
							{
								Failed = true,
								Submitted = submitted,
								Message = message
							};
							break;
						case "submitted":
							if (!submitted)
							{
								submitted = true;
								DateTime dateTime = DateTime.UtcNow.AddSeconds(25.0);
								if (dateTime < deadline)
								{
									deadline = dateTime;
								}
							}
							goto default;
						default:
							await Task.Delay(submitted ? 200 : 1500, token);
							continue;
						}
						break;
					}
					if (outcome == null)
					{
						outcome = new LoginResult
						{
							Submitted = submitted,
							Failed = !submitted,
							Message = (submitted ? "登录请求已提交，认证页面未提供可识别的成功标志；将进行官网验证。" : "40 秒内未能打开并识别认证表单，将进行官网验证。")
						};
					}
					outcome.PortalUrl = redirect.CurrentUrl;
				}
				catch (Exception ex)
				{
					failure = ex;
				}
				await session.CloseAsync();
				if (failure != null)
				{
					ExceptionDispatchInfo.Capture(failure).Throw();
				}
				return outcome;
			}
		}
	}
}
