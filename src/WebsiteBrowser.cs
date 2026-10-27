using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet
{
	internal sealed class WebsiteBrowser : IVerificationBrowser, IDisposable
	{
		private readonly BrowserSession session;

		private readonly Dictionary<string, VerificationPage> documents = new Dictionary<string, VerificationPage>();

		public WebsiteBrowser(string sessionRoot = null)
		{
			session = new BrowserSession(sessionRoot);
		}

		public async Task OpenAsync(string url, CancellationToken token)
		{
			await session.StartAsync(url, true, token, null, true);
			session.ProtocolEvent += Observe;
			await session.CommandAsync("Page.enable", new { }, token);
			await session.CommandAsync("Network.enable", new { }, token);
			Dictionary<string, object> navigation = await session.CommandAsync("Page.navigate", new { url }, token);
			if (navigation.ContainsKey("isDownload") && Convert.ToBoolean(navigation["isDownload"]))
			{
				throw new InvalidOperationException("验证地址返回下载。");
			}
			if (navigation.ContainsKey("errorText") && !string.IsNullOrEmpty(Convert.ToString(navigation["errorText"])) && Convert.ToString(navigation["errorText"]) != "net::ERR_ABORTED")
			{
				throw new InvalidOperationException("验证网页未能打开。");
			}
		}

		private void Observe(Dictionary<string, object> message)
		{
			if (message.ContainsKey("method") && !(Convert.ToString(message["method"]) != "Network.responseReceived"))
			{
				Dictionary<string, object> dictionary = (Dictionary<string, object>)message["params"];
				if (dictionary.ContainsKey("type") && !(Convert.ToString(dictionary["type"]) != "Document") && dictionary.ContainsKey("frameId"))
				{
					Dictionary<string, object> dictionary2 = (Dictionary<string, object>)dictionary["response"];
					documents[Convert.ToString(dictionary["frameId"])] = new VerificationPage
					{
						Url = Convert.ToString(dictionary2["url"]),
						StatusCode = Convert.ToInt32(dictionary2["status"]),
						MimeType = Convert.ToString(dictionary2["mimeType"])
					};
				}
			}
		}

		public async Task<VerificationPage> InspectAsync(CancellationToken token)
		{
			Dictionary<string, object> dictionary = (Dictionary<string, object>)(await session.CommandAsync("Page.getFrameTree", new { }, token))["frameTree"];
			Dictionary<string, object> tree = dictionary;
			Dictionary<string, object> frame = (Dictionary<string, object>)tree["frame"];
			string frameUrl = Convert.ToString(frame["url"]);
			if ((frame.ContainsKey("unreachableUrl") && !string.IsNullOrEmpty(Convert.ToString(frame["unreachableUrl"]))) || frameUrl.StartsWith("chrome-error:", StringComparison.OrdinalIgnoreCase) || frameUrl.StartsWith("edge-error:", StringComparison.OrdinalIgnoreCase))
			{
				return new VerificationPage
				{
					BrowserError = true
				};
			}
			Dictionary<string, object> document = await session.EvaluateAsync("(function () {\n                return { url: location.href, ready: document.readyState === 'interactive' || document.readyState === 'complete',\n                    content: !!document.body && (document.body.innerText.trim().length > 0 ||\n                        !!document.body.querySelector('img,svg,canvas,video,main,article')) };\n            })()", token);
			string url = (document.ContainsKey("url") ? Convert.ToString(document["url"]) : "about:blank");
			VerificationPage response;
			bool matches = documents.TryGetValue(Convert.ToString(frame["id"]), out response) && SameDocument(response.Url, url) && SameDocument(frameUrl, url);
			return new VerificationPage
			{
				Url = url,
				Ready = (document.ContainsKey("ready") && Convert.ToBoolean(document["ready"])),
				HasContent = (document.ContainsKey("content") && Convert.ToBoolean(document["content"])),
				StatusCode = (matches ? response.StatusCode : 0),
				MimeType = (matches ? response.MimeType : null)
			};
		}

		private static bool SameDocument(string first, string second)
		{
			Uri result;
			Uri result2;
			if (Uri.TryCreate(first, UriKind.Absolute, out result) && Uri.TryCreate(second, UriKind.Absolute, out result2))
			{
				return Uri.Compare(result, result2, UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped, StringComparison.Ordinal) == 0;
			}
			return false;
		}

		public Task CloseAsync()
		{
			return session.CloseAsync();
		}

		public void Dispose()
		{
			session.Dispose();
		}
	}
}
