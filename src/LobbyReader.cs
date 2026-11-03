using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.InteropServices;

static class Native {
 [DllImport("kernel32.dll",SetLastError=true)]internal static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")]internal static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll")]internal static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll")]internal static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr written);
 [DllImport("kernel32.dll")]internal static extern IntPtr OpenThread(uint access,bool inherit,int id);
}
sealed class LobbyProcess:IDisposable {
 internal Process Process;internal IntPtr Handle;internal LobbyCompatibility.Match Layout;
 internal LobbyProcess(){
  var ps=System.Diagnostics.Process.GetProcessesByName("AzerothSurvivors");if(ps.Length!=1)throw new Exception("Open one copy of Azeroth Survivors and its Co-op lobby list first.");Process=ps[0];
  Layout=LobbyCompatibility.Find(File.ReadAllBytes(Process.MainModule.FileName));
  Handle=Native.OpenProcess(0xc38,false,Process.Id);if(Handle==IntPtr.Zero)throw new Exception("Windows did not allow the lobby reader to attach. Run both apps with the same privileges.");
 }
 internal byte[] Read(long address,int n){byte[] b=new byte[n];UIntPtr got;if(!Native.ReadProcessMemory(Handle,new IntPtr(address),b,(UIntPtr)n,out got)||got.ToUInt64()!=(ulong)n)throw new Exception("The game closed or its lobby view changed. Open the lobby list and Refresh again.");return b;}
 public void Dispose(){if(Handle!=IntPtr.Zero){Native.CloseHandle(Handle);Handle=IntPtr.Zero;}if(Process!=null)Process.Dispose();}
}
sealed class LobbyReader:IDisposable {
 LobbyProcess process;LobbyCapture capture;readonly Semaphore mutex;bool owns;
 internal LobbyReader(){mutex=new Semaphore(1,1,"Local\\AzerothCoopBrowserCapture");owns=mutex.WaitOne(0);if(!owns){mutex.Dispose();throw new Exception("Another companion browser is already connected.");}}
 internal static LobbyReader Connect(){var r=new LobbyReader();try{r.process=new LobbyProcess();r.capture=new LobbyCapture(r.process);r.capture.Install();return r;}catch{r.Dispose();throw;}}
 static string Text(byte[] b,int row,int index){int off=row+296+index*512;int n=BitConverter.ToInt32(b,off);if(n<0||n>508)throw new Exception("Lobby text failed validation.");return new UTF8Encoding(false,true).GetString(b,off+4,n);}
 internal List<Lobby> ReadRooms(){
  // Observe a new completed render so freed buffers and old snapshots are
  // never mistaken for a live room list.
  ulong initial=BitConverter.ToUInt64(process.Read(capture.Data,8),0);
  UIntPtr written;if(!Native.WriteProcessMemory(process.Handle,new IntPtr(capture.Data+32),BitConverter.GetBytes(1),(UIntPtr)4,out written)||written.ToUInt64()!=4)throw new Exception("Could not request a fresh lobby snapshot.");
  for(int attempt=0;attempt<1500;attempt++){
   Thread.Sleep(10);byte[] head=process.Read(capture.Data,64);ulong seq=BitConverter.ToUInt64(head,0);
   if(seq==0||seq==initial||(seq&1)!=0)continue;
   ulong count=BitConverter.ToUInt64(head,8),total=BitConverter.ToUInt64(head,24);
   if(count>256||total>256)throw new Exception("The game returned more than 256 rooms; browser update required.");
   byte[] b=process.Read(capture.Data+64,checked((int)count*5440));ulong after=BitConverter.ToUInt64(process.Read(capture.Data,8),0);
   if(after!=seq||(after&1)!=0)continue;
   var result=new List<Lobby>();var codes=new HashSet<string>();
   for(int i=0;i<(int)count;i++){
    int row=i*5440;string code=Text(b,row,0);int players=checked((int)BitConverter.ToUInt64(b,row+272)),max=checked((int)BitConverter.ToUInt64(b,row+280));string cc=Text(b,row,8);
    if(!Regex.IsMatch(code,"^[A-Z0-9]{5,8}$")||!codes.Add(code)||players<0||players>5||max<1||max>5||players>max||!(cc==""||Regex.IsMatch(cc,"^[A-Z]{2}$"))||b[row+288]>1||b[row+289]>1)throw new Exception("Live lobby data failed validation. Leave the lobby list visible and refresh again.");
    int classCount=BitConverter.ToInt32(b,row+5424);if(classCount<0||classCount>5)throw new Exception("Party class count failed validation.");
    byte[] classes=new byte[classCount];Array.Copy(b,row+5416,classes,0,classCount);
    foreach(byte id in classes)if(!ClassIcons.Names.ContainsKey(id))throw new Exception("The game returned an unknown party class.");
    result.Add(new Lobby{ClassIds=classes,Code=code,Name=Text(b,row,1),Description=Text(b,row,2),Host=Text(b,row,3),Class=Text(b,row,4),Zone=Text(b,row,5),Difficulty=Text(b,row,7),Country=cc,Players=players,Maximum=max,Opened=b[row+288]!=0,Password=b[row+289]!=0});
   }
   return result;
  }
  throw new Exception("No fresh lobby view captured. Keep Co-op > Lobbies open in the game, then Refresh now.");
 }
 public void Dispose(){try{if(capture!=null){capture.Dispose();capture=null;}}finally{if(process!=null){process.Dispose();process=null;}if(owns){mutex.Release();owns=false;mutex.Dispose();}}}
}
