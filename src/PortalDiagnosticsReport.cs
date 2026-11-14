using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace CampusNet {
    internal static class PortalDiagnosticsReport {
        internal static string Script() {
            using(var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Inspect.js"))) return reader.ReadToEnd();
        }
        static IEnumerable<Dictionary<string, object>> Items(object value) {
            var array = value as IEnumerable;
            if (array != null) foreach (var item in array) { var record = item as Dictionary<string, object>; if(record != null) yield return record; }
        }
        static string Text(Dictionary<string, object> r, string key) { object value; return r.TryGetValue(key,out value) ? Convert.ToString(value) : ""; }
        static bool Flag(Dictionary<string, object> r, string key) { object value; return r.TryGetValue(key,out value) && value is bool && (bool)value; }
        internal static string Summary(Dictionary<string, object> report) {
            int inputs=0, passwords=0, tips=0, menus=0, images=0, blockedFrames=0, shadows=0;
            bool imageTemplate=false;
            foreach(var page in Items(report["pages"])) {
                if(Text(page,"frame").Contains("/shadow")) shadows++;
                foreach(var frame in Items(page["frames"])) if(Flag(frame,"visible") && !Flag(frame,"accessible")) blockedFrames++;
                foreach(var r in Items(page["records"])) {
                    if(!Flag(r,"visible")) continue;
                    string tag=Text(r,"tag"), type=Text(r,"type"), id=Text(r,"id");
                    if(tag=="INPUT" && (type=="text" || type=="email" || type=="tel" || type=="number")) inputs++;
                    if(tag=="INPUT" && type=="password") passwords++;
                    if(Flag(r,"tipField")) tips++;
                    if(tag=="SELECT" || Text(r,"role")=="combobox" || id.StartsWith("bch_service_")) menus++;
                    if(Flag(r,"backgroundImage") && (Text(r,"cursor")=="pointer" || id.IndexOf("login",StringComparison.OrdinalIgnoreCase)>=0)) images++;
                    if(id=="loginLink_div" && Flag(r,"backgroundImage")) imageTemplate=true;
                }
            }
            var s=new StringBuilder();
            s.AppendLine("页面结构检测完成（不代表登录成功）").AppendLine();
            s.AppendLine("可见文本输入框："+inputs+"　密码框："+passwords);
            s.AppendLine("提示输入框："+tips+"　运营商/服务控件线索："+menus);
            s.AppendLine("可点击图片背景线索："+images+"　组件内层页面："+shadows);
            s.AppendLine();
            if(tips>0) s.AppendLine("• 页面使用提示框，真正输入框可能需要点击后才显示。");
            if(imageTemplate) s.AppendLine("• 发现曾遇到的图片登录按钮结构，按钮文字可能无法直接读取。");
            else if(images>0) s.AppendLine("• 发现图片控件，需要结合报告确认哪个用于登录。");
            if(blockedFrames>0) s.AppendLine("• "+blockedFrames+" 个可见内嵌页面无法读取，报告已标注，不会绕过浏览器限制。");
            if(inputs==0 && passwords==0) s.AppendLine("• 未发现可见输入框；请等页面加载完整，再点“重新检测”。");
            if(Flag(report,"truncated")) s.AppendLine("• 页面较大，报告已限制采集数量。");
            s.AppendLine().AppendLine("可手动展开运营商菜单，再点“重新检测”记录展开后的结构。");
            s.AppendLine("保存报告发给作者，可帮助定位不同学校的兼容问题。检测不会自动改写登录配置。");
            return s.ToString();
        }
        internal static string Export(IList<Dictionary<string, object>> samples) {
            var json=new JavaScriptSerializer { MaxJsonLength=8*1024*1024 };
            return "校园网登录页检测报告 v2.0\r\n"+"软件基线：v1.9.1 检测精简版\r\n"+
                "生成时间："+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"\r\n"+
                "不包含输入框内容、Cookie、存储数据、网址参数或完整网页源码。\r\n\r\n"+
                Summary(samples[samples.Count-1])+"\r\n--- 控件结构（包含加载过程及手动复查，供适配使用）---\r\n"+Pretty(json.Serialize(samples));
        }
        static string Pretty(string value) {
            var result=new StringBuilder(); bool quoted=false, escaped=false; int indent=0;
            foreach(char c in value) {
                if(quoted) { result.Append(c); if(escaped)escaped=false; else if(c=='\\')escaped=true; else if(c=='"')quoted=false; continue; }
                if(c=='"') { quoted=true; result.Append(c); }
                else if(c=='{' || c=='[') { result.Append(c).AppendLine(); indent++; result.Append(' ',indent*2); }
                else if(c=='}' || c==']') { result.AppendLine(); indent--; result.Append(' ',indent*2).Append(c); }
                else if(c==',')result.Append(c).AppendLine().Append(' ',indent*2);
                else if(c==':')result.Append(": "); else result.Append(c);
            }
            return result.ToString();
        }
    }
}
