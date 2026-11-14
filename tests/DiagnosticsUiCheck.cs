using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using CampusNet;
class DiagnosticsUiCheck {
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); Console.WriteLine("PASS: "+message); }
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();
        using(var form=new MainForm(false)) {
            form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-4000,-4000); form.ShowInTaskbar=false;
            form.Show();
            var tabs=form.Controls.OfType<TabControl>().Single(); tabs.SelectedIndex=1; Application.DoEvents();
            var page=tabs.SelectedTab;
            Check(page.Controls.OfType<TextBox>().Count()==1,"Only the website verification textbox remains in advanced settings");
            Check(!page.Controls.OfType<ComboBox>().Any(),"No manual school or adapter selector exists");
            Check(page.Controls.Find("diagnosePortal",false).Length==1,"Diagnostic entry exists");
            Check(page.Controls.Find("diagnosePortal",false)[0].Text=="点击检测","Diagnostic button retains requested wording");
            foreach(string key in new[]{"openGitHub","openBilibili","copyQQGroup"}) {
                var control=page.Controls.Find(key,false).Single();
                Check(control.Visible && page.ClientRectangle.Contains(control.Bounds),key+" is visible without scrolling");
            }
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var capture=typeof(MainForm).GetMethod("CaptureSettings",flags);
            var saved=(Settings)capture.Invoke(form,null);
            Check(String.IsNullOrEmpty(saved.UsernameSelector+saved.PasswordSelector+saved.ProviderSelector+saved.ProviderValue+saved.LoginSelector+saved.AccountSuffix),"Login settings always use automatic recognition");
            Check(!page.Controls.OfType<Control>().Any(c=>c.Text.Contains("恢复自动识别") || c.Text.Contains("自动识别已开启") || c.Text.Contains("自动检查输入框")),"Redundant button, status and description are removed");
            using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[0]); }
            form.Hide();
        }
    }
}
