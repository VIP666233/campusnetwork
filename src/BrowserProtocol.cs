using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet
{
	internal static class BrowserProtocol
	{
		public static string QuoteArgument(string value)
		{
			StringBuilder stringBuilder = new StringBuilder("\"");
			int num = 0;
			foreach (char c in value)
			{
				switch (c)
				{
				case '\\':
					num++;
					continue;
				case '"':
					stringBuilder.Append('\\', num * 2 + 1);
					break;
				default:
					stringBuilder.Append('\\', num);
					break;
				}
				stringBuilder.Append(c);
				num = 0;
			}
			stringBuilder.Append('\\', num * 2).Append('"');
			return stringBuilder.ToString();
		}

		public static string SelectEndpoint(object[] pages, string url)
		{
			string result = null;
			int num = -1;
			Uri uri = new Uri(url);
			foreach (object obj in pages)
			{
				Dictionary<string, object> dictionary = obj as Dictionary<string, object>;
				if (dictionary == null || !dictionary.ContainsKey("type") || (string)dictionary["type"] != "page" || !dictionary.ContainsKey("webSocketDebuggerUrl"))
				{
					continue;
				}
				string text = (dictionary.ContainsKey("url") ? Convert.ToString(dictionary["url"]) : "");
				if (!text.StartsWith("devtools:", StringComparison.OrdinalIgnoreCase))
				{
					int num2 = 0;
					Uri result2;
					if (Uri.TryCreate(text, UriKind.Absolute, out result2) && (result2.Scheme == "http" || result2.Scheme == "https"))
					{
						num2 = ((result2.Host == uri.Host) ? 30 : 20);
					}
					if (text == "about:blank")
					{
						num2 = 10;
					}
					if (text == url)
					{
						num2 = 40;
					}
					if (num2 > num)
					{
						num = num2;
						result = Convert.ToString(dictionary["webSocketDebuggerUrl"]);
					}
				}
			}
			return result;
		}

		public static Dictionary<string, object> ReadReply(Dictionary<string, object> reply, int id, string method)
		{
			if (!reply.ContainsKey("id") || Convert.ToInt32(reply["id"]) != id)
			{
				return null;
			}
			if (reply.ContainsKey("error"))
			{
				Dictionary<string, object> dictionary = (Dictionary<string, object>)reply["error"];
				throw new InvalidOperationException("浏览器命令 " + method + " 失败：" + (dictionary.ContainsKey("message") ? Convert.ToString(dictionary["message"]) : "未知协议错误"));
			}
			if (!reply.ContainsKey("result"))
			{
				throw new InvalidOperationException("浏览器没有返回 " + method + " 的执行结果。");
			}
			return (Dictionary<string, object>)reply["result"];
		}

		public static async Task NavigateAsync(string url, Func<string, object, CancellationToken, Task<Dictionary<string, object>>> command, CancellationToken token)
		{
			Dictionary<string, object> result = await command("Page.navigate", new { url }, token);
			if (!result.ContainsKey("errorText") || !(Convert.ToString(result["errorText"]) == "net::ERR_ABORTED"))
			{
				if (result.ContainsKey("errorText") && !string.IsNullOrEmpty(Convert.ToString(result["errorText"])))
				{
					throw new InvalidOperationException("无法打开认证地址：" + Convert.ToString(result["errorText"]) + "。请连接校园网络后重试。");
				}
				if (result.ContainsKey("isDownload") && Convert.ToBoolean(result["isDownload"]))
				{
					throw new InvalidOperationException("认证地址返回了文件下载，请改为学校登录页面地址。");
				}
				if (!result.ContainsKey("frameId") || string.IsNullOrEmpty(Convert.ToString(result["frameId"])))
				{
					throw new InvalidOperationException("浏览器未确认打开认证地址，请重试。");
				}
			}
		}

		public static bool NeedsBlankRecovery(string currentUrl, TimeSpan elapsed)
		{
			if (currentUrl == "about:blank")
			{
				return elapsed.TotalSeconds >= 10.0;
			}
			return false;
		}
	}
}
