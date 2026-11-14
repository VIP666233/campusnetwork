using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CampusNet
{
    internal static class ProjectLinks
    {
        internal const string GitHubUrl = "https://github.com/VIP666233/campusnetwork";
        internal const string BilibiliUrl = "https://www.bilibili.com/video/BV1tbnq66EeL/";
        internal const string QQGroup = "1131310647";

        internal static void Add(TabPage page, int qqY)
        {
            page.AutoScroll = true;
            page.AutoScrollMinSize = new Size(0, 340);
            var feedback = new Label { Name = "projectLinkFeedback", AutoSize = true, Location = new Point(24, 316), ForeColor = Color.FromArgb(45, 52, 60) };
            page.Controls.Add(feedback);
            var qq = MakeLink("QQ交流群：" + QQGroup, "copyQQGroup", 470, qqY, 190);
            qq.LinkColor = Color.FromArgb(45, 52, 60);
            qq.AccessibleDescription = "点击复制 QQ 群号 " + QQGroup;
            qq.LinkClicked += delegate { Copy(QQGroup, "QQ群号", feedback, qq); };
            page.Controls.Add(qq);
            AddUrl(page, "GitHub", GitHubUrl, "GitHub", 244, feedback);
            AddUrl(page, "Bilibili", BilibiliUrl, "Bilibili", 278, feedback);
        }

        static LinkLabel MakeLink(string text, string name, int x, int y, int width)
        {
            return new LinkLabel { Text = text, Name = name, AutoSize = false, Location = new Point(x, y), Size = new Size(width, 24),
                LinkColor = Color.FromArgb(28, 116, 180), ActiveLinkColor = Color.FromArgb(20, 85, 130), VisitedLinkColor = Color.FromArgb(28, 116, 180),
                LinkBehavior = LinkBehavior.HoverUnderline, TabStop = true, AutoEllipsis = true };
        }

        static void AddUrl(TabPage page, string caption, string url, string key, int y, Label feedback)
        {
            page.Controls.Add(new Label { Text = caption, AutoSize = true, Location = new Point(24, y) });
            var link = MakeLink(url, "open" + key, 127, y, 447);
            link.AccessibleDescription = "点击在浏览器中打开 " + url;
            link.LinkClicked += delegate {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception) { feedback.Text = "无法打开浏览器，请复制链接后手动打开。"; page.ScrollControlIntoView(feedback); }
            };
            page.Controls.Add(link);
            var copy = new Button { Name = "copy" + key, Text = "复制链接", Location = new Point(582, y - 4), Size = new Size(78, 28), UseVisualStyleBackColor = true,
                AccessibleName = "复制 " + caption + " 链接" };
            copy.Click += delegate { Copy(url, caption + " 链接", feedback, copy); };
            page.Controls.Add(copy);
        }

        static void Copy(string value, string caption, Label feedback, Control source)
        {
            try {
                Clipboard.SetDataObject(value, true, 5, 100);
                feedback.Text = caption + "已复制。";
            }
            catch (Exception) { feedback.Text = "剪贴板暂时不可用，请再点一次复制。"; }
            var page = feedback.Parent as ScrollableControl;
            if (page != null) page.ScrollControlIntoView(feedback);
        }
    }
}
