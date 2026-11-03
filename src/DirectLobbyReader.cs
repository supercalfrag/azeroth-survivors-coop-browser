using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

sealed class DirectLobbyReader : IDisposable {
 const string Endpoint="wss://wow.aethus.pl/coop/lobby/";
 readonly string version;ClientWebSocket socket;
 internal DirectLobbyReader(string version){if(!Regex.IsMatch(version??"","^[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}$"))throw new Exception("Enter the game version, for example 0.139.0.");this.version=version;}
 internal static string DetectVersion(){
  try{
   string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AzerothSurvivors.exe");
   var games=System.Diagnostics.Process.GetProcessesByName("AzerothSurvivors");
   try{if(games.Length==1)path=games[0].MainModule.FileName;}finally{foreach(var game in games)game.Dispose();}
   byte[] b=File.ReadAllBytes(path),needle=Encoding.ASCII.GetBytes("}).version");
   for(int i=0;i<b.Length-needle.Length-8;i++){
    if(b[i]!=needle[0])continue;bool same=true;for(int j=1;j<needle.Length;j++)if(b[i+j]!=needle[j]){same=false;break;}if(!same)continue;
    int start=i+needle.Length,end=start;while(end<b.Length&&end<start+20&&(b[end]==46||b[end]>=48&&b[end]<=57))end++;
    string found=Encoding.ASCII.GetString(b,start,end-start);if(Regex.IsMatch(found,"^[0-9]+\\.[0-9]+\\.[0-9]+$"))return found;
   }
  }catch{}
  return "0.139.0";
 }
 internal static byte[] Hello(string version){using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8)){
  w.Write((byte)1);w.Write((uint)3);byte[] text=Encoding.UTF8.GetBytes(version);w.Write((uint)text.Length);w.Write(text);w.Write((uint)0);w.Write((byte)1);return m.ToArray();
 }}
 internal List<Lobby> ReadRooms(){
  try{using(var cancel=new CancellationTokenSource(10000)){
   if(socket==null||socket.State!=WebSocketState.Open){Dispose();socket=new ClientWebSocket();socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(20);socket.ConnectAsync(new Uri(Endpoint),cancel.Token).GetAwaiter().GetResult();Send(Hello(version),cancel.Token);}
   Send(new byte[]{2},cancel.Token);
   for(int attempt=0;attempt<8;attempt++){
    using(var m=new MemoryStream()){
     var buffer=new byte[16384];WebSocketReceiveResult r;
     do{r=socket.ReceiveAsync(new ArraySegment<byte>(buffer),cancel.Token).GetAwaiter().GetResult();if(r.MessageType==WebSocketMessageType.Close)throw new Exception("The lobby service closed the connection. Check the game version and refresh again.");if(r.MessageType!=WebSocketMessageType.Binary)throw new Exception("Unexpected lobby response format.");if(m.Length+r.Count>2097152)throw new Exception("Lobby response exceeds the supported size.");m.Write(buffer,0,r.Count);}while(!r.EndOfMessage);
     byte[] data=m.ToArray();if(data.Length>0&&data[0]==20)return Decode(data);
     if(data.Length==0||data[0]!=10)throw new Exception("The lobby service returned an unrecognized reply ("+(data.Length==0?"empty":data[0].ToString())+").");
    }
   }
   throw new Exception("No lobby list was returned.");
  }}catch(OperationCanceledException){Dispose();throw new Exception("The lobby service did not respond in 10 seconds. Refresh to reconnect.");}catch{Dispose();throw;}
 }
 void Send(byte[] data,CancellationToken token){socket.SendAsync(new ArraySegment<byte>(data),WebSocketMessageType.Binary,true,token).GetAwaiter().GetResult();}
 static string ReadText(BinaryReader r){uint n=r.ReadUInt32();if(n>16384||n>r.BaseStream.Length-r.BaseStream.Position)throw new Exception("Invalid lobby text length.");byte[] b=r.ReadBytes((int)n);return new UTF8Encoding(false,true).GetString(b);}
 static readonly Dictionary<string,string> Zones=new Dictionary<string,string>{{"elwynn","Elwynn Forest"},{"duskwood","Duskwood"},{"wsg","Warsong Gulch"},{"ungoro","Un'Goro Crater"},{"ab","Arathi Basin"},{"feralas","Feralas"},{"gurubashi","Gurubashi Arena"},{"arena","Gurubashi Arena"},{"sm","Scarlet Monastery"},{"av","Alterac Valley"},{"brm","Blackrock Mountain"},{"aq","Gates of Ahn'Qiraj"},{"naxx","Naxxramas"}};
 internal static List<Lobby> Decode(byte[] data){
  try{using(var m=new MemoryStream(data,false))using(var r=new BinaryReader(m,Encoding.UTF8)){
   if(r.ReadByte()!=20)throw new Exception("Not a lobby-list response.");uint count=r.ReadUInt32();if(count>256)throw new Exception("The server returned more than 256 rooms.");
   var rows=new List<Lobby>();var codes=new HashSet<string>();
   for(int i=0;i<count;i++){
    string code=ReadText(r),name=ReadText(r),description=ReadText(r),host=ReadText(r);byte cls=r.ReadByte();string zone=ReadText(r);int players=r.ReadByte();byte password=r.ReadByte(),open=r.ReadByte();string country=ReadText(r);uint classes=r.ReadUInt32();
    if(!Regex.IsMatch(code,"^[A-Z0-9]{5,8}$")||!codes.Add(code)||!ClassIcons.Names.ContainsKey(cls)||players>5||password>1||open>1||classes>5||!(country==""||Regex.IsMatch(country,"^[A-Z]{2}$")))throw new Exception("Invalid lobby fields.");
    byte[] ids=r.ReadBytes((int)classes);if(ids.Length!=classes||ids.Any(id=>!ClassIcons.Names.ContainsKey(id)))throw new Exception("Invalid party class roster.");string title=ReadText(r),label; if(!Zones.TryGetValue(zone,out label))label=zone;
    rows.Add(new Lobby{Code=code,Name=name,Description=description,Host=host,Class=ClassIcons.Names[cls],Zone=label,Players=players,Password=password!=0,Opened=false,EligibilityKnown=false,Country=country,ClassIds=ids});
   }
   // Protocol v3 appends one capacity per room, followed by one difficulty per room.
   if(m.Length-m.Position!=count*2)throw new Exception("Lobby protocol changed: unexpected trailing fields.");
   foreach(var row in rows){row.Maximum=r.ReadByte();if(row.Maximum<1||row.Maximum>5||row.Players>row.Maximum)throw new Exception("Invalid lobby capacity.");}
   foreach(var row in rows){int heroic=r.ReadByte();if(heroic>50)throw new Exception("Invalid difficulty.");row.Heroic=heroic;row.Difficulty=heroic==0?"Normal":"Heroic "+Roman(heroic);}
   return rows;
  }}catch(Exception e){throw new Exception("The lobby response failed validation. "+e.Message,e);}
 }
 static string Roman(int n){if(n>20)return n.ToString();string s="";while(n>=10){s+="X";n-=10;}if(n==9)return s+"IX";if(n>=5){s+="V";n-=5;}if(n==4)return s+"IV";return s+new string('I',n);}
 public void Dispose(){if(socket!=null){socket.Dispose();socket=null;}}
}
