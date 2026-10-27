using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CampusNet
{
	public sealed class Settings
	{
		private const string StartupName = "CampusNetLoginHelper";

		public const string DefaultUrl = "http://9.9.9.9.com/";

		public const string DefaultVerificationUrl = "https://www.bilibili.com/";

		private const string PreviousDefaultVerificationUrl = "https://www.cumt.edu.cn/";

		public string Url = "http://9.9.9.9.com/";

		public string VerificationUrl = "https://www.bilibili.com/";

		public string Username = "";

		public string EncryptedPassword = "";

		public string Provider = "校园网";

		public bool Startup;

		public bool ShowBrowserOnStartup;

		public int StartupDelay = 3;

		public string UsernameSelector = "";

		public string PasswordSelector = "";

		public string ProviderSelector = "";

		public string ProviderValue = "";

		public string LoginSelector = "";

		public string AccountSuffix = "";

		public static string DirectoryPath
		{
			get
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CampusNetLoginHelper");
			}
		}

		public static string FilePath
		{
			get
			{
				return Path.Combine(DirectoryPath, "settings.json");
			}
		}

		public static string Protect(string value)
		{
			if (value.Length != 0)
			{
				return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
			}
			return "";
		}

		public string GetPassword()
		{
			if (string.IsNullOrEmpty(EncryptedPassword))
			{
				return "";
			}
			return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(EncryptedPassword), null, DataProtectionScope.CurrentUser));
		}

		public static Settings Load()
		{
			if (!File.Exists(FilePath))
			{
				return new Settings();
			}
			Settings settings = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new Settings();
			settings.VerificationUrl = MigrateVerificationUrl(settings.VerificationUrl);
			if (string.Equals(settings.Url, "http://9.9.9.9/", StringComparison.OrdinalIgnoreCase) || string.Equals(settings.Url, "http://9.9.9.9", StringComparison.OrdinalIgnoreCase) || string.Equals(settings.Url, "http://10.2.5.251/a79.htm", StringComparison.OrdinalIgnoreCase))
			{
				settings.Url = "http://9.9.9.9.com/";
			}
			return settings;
		}

		public void Save()
		{
			Directory.CreateDirectory(DirectoryPath);
			string text = FilePath + ".new";
			File.WriteAllText(text, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
			if (File.Exists(FilePath))
			{
				File.Replace(text, FilePath, null);
			}
			else
			{
				File.Move(text, FilePath);
			}
		}

		public static bool HasStartup()
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
			{
				return registryKey != null && registryKey.GetValue("CampusNetLoginHelper") != null;
			}
		}

		public void ApplyStartup()
		{
			using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
			{
				if (Startup)
				{
					registryKey.SetValue("CampusNetLoginHelper", "\"" + Application.ExecutablePath + "\" --auto");
				}
				else
				{
					registryKey.DeleteValue("CampusNetLoginHelper", false);
				}
			}
		}

		public static string NormalizeVerificationUrl(string value)
		{
			Uri result;
			if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out result) || (result.Scheme != "http" && result.Scheme != "https") || result.UserInfo.Length > 0)
			{
				throw new ArgumentException("请输入完整的 http:// 或 https:// 官网验证地址，网址中不能包含账号密码。");
			}
			return result.AbsoluteUri;
		}

		internal static string MigrateVerificationUrl(string value)
		{
			if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, "https://www.cumt.edu.cn/", StringComparison.OrdinalIgnoreCase))
			{
				return value;
			}
			return "https://www.bilibili.com/";
		}

		public static string NormalizeUrl(string value)
		{
			Uri result;
			if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out result) || (result.Scheme != "http" && result.Scheme != "https") || result.UserInfo.Length > 0)
			{
				throw new ArgumentException("请输入完整的 http:// 或 https:// 认证地址。");
			}
			if (string.Equals(result.Host, "9.9.9.9.com", StringComparison.OrdinalIgnoreCase))
			{
				return "http://9.9.9.9/";
			}
			return result.AbsoluteUri;
		}
	}
}
