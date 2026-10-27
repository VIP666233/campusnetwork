using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet
{
	public static class NetworkReady
	{
		public static bool IsUsable(OperationalStatus status, NetworkInterfaceType type, IPAddress[] addresses, IPAddress[] gateways)
		{
			if (status == OperationalStatus.Up && type != NetworkInterfaceType.Loopback && type != NetworkInterfaceType.Tunnel && addresses.Any(IsIPv4))
			{
				return gateways.Any(IsIPv4);
			}
			return false;
		}

		private static bool IsIPv4(IPAddress ip)
		{
			if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any))
			{
				return false;
			}
			byte[] addressBytes = ip.GetAddressBytes();
			if (addressBytes[0] == 169)
			{
				return addressBytes[1] != 254;
			}
			return true;
		}

		public static bool IsReady()
		{
			try
			{
				NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				foreach (NetworkInterface networkInterface in allNetworkInterfaces)
				{
					IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
					if (IsUsable(networkInterface.OperationalStatus, networkInterface.NetworkInterfaceType, iPProperties.UnicastAddresses.Select((UnicastIPAddressInformation a) => a.Address).ToArray(), iPProperties.GatewayAddresses.Select((GatewayIPAddressInformation a) => a.Address).ToArray()))
					{
						return true;
					}
				}
			}
			catch (NetworkInformationException)
			{
			}
			return false;
		}

		public static async Task WaitAsync(int settleSeconds, CancellationToken token, Action<string> report, Func<bool> ready = null, Func<int, CancellationToken, Task> pause = null)
		{
			ready = ready ?? new Func<bool>(IsReady);
			pause = pause ?? new Func<int, CancellationToken, Task>(Task.Delay);
			bool stable;
			do
			{
				token.ThrowIfCancellationRequested();
				report("正在等待有线网络或 Wi-Fi 获得有效地址…");
				while (!ready())
				{
					await pause(1000, token);
					token.ThrowIfCancellationRequested();
				}
				report("已检测到网络连接，正在等待网络稳定…");
				stable = true;
				for (int i = 0; i < Math.Max(1, settleSeconds); i++)
				{
					await pause(1000, token);
					token.ThrowIfCancellationRequested();
					if (!ready())
					{
						stable = false;
						break;
					}
				}
			}
			while (!stable);
			report("网络已就绪，开始自动登录…");
		}
	}
}
