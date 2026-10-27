using System;
using System.Collections.Generic;

namespace CampusNet {
    public static class PortalRedirectTests {
        public static string[] RunAll() {
            var results=new List<string>();
            DateTime now=new DateTime(2026,10,14,0,0,0,DateTimeKind.Utc);
            var r=new PortalRedirect("http://9.9.9.9/");
            Check(!r.Observe("http://10.2.5.251/a79.htm",now) && r.CurrentUrl=="http://9.9.9.9/","Do not use transient redirected origin",results);
            Check(!r.Observe("http://10.2.5.251/a79.htm",now.AddSeconds(2)),"Wait for redirect stability",results);
            Check(r.Observe("http://10.2.5.251/a79.htm",now.AddSeconds(3)) && r.CurrentUrl=="http://10.2.5.251/a79.htm","Follow stable campus portal in the same operation",results);
            r=new PortalRedirect("http://9.9.9.9/");
            r.Observe("http://10.0.0.1/login",now); r.Observe("http://10.2.5.251/a79.htm",now.AddSeconds(2));
            Check(!r.Observe("http://10.2.5.251/a79.htm",now.AddSeconds(3)),"Restart stability window when redirect changes",results);
            r.ResetPending();
            Check(!r.Observe("http://10.2.5.251/a79.htm",now.AddSeconds(8)),"A non-redirect observation clears pending transition",results);
            bool blocked=false; try { new PortalRedirect("https://9.9.9.9/").Observe("http://10.2.5.251/a79.htm",now); } catch(InvalidOperationException) { blocked=true; }
            Check(blocked,"Do not silently downgrade HTTPS",results);
            blocked=false; try { r.Observe("file:///C:/login",now); } catch(ArgumentException) { blocked=true; }
            Check(blocked,"Only follow HTTP or HTTPS",results);
            r=new PortalRedirect("http://9.9.9.9/"); blocked=false;
            try { for(int i=0;i<5;i++) { string target="http://10.0.0."+(i+1)+"/login"; r.Observe(target,now.AddSeconds(i*4)); r.Observe(target,now.AddSeconds(i*4+3)); } } catch(InvalidOperationException) { blocked=true; }
            Check(blocked,"Bound repeated redirects",results);
            return results.ToArray();
        }
        static void Check(bool condition,string name,List<string> results) { if(!condition)throw new Exception("FAIL: "+name); results.Add("PASS: "+name); }
    }
}
