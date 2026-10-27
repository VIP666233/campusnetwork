using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet
{
	internal sealed class LoopbackWebSocket : IDisposable
	{
		private const int MaximumMessage = 2097152;

		private TcpClient client;

		private NetworkStream stream;

		public bool IsOpen { get; private set; }

		public async Task ConnectAsync(Uri endpoint, CancellationToken token)
		{
			IPAddress address;
			if (endpoint.Scheme != "ws" || !IPAddress.TryParse(endpoint.Host, out address) || !IPAddress.IsLoopback(address) || endpoint.UserInfo.Length != 0)
			{
				throw new InvalidOperationException("浏览器调试接口必须是本机 WebSocket 地址。");
			}
			client = new TcpClient(address.AddressFamily);
			client.NoDelay = true;
			using (token.Register(delegate
			{
				client.Close();
			}))
			{
				try
				{
					token.ThrowIfCancellationRequested();
					await client.ConnectAsync(address, endpoint.Port);
					stream = client.GetStream();
					byte[] nonce = new byte[16];
					using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
					{
						randomNumberGenerator.GetBytes(nonce);
					}
					string key = Convert.ToBase64String(nonce);
					string request = "GET " + endpoint.PathAndQuery + " HTTP/1.1\r\nHost: " + endpoint.Authority + "\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: " + key + "\r\nSec-WebSocket-Version: 13\r\n\r\n";
					byte[] bytes = Encoding.ASCII.GetBytes(request);
					await stream.WriteAsync(bytes, 0, bytes.Length);
					using (MemoryStream header = new MemoryStream())
					{
						byte[] single = new byte[1];
						string reply = null;
						while (header.Length < 16384)
						{
							await ReadExactlyAsync(single, 0, 1);
							header.WriteByte(single[0]);
							if (single[0] == 10 && header.Length >= 4)
							{
								byte[] buffer = header.GetBuffer();
								int num = (int)header.Length;
								if (buffer[num - 4] == 13 && buffer[num - 3] == 10 && buffer[num - 2] == 13)
								{
									reply = Encoding.ASCII.GetString(buffer, 0, num);
									break;
								}
							}
						}
						if (reply == null)
						{
							throw new WebSocketException("浏览器接口握手响应过长。");
						}
						string[] lines = reply.Split(new string[1] { "\r\n" }, StringSplitOptions.None);
						if (!lines[0].StartsWith("HTTP/1.1 101 ") && lines[0] != "HTTP/1.1 101")
						{
							throw new WebSocketException("浏览器接口拒绝握手：" + lines[0]);
						}
						string accept = null;
						string upgrade = null;
						string connection = null;
						string[] array = lines;
						string[] array2 = array;
						foreach (string text in array2)
						{
							int num2 = text.IndexOf(':');
							if (num2 >= 0)
							{
								string text2 = text.Substring(0, num2).Trim();
								string text3 = text.Substring(num2 + 1).Trim();
								if (text2.Equals("Sec-WebSocket-Accept", StringComparison.OrdinalIgnoreCase))
								{
									accept = text3;
								}
								if (text2.Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
								{
									upgrade = text3;
								}
								if (text2.Equals("Connection", StringComparison.OrdinalIgnoreCase))
								{
									connection = text3;
								}
							}
						}
						string expected;
						using (SHA1 sHA = SHA1.Create())
						{
							expected = Convert.ToBase64String(sHA.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
						}
						if (accept != expected || !string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase) || connection == null || !Array.Exists(connection.Split(','), (string s) => s.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase)))
						{
							throw new WebSocketException("浏览器接口握手校验失败。");
						}
					}
					IsOpen = true;
				}
				catch
				{
					token.ThrowIfCancellationRequested();
					throw;
				}
			}
		}

		private async Task ReadExactlyAsync(byte[] bytes, int offset, int count)
		{
			while (count > 0)
			{
				int read = await stream.ReadAsync(bytes, offset, count);
				if (read == 0)
				{
					IsOpen = false;
					throw new WebSocketException("浏览器关闭了本机控制连接。");
				}
				offset += read;
				count -= read;
			}
		}

		private async Task SendFrameAsync(int opcode, byte[] payload)
		{
			using (MemoryStream frame = new MemoryStream())
			{
				frame.WriteByte((byte)(0x80u | (uint)opcode));
				if (payload.Length < 126)
				{
					frame.WriteByte((byte)(0x80u | (uint)payload.Length));
				}
				else if (payload.Length <= 65535)
				{
					frame.WriteByte(254);
					frame.WriteByte((byte)(payload.Length >> 8));
					frame.WriteByte((byte)payload.Length);
				}
				else
				{
					frame.WriteByte(byte.MaxValue);
					for (int num = 56; num >= 0; num -= 8)
					{
						frame.WriteByte((byte)((ulong)payload.Length >> num));
					}
				}
				byte[] mask = new byte[4];
				using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
				{
					randomNumberGenerator.GetBytes(mask);
				}
				frame.Write(mask, 0, 4);
				for (int i = 0; i < payload.Length; i++)
				{
					frame.WriteByte((byte)(payload[i] ^ mask[i % 4]));
				}
				byte[] bytes = frame.ToArray();
				await stream.WriteAsync(bytes, 0, bytes.Length);
			}
		}

		public async Task SendTextAsync(string message, CancellationToken token)
		{
			if (!IsOpen)
			{
				throw new WebSocketException("浏览器控制连接未建立。");
			}
			byte[] payload = Encoding.UTF8.GetBytes(message);
			if (payload.Length > 2097152)
			{
				throw new WebSocketException("浏览器命令过长。");
			}
			using (token.Register(delegate
			{
				client.Close();
			}))
			{
				int num = default(int);
				int num2 = num;
				int num3 = 0;
				try
				{
					token.ThrowIfCancellationRequested();
					await SendFrameAsync(1, payload);
				}
				catch (Exception innerException)
				{
					token.ThrowIfCancellationRequested();
					IsOpen = false;
					throw new WebSocketException("发送本机浏览器命令失败。", innerException);
				}
			}
		}

		public async Task<string> ReceiveTextAsync(CancellationToken token)
		{
			if (!IsOpen)
			{
				throw new WebSocketException("浏览器控制连接未建立。");
			}
			using (token.Register(delegate
			{
				client.Close();
			}))
			{
				using (MemoryStream message = new MemoryStream())
				{
					try
					{
						token.ThrowIfCancellationRequested();
						bool started = false;
						while (true)
						{
							byte[] header = new byte[2];
							await ReadExactlyAsync(header, 0, 2);
							bool final = (header[0] & 0x80) != 0;
							int opcode = header[0] & 0xF;
							if ((header[0] & 0x70u) != 0 || (header[1] & 0x80u) != 0)
							{
								throw new WebSocketException("浏览器返回了不支持的 WebSocket 帧。");
							}
							ulong length = (ulong)(int)(header[1] & 0x7Fu);
							ulong num = length;
							if ((long)num <= 127L && (long)num >= 126L)
							{
								switch (num - 126)
								{
								case 0uL:
								{
									byte[] extended = new byte[2];
									await ReadExactlyAsync(extended, 0, 2);
									length = (ulong)(extended[0] * 256 + extended[1]);
									break;
								}
								case 1uL:
								{
									byte[] extended2 = new byte[8];
									await ReadExactlyAsync(extended2, 0, 8);
									length = 0uL;
									byte[] array = extended2;
									byte[] array2 = array;
									foreach (byte b in array2)
									{
										length = (length << 8) | b;
									}
									break;
								}
								}
							}
							if (length > 2097152 || (opcode < 8 && message.Length + (long)length > 2097152))
							{
								throw new WebSocketException("浏览器返回数据过大。");
							}
							if (opcode >= 8 && (!final || length > 125))
							{
								throw new WebSocketException("浏览器控制帧无效。");
							}
							byte[] payload = new byte[(uint)length];
							await ReadExactlyAsync(payload, 0, payload.Length);
							if (opcode == 8)
							{
								IsOpen = false;
								throw new WebSocketException("浏览器关闭了本机控制连接。");
							}
							if (opcode == 9)
							{
								await SendFrameAsync(10, payload);
							}
							else if (opcode != 10)
							{
								if (opcode == 1 && !started)
								{
									started = true;
								}
								else if (opcode != 0 || !started)
								{
									throw new WebSocketException("浏览器文本消息帧顺序无效。");
								}
								message.Write(payload, 0, payload.Length);
								if (final)
								{
									break;
								}
							}
						}
						return new UTF8Encoding(false, true).GetString(message.ToArray());
					}
					catch (Exception ex)
					{
						token.ThrowIfCancellationRequested();
						IsOpen = false;
						if (ex is WebSocketException)
						{
							throw;
						}
						throw new WebSocketException("接收本机浏览器响应失败。", ex);
					}
				}
			}
		}

		public void Dispose()
		{
			IsOpen = false;
			if (client != null)
			{
				client.Close();
			}
		}
	}
}
