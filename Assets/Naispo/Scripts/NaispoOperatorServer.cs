using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace WebSwing
{
    public sealed class NaispoOperatorServer : MonoBehaviour
    {
        public const int Port=18765;
        public string Address { get; private set; }
        public string PairCode { get; private set; }
        public string Key { get; private set; }
        sealed class Pending {public string body;public DateTime deadline;public readonly TaskCompletionSource<string> result=new TaskCompletionSource<string>();}
        [Serializable] sealed class Pair {public string code;}
        readonly ConcurrentQueue<Pending> queue=new ConcurrentQueue<Pending>();
        readonly object pairLock=new object();int pairAttempts;DateTime pairWindow=DateTime.UtcNow;
        NaispoSession session;TcpListener listener;Thread thread;volatile bool running;
        volatile string status="{}";byte[] html;float nextStatus;
        volatile bool paired;long lastPoll;
        public bool PairedBefore=>paired;
        public bool Listening=>running;
        public void PublishStatus(string snapshot){status=snapshot;}
        public bool OperatorConnected=>paired&&TimeSpan.FromTicks(DateTime.UtcNow.Ticks-Interlocked.Read(ref lastPoll)).TotalSeconds<6;
        public void Initialize(NaispoSession owner)
        {
            session=owner;var bytes=new byte[4];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);
            PairCode=(100000+BitConverter.ToUInt32(bytes,0)%900000).ToString();Key=Guid.NewGuid().ToString("N");
            Address="http://"+LocalAddress()+":"+Port;
            var page=Resources.Load<TextAsset>("NaispoOperator");html=Encoding.UTF8.GetBytes(page?page.text:"Naispo operator page missing");
            status=session.StatusJson();
#if UNITY_EDITOR
            Address="http://QUEST-IP:"+Port;return; // Never run an operator panel on the development PC.
#else
            try{listener=new TcpListener(IPAddress.Any,Port);listener.Start(4);running=true;thread=new Thread(Accept){IsBackground=true,Name="Naispo local operator"};thread.Start();}
            catch(Exception error){Address="Operator connection unavailable";Debug.LogError("NAISPO_SERVER: "+error.Message);}
#endif
        }
        static string LocalAddress()
        {
            try{foreach(var nic in NetworkInterface.GetAllNetworkInterfaces())foreach(var item in nic.GetIPProperties().UnicastAddresses)
                if(item.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(item.Address)&&!item.Address.ToString().StartsWith("169.254."))return item.Address.ToString();}catch{}
            return "127.0.0.1";
        }
        void Update()
        {
            for(int i=0;i<4&&queue.TryDequeue(out var pending);i++)
            {
                if(DateTime.UtcNow>pending.deadline){pending.result.TrySetResult("{\"ok\":false}");continue;}
                try{var command=JsonUtility.FromJson<NaispoCommand>(pending.body);bool ok=session.Execute(command);status=session.StatusJson();pending.result.TrySetResult("{\"ok\":"+(ok?"true":"false")+",\"status\":"+status+"}");}
                catch{pending.result.TrySetResult("{\"ok\":false}");}
            }
            if(Time.unscaledTime>nextStatus){nextStatus=Time.unscaledTime+.2f;status=session.StatusJson();}
        }
        void Accept()
        {
            while(running)try{var client=listener.AcceptTcpClient();ThreadPool.QueueUserWorkItem(_=>Serve(client));}catch{if(!running)return;}
        }
        static string Line(NetworkStream stream,int maximum=4096)
        {
            var bytes=new List<byte>();int b;
            while((b=stream.ReadByte())>=0){if(b==10)break;if(b!=13)bytes.Add((byte)b);if(bytes.Count>maximum)throw new IOException("Header too long");}
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
        void Serve(TcpClient client)
        {
            using(client)try
            {
                client.ReceiveTimeout=2000;client.SendTimeout=2000;var stream=client.GetStream();
                var first=Line(stream).Split(' ');if(first.Length!=3)return;
                string method=first[0],path=first[1].Split('?')[0];var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                for(int i=0;i<24;i++){string line=Line(stream);if(line.Length==0)break;int colon=line.IndexOf(':');if(colon>0)headers[line.Substring(0,colon).Trim()]=line.Substring(colon+1).Trim();if(i==23)throw new IOException("Too many headers");}
                if(!headers.TryGetValue("Host",out string host)||!IPAddress.TryParse(host.Split(':')[0],out _)){Reply(stream,403,"text/plain","Use the Quest IP address");return;}
                if(headers.TryGetValue("Origin",out string origin)&&origin!="http://"+host){Reply(stream,403,"text/plain","Origin rejected");return;}
                if(method=="GET"&&path=="/"){Reply(stream,200,"text/html; charset=utf-8",html);return;}
                int length=0;if(headers.TryGetValue("Content-Length",out string number)&&(!int.TryParse(number,out length)||length<0||length>2048)){Reply(stream,413,"text/plain","Body too large");return;}
                byte[] body=new byte[length];int offset=0;while(offset<length){int read=stream.Read(body,offset,length-offset);if(read==0)return;offset+=read;}
                string json=Encoding.UTF8.GetString(body);
                if(method=="POST"&&path=="/api/pair")
                {
                    bool allowed;lock(pairLock){if((DateTime.UtcNow-pairWindow).TotalSeconds>60){pairAttempts=0;pairWindow=DateTime.UtcNow;}allowed=++pairAttempts<=10;}
                    if(!allowed){Reply(stream,429,"application/json","{\"ok\":false}");return;}
                    // Parse this small authentication payload without touching Unity APIs on a socket thread.
                    string supplied="";try{int c=json.IndexOf("\"code\"",StringComparison.Ordinal);int colon=json.IndexOf(':',c+6);int q=json.IndexOf('"',colon+1);int end=json.IndexOf('"',q+1);if(c>=0&&colon>=0&&q>=0&&end>q)supplied=json.Substring(q+1,end-q-1);}catch{}
                    if(supplied!=PairCode){Reply(stream,401,"application/json","{\"ok\":false}");return;}
                    paired=true;Interlocked.Exchange(ref lastPoll,DateTime.UtcNow.Ticks);Reply(stream,200,"application/json","{\"ok\":true,\"key\":\""+Key+"\"}");return;
                }
                if(!headers.TryGetValue("X-Naispo-Key",out string key)||key!=Key){Reply(stream,401,"application/json","{\"ok\":false}");return;}
                if(method=="GET"&&path=="/api/status"){Interlocked.Exchange(ref lastPoll,DateTime.UtcNow.Ticks);Reply(stream,200,"application/json; charset=utf-8",status);return;}
                if(method=="POST"&&path=="/api/control")
                {
                    if(queue.Count>=8){Reply(stream,429,"application/json","{\"ok\":false}");return;}
                    var pending=new Pending{body=json,deadline=DateTime.UtcNow.AddSeconds(2)};queue.Enqueue(pending);
                    if(pending.result.Task.Wait(2400))Reply(stream,200,"application/json; charset=utf-8",pending.result.Task.Result);
                    else Reply(stream,503,"application/json","{\"ok\":false}");return;
                }
                Reply(stream,404,"text/plain","Not found");
            }catch{}
        }
        static void Reply(NetworkStream stream,int code,string type,string body)=>Reply(stream,code,type,Encoding.UTF8.GetBytes(body));
        static void Reply(NetworkStream stream,int code,string type,byte[] body)
        {
            string reason=code==200?"OK":"Request rejected";
            var header=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+reason+"\r\nContent-Type: "+type+"\r\nContent-Length: "+body.Length+"\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'\r\nConnection: close\r\n\r\n");
            stream.Write(header,0,header.Length);stream.Write(body,0,body.Length);
        }
        void OnDestroy(){running=false;try{listener?.Stop();}catch{}while(queue.TryDequeue(out var pending))pending.result.TrySetResult("{\"ok\":false}");}
    }
}
