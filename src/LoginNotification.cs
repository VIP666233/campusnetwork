using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CampusNet
{
	internal sealed class LoginNotification : Form
	{
		private readonly Timer dismiss = new Timer
		{
			Interval = 10000
		};

		private readonly Timer motion = new Timer
		{
			Interval = 15
		};

		private readonly Stopwatch motionClock = new Stopwatch();

		private readonly Font headingFont = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);

		private readonly Font detailFont = new Font("Microsoft YaHei UI", 9.5f);

		private Point restingPoint;

		private Point hiddenPoint;

		private Point motionFrom;

		private Point motionTo;

		private double opacityFrom;

		private double opacityTo;

		private int motionDuration;

		private bool exiting;

		private bool closeCommitted;

		private bool positioned;

		private Action afterClose;

		private static bool AnimationsEnabled
		{
			get
			{
				bool value;
				if (SystemParametersInfo(4162u, 0u, out value, 0u))
				{
					return value;
				}
				return true;
			}
		}

		protected override bool ShowWithoutActivation
		{
			get
			{
				return true;
			}
		}

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams createParams = base.CreateParams;
				createParams.ExStyle |= 134217856;
				return createParams;
			}
		}

		public LoginNotification(ConnectionNotice notice, Action openSettings)
		{
			LoginNotification loginNotification = this;
			base.AutoScaleDimensions = new SizeF(96f, 96f);
			base.AutoScaleMode = AutoScaleMode.Dpi;
			base.ClientSize = new Size(416, 218);
			base.FormBorderStyle = FormBorderStyle.None;
			base.StartPosition = FormStartPosition.Manual;
			base.ShowInTaskbar = false;
			base.TopMost = true;
			BackColor = Color.White;
			DoubleBuffered = true;
			Font = detailFont;
			Text = "校园网自动登录 — " + notice.Status;
			base.AccessibleName = notice.Title + "，" + notice.Status;
			TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(20, 16, 20, 14),
				ColumnCount = 1,
				RowCount = 4,
				BackColor = Color.White,
				Margin = Padding.Empty
			};
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
			tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
			tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			tableLayoutPanel.Controls.Add(new Label
			{
				Text = notice.Title,
				Font = headingFont,
				Dock = DockStyle.Fill,
				AutoEllipsis = true,
				ForeColor = Color.FromArgb(37, 161, 244)
			}, 0, 0);
			tableLayoutPanel.Controls.Add(new Label
			{
				Text = notice.Status,
				Dock = DockStyle.Fill,
				ForeColor = (notice.Failed ? Color.FromArgb(160, 49, 49) : (notice.Verified ? Color.FromArgb(37, 161, 244) : Color.FromArgb(140, 79, 12)))
			}, 0, 1);
			tableLayoutPanel.Controls.Add(new Label
			{
				Text = notice.Detail,
				Dock = DockStyle.Fill,
				ForeColor = Color.FromArgb(60, 74, 80)
			}, 0, 2);
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.RightToLeft,
				WrapContents = false,
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Margin = Padding.Empty,
				Padding = new Padding(0, 4, 0, 2)
			};
			Button button = new Button
			{
				Text = "关闭",
				AutoSize = true,
				MinimumSize = new Size(88, 34),
				Margin = Padding.Empty,
				Padding = new Padding(8, 2, 8, 2)
			};
			Button button2 = new Button
			{
				Text = "打开设置",
				AutoSize = true,
				MinimumSize = new Size(96, 34),
				Margin = new Padding(0, 0, 10, 0),
				Padding = new Padding(8, 2, 8, 2)
			};
			button.Click += delegate
			{
				Dismiss(null);
			};
			button2.Click += delegate
			{
				loginNotification.Dismiss(openSettings);
			};
			flowLayoutPanel.Controls.Add(button);
			flowLayoutPanel.Controls.Add(button2);
			tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 3);
			base.Controls.Add(tableLayoutPanel);
			dismiss.Tick += delegate
			{
				Dismiss(null);
			};
			motion.Tick += delegate
			{
				AdvanceMotion();
			};
			TrackPointer(this);
		}

		private void TrackPointer(Control control)
		{
			control.MouseEnter += delegate
			{
				dismiss.Stop();
			};
			control.MouseLeave += delegate
			{
				if (!exiting && !motion.Enabled && !RectangleToScreen(base.ClientRectangle).Contains(Cursor.Position))
				{
					dismiss.Start();
				}
			};
			foreach (Control control2 in control.Controls)
			{
				TrackPointer(control2);
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			restingPoint = new Point(Math.Max(workingArea.Left, workingArea.Right - base.Width - 16), Math.Max(workingArea.Top, workingArea.Bottom - base.Height - 16));
			hiddenPoint = new Point(workingArea.Right + 16, restingPoint.Y);
			positioned = true;
			base.Location = (AnimationsEnabled ? hiddenPoint : restingPoint);
			base.Opacity = 0.0;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			StartMotion(restingPoint, 1.0, 250);
		}

		private void Dismiss(Action action)
		{
			if (!exiting && !base.IsDisposed)
			{
				exiting = true;
				afterClose = action;
				dismiss.Stop();
				base.Enabled = false;
				StartMotion(hiddenPoint, 0.0, 200);
			}
		}

		private void StartMotion(Point target, double opacity, int duration)
		{
			motionFrom = base.Location;
			motionTo = (AnimationsEnabled ? target : restingPoint);
			opacityFrom = base.Opacity;
			opacityTo = opacity;
			motionDuration = duration;
			motionClock.Restart();
			motion.Start();
		}

		private void AdvanceMotion()
		{
			double num = Math.Min(1.0, motionClock.Elapsed.TotalMilliseconds / (double)motionDuration);
			double num2 = EaseOut(num);
			base.Location = new Point((int)Math.Round((double)motionFrom.X + (double)(motionTo.X - motionFrom.X) * num2), (int)Math.Round((double)motionFrom.Y + (double)(motionTo.Y - motionFrom.Y) * num2));
			base.Opacity = Math.Max(0.0, Math.Min(1.0, opacityFrom + (opacityTo - opacityFrom) * num2));
			if (num < 1.0)
			{
				return;
			}
			motion.Stop();
			if (exiting)
			{
				closeCommitted = true;
				Close();
				if (afterClose != null)
				{
					afterClose();
				}
			}
			else if (!RectangleToScreen(base.ClientRectangle).Contains(Cursor.Position))
			{
				dismiss.Start();
			}
		}

		internal static double EaseOut(double progress)
		{
			double num = 0.0;
			double num2 = 1.0;
			for (int i = 0; i < 18; i++)
			{
				double num3 = (num + num2) / 2.0;
				double num4 = 1.0 - num3;
				double num5 = 3.0 * num4 * num4 * num3 * 0.23 + 3.0 * num4 * num3 * num3 * 0.32 + num3 * num3 * num3;
				if (num5 < progress)
				{
					num = num3;
				}
				else
				{
					num2 = num3;
				}
			}
			double num6 = (num + num2) / 2.0;
			return 1.0 - Math.Pow(1.0 - num6, 3.0);
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool SystemParametersInfo(uint action, uint parameter, out bool value, uint flags);

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (!closeCommitted && positioned && e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				Dismiss(null);
			}
			base.OnFormClosing(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			using (Pen pen = new Pen(Color.FromArgb(180, 195, 198)))
			{
				e.Graphics.DrawRectangle(pen, 0, 0, base.Width - 1, base.Height - 1);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				dismiss.Dispose();
				motion.Dispose();
			}
			base.Dispose(disposing);
			if (disposing)
			{
				headingFont.Dispose();
				detailFont.Dispose();
			}
		}
	}
}
