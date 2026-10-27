using System;

namespace CampusNet
{
	internal sealed class ConnectionNotice
	{
		public string Title;

		public string Status;

		public string Detail;

		public bool Verified;

		public bool Failed;

		public static ConnectionNotice Create(string provider, LoginResult login, ConnectivityResult website)
		{
			string text = (string.IsNullOrWhiteSpace(website.SiteName) ? new Uri("https://www.bilibili.com/").Host : website.SiteName);
			if (!login.Completed)
			{
				if (website.Verified)
				{
					ConnectionNotice connectionNotice = new ConnectionNotice();
					connectionNotice.Title = "当前已经连接校园网";
					connectionNotice.Status = text + "官网登录成功 · " + provider;
					connectionNotice.Detail = "当前已经连接校园网，请勿重复点击。\n" + (website.Message ?? "测试网站访问成功，网络成功登录");
					connectionNotice.Verified = true;
					return connectionNotice;
				}
				ConnectionNotice connectionNotice2 = new ConnectionNotice();
				connectionNotice2.Title = provider + "连接网络失败";
				connectionNotice2.Status = "连接网络失败";
				connectionNotice2.Detail = "认证页未确认登录，官网也无法访问。请检查校园网连接、账号密码和验证地址后重试。";
				connectionNotice2.Failed = true;
				return connectionNotice2;
			}
			if (website.Verified)
			{
				ConnectionNotice connectionNotice3 = new ConnectionNotice();
				connectionNotice3.Title = provider + "登录成功";
				connectionNotice3.Status = text + "官网登录成功";
				connectionNotice3.Detail = website.Message ?? "测试网站访问成功，网络成功登录";
				connectionNotice3.Verified = true;
				return connectionNotice3;
			}
			ConnectionNotice connectionNotice4 = new ConnectionNotice();
			connectionNotice4.Title = provider + (login.Confirmed ? "登录成功" : "连接流程已完成");
			connectionNotice4.Status = "未进行官网验证";
			connectionNotice4.Detail = (login.Confirmed ? "认证页显示已登录，官网未进行验证。" : "登录请求已提交，联网状态待确认，官网未进行验证。");
			return connectionNotice4;
		}
	}
}
