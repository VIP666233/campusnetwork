using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet
{
	internal static class ConnectivityCheck
	{
		public const string SuccessMessage = "测试网站访问成功，网络成功登录";

		public static Task<ConnectivityResult> VerifyAsync(string url, CancellationToken token)
		{
			return VerifyAsync(url, token, () => new WebsiteBrowser());
		}

		internal static async Task<ConnectivityResult> VerifyAsync(string url, CancellationToken token, Func<IVerificationBrowser> createBrowser, Func<int, CancellationToken, Task> delay = null, int timeoutMilliseconds = 35000)
		{
			url = Settings.NormalizeVerificationUrl(url);
			token.ThrowIfCancellationRequested();
			using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
			{
				using (IVerificationBrowser browser = createBrowser())
				{
					timeout.CancelAfter(timeoutMilliseconds);
					ConnectivityResult result = null;
					Exception cancellation = null;
					try
					{
						await browser.OpenAsync(url, timeout.Token);
						result = await WaitForPageAsync(url, browser, timeout.Token, delay);
					}
					catch (OperationCanceledException ex)
					{
						if (token.IsCancellationRequested)
						{
							cancellation = ex;
						}
						else
						{
							result = NotVerified("验证网页打开超时");
						}
					}
					catch (Exception)
					{
						if (token.IsCancellationRequested)
						{
							cancellation = new OperationCanceledException(token);
						}
						else
						{
							result = NotVerified("浏览器暂未完成网页访问");
						}
					}
					try
					{
						await browser.CloseAsync();
					}
					catch
					{
					}
					if (cancellation != null)
					{
						ExceptionDispatchInfo.Capture(cancellation).Throw();
					}
					return result;
				}
			}
		}

		private static async Task<ConnectivityResult> WaitForPageAsync(string url, IVerificationBrowser browser, CancellationToken token, Func<int, CancellationToken, Task> delay)
		{
			while (true)
			{
				token.ThrowIfCancellationRequested();
				VerificationPage page = await browser.InspectAsync(token);
				if (page.BrowserError)
				{
					return NotVerified("浏览器暂未打开验证网页");
				}
				Uri actual;
				if (Uri.TryCreate(page.Url, UriKind.Absolute, out actual) && (actual.Scheme == "http" || actual.Scheme == "https"))
				{
					if (!IsExpectedUrl(new Uri(url), actual))
					{
						return NotVerified("验证网页跳转到了其他网站或认证页");
					}
					if (page.StatusCode >= 400)
					{
						return NotVerified("验证网页暂时无法访问");
					}
					if (page.Ready && page.HasContent && page.StatusCode >= 200 && page.StatusCode < 300 && (string.Equals(page.MimeType, "text/html", StringComparison.OrdinalIgnoreCase) || string.Equals(page.MimeType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase)))
					{
						break;
					}
				}
				await ((delay == null) ? Task.Delay(200, token) : delay(200, token));
			}
			ConnectivityResult connectivityResult = new ConnectivityResult();
			connectivityResult.Verified = true;
			connectivityResult.Message = SuccessFor(url);
			connectivityResult.SiteName = new Uri(url).Host;
			return connectivityResult;
		}

		internal static bool IsExpectedUrl(Uri expected, Uri actual)
		{
			if ((actual.Scheme == "http" || actual.Scheme == "https") && actual.UserInfo.Length == 0 && (expected.Scheme != "https" || actual.Scheme == "https"))
			{
				if (!actual.Host.Equals(expected.Host, StringComparison.OrdinalIgnoreCase))
				{
					if (IsCumt(expected.Host))
					{
						return IsCumt(actual.Host);
					}
					return false;
				}
				return true;
			}
			return false;
		}

		private static bool IsCumt(string host)
		{
			if (!host.Equals("cumt.edu.cn", StringComparison.OrdinalIgnoreCase))
			{
				return host.EndsWith(".cumt.edu.cn", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}

		private static string SuccessFor(string url)
		{
			return "测试网站访问成功，网络成功登录";
		}

		private static ConnectivityResult NotVerified(string reason)
		{
			ConnectivityResult connectivityResult = new ConnectivityResult();
			connectivityResult.Message = "未进行官网验证：" + reason + "。";
			return connectivityResult;
		}
	}
}
