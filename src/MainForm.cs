using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CampusNet
{
	public sealed class MainForm : Form
	{
		private TextBox address;

		private TextBox username;

		private TextBox password;

		private TextBox userSelector;

		private TextBox passwordSelector;

		private TextBox providerSelector;

		private TextBox providerValue;

		private TextBox loginSelector;

		private TextBox suffix;

		private TextBox verificationAddress;

		private TextBox log;

		private ComboBox provider;

		private CheckBox startup;

		private CheckBox showBrowser;

		private CheckBox showPassword;

		private NumericUpDown delay;

		private Button save;

		private Button login;

		private Button cancel;

		private Button open;

		private Label state;

		private NotifyIcon tray;

		private LoginNotification notification;

		private Icon trayIcon;

		private Bitmap logoImage;

		private CancellationTokenSource running;

		private readonly bool automatic;

		private bool closeWhenFinished;

		private bool trayTipShown;

		private string lastMessage;

		private readonly SettingsChanges changes = new SettingsChanges();

		private bool closeApproved;

		private bool exitRequested;

		public MainForm(bool auto)
		{
			automatic = auto;
			Build();
			ReadSettings();
			if (auto)
			{
				base.ShowInTaskbar = false;
				base.Opacity = 0.0;
			}
			base.Shown += async delegate
			{
				if (automatic)
				{
					MinimizeToTray();
					if (startup.Checked)
					{
						await RunLogin(true);
					}
				}
			};
			base.FormClosing += delegate(object sender, FormClosingEventArgs e)
			{
				if (!exitRequested)
				{
					if (!closeApproved && !ConfirmSettings("隐藏到通知区域"))
					{
						e.Cancel = true;
					}
					else
					{
						e.Cancel = true;
						MinimizeToTray();
					}
				}
				else if (!closeApproved && !ConfirmSettings("退出程序"))
				{
					e.Cancel = true;
				}
				else if (running != null)
				{
					e.Cancel = true;
					closeApproved = true;
					closeWhenFinished = true;
					running.Cancel();
					SetState("正在结束登录…", false);
				}
			};
			base.Resize += delegate
			{
				if (base.WindowState == FormWindowState.Minimized)
				{
					MinimizeToTray();
					if (!trayTipShown)
					{
						tray.ShowBalloonTip(2500, "校园网自动登录", "双击通知区域图标可打开设置。", ToolTipIcon.Info);
						trayTipShown = true;
					}
				}
			};
		}

		private void Build()
		{
			Text = "校园网自动登录 v1.9.1（基于 v1.3）";
			base.ClientSize = new Size(744, 700);
			base.StartPosition = FormStartPosition.CenterScreen;
			Size size2 = (MaximumSize = base.Size);
			Size minimumSize = size2;
			MinimumSize = minimumSize;
			base.MaximizeBox = false;
			BackColor = Color.FromArgb(246, 248, 250);
			Font = new Font("Microsoft YaHei UI", 9f);
			base.AutoScaleMode = AutoScaleMode.None;
			Panel panel = new Panel();
			panel.Dock = DockStyle.Top;
			panel.Height = 90;
			panel.BackColor = Color.FromArgb(37, 161, 244);
			Panel panel2 = panel;
			base.Controls.Add(panel2);
			logoImage = AppArtwork.LoadLogo();
			panel2.Controls.Add(new PictureBox
			{
				Image = logoImage,
				SizeMode = PictureBoxSizeMode.Zoom,
				Location = new Point(23, 17),
				Size = new Size(58, 58),
				AccessibleName = "校园网自动登录 Logo",
				TabStop = false
			});
			panel2.Controls.Add(new Label
			{
				Text = "校园网自动登录",
				Font = new Font(Font.FontFamily, 20f, FontStyle.Bold),
				ForeColor = Color.White,
				AutoSize = true,
				Location = new Point(91, 17)
			});
			panel2.Controls.Add(new Label
			{
				Text = "登录 Windows 后自动连接校园网",
				ForeColor = Color.FromArgb(232, 245, 255),
				AutoSize = true,
				Location = new Point(94, 58)
			});
			panel2.Controls.Add(new Label
			{
				Text = "Made by VIP666233",
				ForeColor = Color.FromArgb(232, 245, 255),
				AutoSize = true,
				Location = new Point(570, 62)
			});
			TabControl tabControl = new TabControl();
			tabControl.Location = new Point(24, 108);
			tabControl.Size = new Size(696, 377);
			TabControl tabControl2 = tabControl;
			TabPage tabPage = new TabPage("登录设置");
			tabPage.BackColor = Color.White;
			TabPage tabPage2 = tabPage;
			TabPage tabPage3 = new TabPage("高级设置");
			tabPage3.BackColor = Color.White;
			TabPage value = tabPage3;
			tabControl2.TabPages.Add(tabPage2);
			tabControl2.TabPages.Add(value);
			base.Controls.Add(tabControl2);
			address = Field(tabPage2, "认证地址", 25, 26, 490);
			address.Text = "http://9.9.9.9.com/";
			username = Field(tabPage2, "账号", 25, 72, 490);
			password = Field(tabPage2, "密码", 25, 118, 380);
			password.UseSystemPasswordChar = true;
			showPassword = new CheckBox
			{
				Text = "显示密码",
				AutoSize = true,
				Location = new Point(524, 122)
			};
			tabPage2.Controls.Add(showPassword);
			showPassword.CheckedChanged += delegate
			{
				password.UseSystemPasswordChar = !showPassword.Checked;
			};
			LabelAt(tabPage2, "运营商", 25, 167);
			provider = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Location = new Point(127, 162),
				Size = new Size(490, 28)
			};
			provider.Items.AddRange(new object[4] { "校园网", "中国移动", "中国联通", "中国电信" });
			provider.SelectedIndex = 0;
			tabPage2.Controls.Add(provider);
			startup = new CheckBox
			{
				Text = "开机自动登录（进入 Windows 桌面后）",
				AutoSize = true,
				Location = new Point(28, 211)
			};
			tabPage2.Controls.Add(startup);
			LabelAt(tabPage2, "启动等待", 25, 255);
			delay = new NumericUpDown
			{
				Minimum = 0m,
				Maximum = 180m,
				Value = 3m,
				Location = new Point(127, 250),
				Size = new Size(70, 27)
			};
			tabPage2.Controls.Add(delay);
			LabelAt(tabPage2, "秒", 209, 255);
			showBrowser = new CheckBox
			{
				Text = "自动登录时显示浏览器窗口",
				AutoSize = true,
				Location = new Point(284, 252)
			};
			tabPage2.Controls.Add(showBrowser);
			LabelAt(tabPage2, "密码由当前 Windows 用户加密保存。测试登录会显示浏览器。", 28, 305).ForeColor = Color.FromArgb(96, 110, 122);
			verificationAddress = Field(value, "官网验证地址", 24, 18, 460);
			verificationAddress.Text = "https://www.bilibili.com/";
			LabelAt(value, "登录后用浏览器打开验证，网页加载成功后自动关闭。", 127, 49).ForeColor = Color.FromArgb(96, 110, 122);
			LabelAt(value, "下方表单默认自动识别；识别有误时填写 CSS 选择器。", 24, 80).ForeColor = Color.FromArgb(96, 110, 122);
			userSelector = Field(value, "账号输入框", 24, 110, 460);
			passwordSelector = Field(value, "密码输入框", 24, 142, 460);
			providerSelector = Field(value, "运营商控件", 24, 174, 460);
			providerValue = Field(value, "运营商选项值", 24, 206, 460);
			loginSelector = Field(value, "登录按钮", 24, 238, 460);
			suffix = Field(value, "账号后缀", 24, 270, 460);
			Label label = LabelAt(value, "示例：#username、input[name=\"DDDDD\"]、#ISP_select。", 24, 306);
			label.ForeColor = Color.FromArgb(96, 110, 122);
			int num = label.Top + label.Font.Height;
			LabelAt(value, "选项值、账号后缀仅在学校明确要求时填写。", 24, num).ForeColor = label.ForeColor;
			LabelAt(value, "QQ交流群：1131310647", 470, num).ForeColor = Color.FromArgb(45, 52, 60);
			state = new Label
			{
				Text = "准备就绪",
				Font = new Font(Font, FontStyle.Bold),
				ForeColor = Color.FromArgb(37, 161, 244),
				Location = new Point(27, 501),
				Size = new Size(690, 42)
			};
			base.Controls.Add(state);
			log = new TextBox
			{
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Vertical,
				Location = new Point(25, 548),
				Size = new Size(693, 83),
				BackColor = Color.White,
				BorderStyle = BorderStyle.FixedSingle
			};
			base.Controls.Add(log);
			save = ButtonAt("保存设置", 25, 647, 127);
			login = ButtonAt("立即登录 / 测试", 166, 647, 155);
			cancel = ButtonAt("取消", 335, 647, 84);
			open = ButtonAt("打开认证页", 514, 647, 203);
			login.BackColor = Color.FromArgb(37, 161, 244);
			login.ForeColor = Color.White;
			login.FlatStyle = FlatStyle.Flat;
			save.Click += delegate
			{
				try
				{
					Settings settings = SaveSettings();
					Report("设置已保存。" + (settings.Startup ? "下次进入 Windows 桌面后将自动登录，请保持此 exe 的位置不变。" : "开机自动登录已关闭。"));
				}
				catch (Exception ex2)
				{
					SetState(ex2.Message, true);
				}
			};
			login.Click += async delegate
			{
				if (running == null && ConfirmSettings("开始连接"))
				{
					await RunLogin(false);
				}
			};
			cancel.Enabled = false;
			cancel.Click += delegate
			{
				if (running != null)
				{
					running.Cancel();
				}
			};
			open.Click += delegate
			{
				try
				{
					Process.Start(new ProcessStartInfo(Settings.NormalizeUrl(address.Text))
					{
						UseShellExecute = true
					});
				}
				catch (Exception ex)
				{
					SetState(ex.Message, true);
				}
			};
			trayIcon = CreateTrayIcon();
			base.Icon = trayIcon;
			tray = new NotifyIcon
			{
				Icon = trayIcon,
				Text = "校园网自动登录",
				Visible = true
			};
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
			contextMenuStrip.Items.Add("打开设置", null, delegate
			{
				Restore();
			});
			contextMenuStrip.Items.Add("退出", null, delegate
			{
				exitRequested = true;
				Close();
			});
			tray.ContextMenuStrip = contextMenuStrip;
			tray.DoubleClick += delegate
			{
				Restore();
			};
		}

		private TextBox Field(Control parent, string caption, int x, int y, int width)
		{
			LabelAt(parent, caption, x, y + 4);
			TextBox textBox = new TextBox();
			textBox.Location = new Point(127, y);
			textBox.Size = new Size(width, 27);
			textBox.MaxLength = 1024;
			TextBox textBox2 = textBox;
			parent.Controls.Add(textBox2);
			return textBox2;
		}

		private Label LabelAt(Control parent, string text, int x, int y)
		{
			Label label = new Label();
			label.Text = text;
			label.Location = new Point(x, y);
			label.AutoSize = true;
			Label label2 = label;
			parent.Controls.Add(label2);
			return label2;
		}

		private Button ButtonAt(string text, int x, int y, int width)
		{
			Button button = new Button();
			button.Text = text;
			button.Location = new Point(x, y);
			button.Size = new Size(width, 34);
			button.UseVisualStyleBackColor = true;
			Button button2 = button;
			base.Controls.Add(button2);
			return button2;
		}

		private void Restore()
		{
			Show();
			base.Opacity = 1.0;
			base.ShowInTaskbar = true;
			base.WindowState = FormWindowState.Normal;
			Activate();
		}

		private void MinimizeToTray()
		{
			base.ShowInTaskbar = false;
			base.WindowState = FormWindowState.Minimized;
			Hide();
		}

		private Icon CreateTrayIcon()
		{
			return AppArtwork.LoadIcon(32);
		}

		private void ReadSettings()
		{
			try
			{
				Settings settings = Settings.Load();
				address.Text = settings.Url;
				verificationAddress.Text = settings.VerificationUrl;
				username.Text = settings.Username;
				password.Text = settings.GetPassword();
				provider.SelectedItem = settings.Provider;
				if (provider.SelectedIndex < 0)
				{
					provider.SelectedIndex = 0;
				}
				startup.Checked = Settings.HasStartup();
				showBrowser.Checked = settings.ShowBrowserOnStartup;
				delay.Value = Math.Max(0, Math.Min(180, settings.StartupDelay));
				userSelector.Text = settings.UsernameSelector;
				passwordSelector.Text = settings.PasswordSelector;
				providerSelector.Text = settings.ProviderSelector;
				providerValue.Text = settings.ProviderValue;
				loginSelector.Text = settings.LoginSelector;
				suffix.Text = settings.AccountSuffix;
			}
			catch
			{
				Report("设置或密码无法读取，请在当前 Windows 账户中重新填写并保存。", true);
			}
			changes.Accept(EditValues());
		}

		private string[] EditValues()
		{
			return new string[14]
			{
				address.Text,
				username.Text,
				password.Text,
				provider.Text,
				startup.Checked.ToString(),
				showBrowser.Checked.ToString(),
				delay.Value.ToString(),
				userSelector.Text,
				passwordSelector.Text,
				providerSelector.Text,
				providerValue.Text,
				loginSelector.Text,
				suffix.Text,
				verificationAddress.Text
			};
		}

		private Settings SaveSettings()
		{
			Settings settings = CaptureSettings();
			if (settings.Startup && (settings.Username.Length == 0 || password.Text.Length == 0))
			{
				throw new InvalidOperationException("启用开机登录前，请填写账号和密码。");
			}
			settings.Save();
			settings.ApplyStartup();
			changes.Accept(EditValues());
			return settings;
		}

		private bool ConfirmSettings(string action)
		{
			return changes.Confirm(EditValues(), () => MessageBox.Show(this, "设置已更改，是否保存后" + action + "？\n\n是：保存后继续。\n否：本次不保存，继续" + action + "。\n取消：返回修改。", "保存设置", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question), delegate
			{
				try
				{
					SaveSettings();
					return true;
				}
				catch (Exception ex)
				{
					Report("设置保存未完成：" + ex.Message, true);
					MessageBox.Show(this, ex.Message, "设置未保存", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return false;
				}
			});
		}

		private Settings CaptureSettings()
		{
			Settings settings = new Settings();
			settings.Url = Settings.NormalizeUrl(address.Text);
			settings.VerificationUrl = Settings.NormalizeVerificationUrl(verificationAddress.Text);
			settings.Username = username.Text.Trim();
			settings.EncryptedPassword = Settings.Protect(password.Text);
			settings.Provider = provider.Text;
			settings.Startup = startup.Checked;
			settings.ShowBrowserOnStartup = showBrowser.Checked;
			settings.StartupDelay = (int)delay.Value;
			settings.UsernameSelector = userSelector.Text.Trim();
			settings.PasswordSelector = passwordSelector.Text.Trim();
			settings.ProviderSelector = providerSelector.Text.Trim();
			settings.ProviderValue = providerValue.Text.Trim();
			settings.LoginSelector = loginSelector.Text.Trim();
			settings.AccountSuffix = suffix.Text;
			return settings;
		}

		private void SetState(string text, bool error)
		{
			state.Text = text;
			state.ForeColor = (error ? Color.FromArgb(160, 49, 49) : Color.FromArgb(37, 161, 244));
		}

		private void Report(string text, bool error = false)
		{
			SetState(text, error);
			if (text == lastMessage)
			{
				return;
			}
			lastMessage = text;
			string contents = DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine;
			if (log.TextLength > 20000)
			{
				log.Clear();
			}
			log.AppendText(contents);
			try
			{
				Directory.CreateDirectory(Settings.DirectoryPath);
				string text2 = Path.Combine(Settings.DirectoryPath, "last-run.log");
				if (File.Exists(text2) && new FileInfo(text2).Length > 65536)
				{
					File.WriteAllText(text2, "");
				}
				File.AppendAllText(text2, contents);
			}
			catch
			{
			}
		}

		private void Busy(bool busy)
		{
			foreach (Control control in base.Controls)
			{
				if (control is TabControl)
				{
					control.Enabled = !busy;
				}
			}
			Button button = save;
			Button button2 = login;
			bool flag2 = (open.Enabled = !busy);
			bool flag3 = flag2;
			bool flag5 = (button2.Enabled = flag3);
			bool enabled = flag5;
			button.Enabled = enabled;
			cancel.Enabled = busy;
		}

		private async Task RunLogin(bool auto)
		{
			if (running != null)
			{
				return;
			}
			running = new CancellationTokenSource();
			Busy(true);
			bool completed = false;
			try
			{
				Settings cfg = CaptureSettings();
				string selectedProvider = cfg.Provider;
				if (string.IsNullOrWhiteSpace(cfg.Username) || password.Text.Length == 0)
				{
					throw new InvalidOperationException("请填写账号和密码，然后点击连接。");
				}
				if (auto)
				{
					await NetworkReady.WaitAsync(cfg.StartupDelay, running.Token, delegate(string message)
					{
						Report(message);
					});
				}
				ConnectionNotice notice = await ConnectionFlow.RunAsync(selectedProvider, () => LoginService.RunAsync(cfg, !auto || cfg.ShowBrowserOnStartup, new Progress<string>(delegate(string s)
				{
					Report(s);
				}), running.Token), () => ConnectivityCheck.VerifyAsync(cfg.VerificationUrl, running.Token), delegate(string s)
				{
					Report(s);
				}, running.Token);
				completed = !notice.Failed;
				Report(notice.Title + "；" + notice.Status + "。" + notice.Detail.Replace("\n", " "), notice.Failed);
				if (notification != null && !notification.IsDisposed)
				{
					notification.Close();
				}
				notification = new LoginNotification(notice, Restore);
				notification.Show();
				if (auto && notice.Failed)
				{
					Restore();
				}
			}
			catch (OperationCanceledException)
			{
				Report("登录已取消。尚未确认认证结果。");
			}
			catch (Exception ex2)
			{
				Report("登录未完成：" + ex2.Message, true);
				if (auto)
				{
					Restore();
				}
			}
			finally
			{
				running.Dispose();
				running = null;
				Busy(false);
			}
			if (closeWhenFinished)
			{
				Close();
			}
			else if (auto && completed)
			{
				MinimizeToTray();
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (notification != null)
				{
					notification.Dispose();
				}
				if (tray != null)
				{
					tray.Visible = false;
					tray.Dispose();
				}
				if (trayIcon != null)
				{
					trayIcon.Dispose();
				}
				if (logoImage != null)
				{
					logoImage.Dispose();
				}
				if (running != null)
				{
					running.Cancel();
				}
			}
			base.Dispose(disposing);
		}
	}
}
