using System;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet
{
	internal static class ConnectionFlow
	{
		internal static async Task<ConnectionNotice> RunAsync(string provider, Func<Task<LoginResult>> login, Func<Task<ConnectivityResult>> verify, Action<string> report, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			LoginResult result;
			try
			{
				result = await login();
			}
			catch (Exception ex)
			{
				token.ThrowIfCancellationRequested();
				result = new LoginResult
				{
					Failed = true,
					Message = "认证页登录未完成：" + ex.Message
				};
			}
			token.ThrowIfCancellationRequested();
			report(result.Message);
			report("正在用浏览器打开官网验证地址，网页加载成功后将自动关闭…");
			ConnectivityResult website = await verify();
			token.ThrowIfCancellationRequested();
			return ConnectionNotice.Create(provider, result, website);
		}
	}
}
