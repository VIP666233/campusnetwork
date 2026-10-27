using System;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace CampusNet
{
	internal static class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			if (args.Length > 0 && args[0] == "--self-test")
			{
				SelfTest.Run((args.Length > 1) ? args[1] : Application.StartupPath);
				return;
			}
			bool createdNew;
			using (Mutex mutex = new Mutex(true, "Local\\CampusNetLoginHelper-" + WindowsIdentity.GetCurrent().User.Value, out createdNew))
			{
				if (!createdNew)
				{
					if (args.Length == 0)
					{
						MessageBox.Show("程序已在运行。请双击通知区域中的校园网图标打开窗口。", "校园网自动登录");
					}
					return;
				}
				try
				{
					Application.Run(new MainForm(Array.IndexOf(args, "--auto") >= 0));
				}
				finally
				{
					mutex.ReleaseMutex();
				}
			}
		}
	}
}
