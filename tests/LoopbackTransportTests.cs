using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet {
    public static class LoopbackTransportTests {
        static List<string> results;
        static void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); results.Add("PASS: "+name); }
        public static string[] RunAll() { results=new List<string>(); RunAsync().GetAwaiter().GetResult(); results.Add("ALL "+results.Count+" TRANSPORT TESTS PASSED"); return results.ToArray(); }
        static async Task RunAsync() {
            using(var timeout=new CancellationTokenSource(15000)) using(var server=new Fixture(async stream => {
                await Handshake(stream,true);
                await WriteFrame(stream,1,Encoding.UTF8.GetBytes("ready"),true);
                for(int i=0;i<3;i++) {
                    var frame=await ReadClientFrame(stream);
                    Check(frame.Item1==1,"Client sends masked text frame #"+i);
                    int half=frame.Item2.Length/2;
                    await WriteFrame(stream,1,Slice(frame.Item2,0,half),false);
                    await WriteFrame(stream,9,Encoding.ASCII.GetBytes("ping"),true);
                    await WriteFrame(stream,0,Slice(frame.Item2,half,frame.Item2.Length-half),true);
                    var pong=await ReadClientFrame(stream); Check(pong.Item1==10 && Encoding.ASCII.GetString(pong.Item2)=="ping","Respond to ping during fragmented text #"+i);
                }
            })) using(var client=new LoopbackWebSocket()) {
                await client.ConnectAsync(server.Url,timeout.Token); Check(client.IsOpen,"Complete real loopback RFC6455 handshake");
                Check(await client.ReceiveTextAsync(timeout.Token)=="ready","Read message sent immediately after handshake");
                foreach(string payload in new[]{"本机测试'\"\\",new string('a',300),new string('b',70000)}) {
                    await client.SendTextAsync(payload,timeout.Token);
                    Check(await client.ReceiveTextAsync(timeout.Token)==payload,"Roundtrip fragmented UTF8 / length "+Encoding.UTF8.GetByteCount(payload));
                }
                await server.Completion;
            }
            using(var timeout=new CancellationTokenSource(5000)) using(var server=new Fixture(async stream => { await Handshake(stream,false); })) using(var client=new LoopbackWebSocket()) {
                bool rejected=false; try { await client.ConnectAsync(server.Url,timeout.Token); } catch(WebSocketException) { rejected=true; }
                Check(rejected,"Reject incorrect server handshake signature"); await server.Completion;
            }
            using(var timeout=new CancellationTokenSource(5000)) using(var server=new Fixture(async stream => {
                await Handshake(stream,true); var one=new byte[1]; try { await stream.ReadAsync(one,0,1); } catch(IOException) {}
            })) using(var client=new LoopbackWebSocket()) {
                await client.ConnectAsync(server.Url,timeout.Token);
                using(var cancel=new CancellationTokenSource(100)) {
                    bool cancelled=false; try { await client.ReceiveTextAsync(cancel.Token); } catch(OperationCanceledException) { cancelled=true; }
                    Check(cancelled,"Pending socket read is promptly cancellable");
                }
                await server.Completion;
            }
            using(var client=new LoopbackWebSocket()) {
                bool blocked=false; try { await client.ConnectAsync(new Uri("ws://9.9.9.9:9222/devtools/page/test"),CancellationToken.None); } catch(InvalidOperationException) { blocked=true; }
                Check(blocked,"Browser transport only accepts loopback endpoints");
            }
            using(var timeout=new CancellationTokenSource(5000)) using(var server=new Fixture(async stream => {
                await Handshake(stream,true); await WriteFrame(stream,8,new byte[0],true);
            })) using(var client=new LoopbackWebSocket()) {
                await client.ConnectAsync(server.Url,timeout.Token);
                bool closed=false; try { await client.ReceiveTextAsync(timeout.Token); } catch(WebSocketException) { closed=true; }
                Check(closed&&!client.IsOpen,"Report server closing the browser transport"); await server.Completion;
            }
        }
        static byte[] Slice(byte[] input,int offset,int length) { var output=new byte[length]; Buffer.BlockCopy(input,offset,output,0,length); return output; }
        static async Task Exact(NetworkStream stream,byte[] bytes) { int offset=0; while(offset<bytes.Length) { int n=await stream.ReadAsync(bytes,offset,bytes.Length-offset); if(n==0) throw new IOException("Unexpected EOF"); offset+=n; } }
        static async Task Handshake(NetworkStream stream,bool valid) {
            var header=new StringBuilder(); var one=new byte[1];
            while(header.Length<16384) { await Exact(stream,one); header.Append((char)one[0]); if(header.ToString().EndsWith("\r\n\r\n"))break; }
            string key=null;
            foreach(string line in header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None)) if(line.StartsWith("Sec-WebSocket-Key:",StringComparison.OrdinalIgnoreCase)) key=line.Substring(line.IndexOf(':')+1).Trim();
            if(key==null) throw new Exception("Missing nonce"); string accept;
            using(var sha=SHA1.Create()) accept=Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: keep-alive, Upgrade\r\nSec-WebSocket-Accept: "+(valid?accept:"bad-signature")+"\r\n\r\n");
            await stream.WriteAsync(response,0,response.Length);
        }
        static async Task<Tuple<int,byte[]>> ReadClientFrame(NetworkStream stream) {
            var header=new byte[2]; await Exact(stream,header);
            if((header[1]&128)==0) throw new Exception("Client frame must be masked");
            ulong size=(ulong)(header[1]&127);
            if(size==126){var extended=new byte[2];await Exact(stream,extended);size=(ulong)(extended[0]*256+extended[1]);}
            else if(size==127){var extended=new byte[8];await Exact(stream,extended);size=0;foreach(byte b in extended)size=(size<<8)|b;}
            if(size>200000) throw new Exception("Test frame too large");
            var mask=new byte[4];await Exact(stream,mask);var payload=new byte[(int)size];await Exact(stream,payload);
            for(int i=0;i<payload.Length;i++)payload[i]^=mask[i%4];
            return Tuple.Create(header[0]&15,payload);
        }
        static async Task WriteFrame(NetworkStream stream,int opcode,byte[] payload,bool final) {
            using(var frame=new MemoryStream()) {
                frame.WriteByte((byte)((final?128:0)|opcode));
                if(payload.Length<126)frame.WriteByte((byte)payload.Length);
                else if(payload.Length<=65535){frame.WriteByte(126);frame.WriteByte((byte)(payload.Length>>8));frame.WriteByte((byte)payload.Length);}
                else{frame.WriteByte(127);for(int shift=56;shift>=0;shift-=8)frame.WriteByte((byte)((ulong)payload.Length>>shift));}
                frame.Write(payload,0,payload.Length);var bytes=frame.ToArray();await stream.WriteAsync(bytes,0,bytes.Length);
            }
        }
        sealed class Fixture:IDisposable {
            TcpListener listener=new TcpListener(IPAddress.Loopback,0);
            public Uri Url;public Task Completion;
            public Fixture(Func<NetworkStream,Task> serve) {
                listener.Start();Url=new Uri("ws://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/devtools/page/test");
                Completion=Task.Run(async ()=> { using(var client=await listener.AcceptTcpClientAsync()) using(var stream=client.GetStream()) await serve(stream); });
            }
            public void Dispose(){listener.Stop();}
        }
    }
}
