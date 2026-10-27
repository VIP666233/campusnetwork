using System;

namespace CampusNet
{
	public sealed class PortalRedirect
	{
		private string pending;

		private DateTime since;

		private int transitions;

		public string CurrentUrl { get; private set; }

		public PortalRedirect(string url)
		{
			CurrentUrl = Settings.NormalizeUrl(url);
		}

		public bool Observe(string url, DateTime now)
		{
			url = Settings.NormalizeUrl(url);
			Uri uri = new Uri(CurrentUrl);
			Uri uri2 = new Uri(url);
			if (uri.Scheme == "https" && uri2.Scheme == "http")
			{
				throw new InvalidOperationException("认证地址从 HTTPS 跳转到了 HTTP。请核对学校认证地址。");
			}
			if (pending != url)
			{
				pending = url;
				since = now;
				return false;
			}
			if ((now - since).TotalSeconds < 3.0)
			{
				return false;
			}
			if (++transitions > 4)
			{
				throw new InvalidOperationException("认证页反复跳转，请检查认证地址。");
			}
			CurrentUrl = url;
			ResetPending();
			return true;
		}

		public void ResetPending()
		{
			pending = null;
		}
	}
}
