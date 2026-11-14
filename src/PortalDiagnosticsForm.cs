using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CampusNet {
    internal sealed class PortalDiagnosticsForm : Form {
        readonly string url;
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        readonly List<Dictionary<string,object>> samples=new List<Dictionary<string,object>>();
        readonly TextBox summary=new TextBox { Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, Dock=DockStyle.Fill, BackColor=Color.White, BorderStyle=BorderStyle.FixedSingle };
        readonly Label status=new Label { Text="准备打开当前认证地址…", Dock=DockStyle.Fill, AutoEllipsis=true };
        readonly Button rescan=new Button { Text="重新检测", AutoSize=true, Enabled=false };
        readonly Button save=new Button { Text="保存检测报告", AutoSize=true, Enabled=false };
        readonly Button close=new Button { Text="关闭", AutoSize=true };
        BrowserSession browser;
        Task operation=Task.FromResult(0);
        bool busy, closing, canClose, disposed;
        internal PortalDiagnosticsForm(string address) {
            url=address; Text="登录页检测"; ClientSize=new Size(640,450); MinimumSize=new Size(610,440);
            StartPosition=FormStartPosition.CenterParent; Font=new Font("Microsoft YaHei UI",9f);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(20), ColumnCount=1, RowCount=4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            layout.Controls.Add(new Label { Text="会打开独立浏览器检查当前登录页，无需填写账号密码。\n等页面加载完整；也可展开运营商菜单后重新检测。", Dock=DockStyle.Fill });
            layout.Controls.Add(summary); layout.Controls.Add(status);
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill }; buttons.Controls.Add(rescan); buttons.Controls.Add(save); buttons.Controls.Add(close); layout.Controls.Add(buttons); Controls.Add(layout);
            Shown+=async (s,e)=>await ScanAsync(true);
            rescan.Click+=async (s,e)=>await ScanAsync(false);
            save.Click+=delegate {
                if(samples.Count==0)return;
                using(var dialog=new SaveFileDialog { Filter="检测报告 (*.txt)|*.txt", FileName="校园网检测报告_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt", InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }) {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    try { File.WriteAllText(dialog.FileName,PortalDiagnosticsReport.Export(samples),new System.Text.UTF8Encoding(true)); status.Text="报告已保存，可将 .txt 文件发给作者。"; }
                    catch(Exception) { MessageBox.Show(this,"文件保存失败，请换一个可写入的位置。","保存报告",MessageBoxButtons.OK,MessageBoxIcon.Information); }
                }
            };
            close.Click+=delegate { Close(); };
            FormClosing+=async (s,e)=> {
                if(canClose)return; e.Cancel=true; if(closing)return; closing=true; lifetime.Cancel(); Enabled=false;
                try { await operation; } catch { }
                if(browser!=null) { await browser.CloseAsync(); browser.Dispose(); browser=null; }
                canClose=true; Close();
            };
        }
        async Task ScanAsync(bool first) {
            if(busy || closing)return; busy=true; rescan.Enabled=save.Enabled=false;
            operation=ScanCoreAsync(first);
            try { await operation; }
            catch(OperationCanceledException) { }
            catch(Exception) { status.Text="检测未完成，请确认诊断浏览器仍打开且页面已加载。可重新检测或关闭后重试。"; }
            finally { busy=false; if(!closing){rescan.Enabled=browser!=null; save.Enabled=samples.Count>0;} }
        }
        async Task ScanCoreAsync(bool first) {
            if(first) {
                browser=new BrowserSession(Path.Combine(Settings.DirectoryPath,"Diagnostics"));
                await browser.StartAsync(url,true,lifetime.Token,new Progress<string>(s=> { if(!closing)status.Text=s; }));
            }
            int attempts=first?3:1;
            for(int i=0;i<attempts;i++) {
                if(first)await Task.Delay(i==0?1000:1500,lifetime.Token);
                status.Text="正在检测页面结构…";
                var report=await browser.EvaluateAsync(PortalDiagnosticsReport.Script(),lifetime.Token);
                if(!report.ContainsKey("pages"))continue;
                report["sampleTime"]=DateTime.Now.ToString("HH:mm:ss");
                samples.Add(report); if(samples.Count>6)samples.RemoveAt(0);
                summary.Text=PortalDiagnosticsReport.Summary(report);
            }
            status.Text=samples.Count==0?"页面仍在切换，请稍后重新检测。":"检测完成；展开菜单后可再次检测，报告会保留前后结构。";
        }
        protected override void Dispose(bool disposing) {
            if(disposing && !disposed) { disposed=true; lifetime.Cancel(); lifetime.Dispose(); if(browser!=null)browser.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
