using System;using System.IO;using System.Linq;using System.Diagnostics;using System.Security.Cryptography;using System.Collections.Generic;

// Joining is deliberately independent of lobby-list and hero-reader compatibility.
sealed class JoinGame : IDisposable {
 internal const string SupportedHash="0BB6C557C5F6CFEEA212761A33554F9A5647CE7CB7E03B8A163B95489496E908";
 internal Process Process;internal IntPtr Handle;internal long Base;
 internal byte[] Expected;internal int TargetRva;internal const int PollRva=0x261210,PollLength=6266;
 internal JoinGame(int targetRva=PollRva,int targetLength=PollLength){
  if(!(targetRva==PollRva&&targetLength==PollLength||targetRva==0x2e9900&&targetLength==254))throw new ArgumentException("Unknown bridge target.");TargetRva=targetRva;
  try{
   var games=Process.GetProcessesByName("AzerothSurvivors");if(games.Length!=1){foreach(var g in games)g.Dispose();throw new Exception("Open exactly one copy of Azeroth Survivors before joining.");}Process=games[0];
   byte[] file=File.ReadAllBytes(Process.MainModule.FileName);string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
   if(hash!=SupportedHash)throw new Exception("Joining is not verified for this game update. Lobby browsing and Copy join code still work.");
   Base=Process.MainModule.BaseAddress.ToInt64();Handle=Native.OpenProcess(0xc38,false,Process.Id);if(Handle==IntPtr.Zero)throw new Exception("Windows denied the join bridge access. Run the game and browser with the same privileges.");
   int pe=BitConverter.ToInt32(file,60),head=pe+24+BitConverter.ToUInt16(file,pe+20),count=BitConverter.ToUInt16(file,pe+6);var sections=new List<int[]>();var bounds=new List<int[]>();
   for(int i=0;i<count;i++){int p=head+40*i;int rva=BitConverter.ToInt32(file,p+12),size=BitConverter.ToInt32(file,p+16),raw=BitConverter.ToInt32(file,p+20);sections.Add(new[]{rva,size,raw});if(System.Text.Encoding.ASCII.GetString(file,p,8).TrimEnd('\0')==".pdata")for(int j=raw;j+12<=raw+size;j+=12)bounds.Add(new[]{BitConverter.ToInt32(file,j),BitConverter.ToInt32(file,j+4)});}
   foreach(int rva in new[]{targetRva,0x24ed80,0x1205570,0x2200,0x21c0,0x21d0}){
    int length=rva==targetRva?targetLength:32;var bound=bounds.FirstOrDefault(x=>x[0]==rva);if(bound!=null)length=bound[1]-bound[0];
    var section=sections.FirstOrDefault(x=>rva>=x[0]&&(long)rva+length<=(long)x[0]+x[1]);if(section==null)throw new Exception("Join function is missing.");
    var expected=new byte[length];Array.Copy(file,section[2]+rva-section[0],expected,0,length);if(!Read(Base+rva,length).SequenceEqual(expected))throw new Exception("The game's join code differs from the verified executable. No join sent.");if(rva==targetRva)Expected=expected;
   }
  }catch{Dispose();throw;}
 }
 internal byte[] Read(long address,int n){var b=new byte[n];UIntPtr got;if(!Native.ReadProcessMemory(Handle,new IntPtr(address),b,(UIntPtr)n,out got)||got.ToUInt64()!=(ulong)n)throw new Exception("The game closed or its join data changed.");return b;}
 public void Dispose(){if(Handle!=IntPtr.Zero){Native.CloseHandle(Handle);Handle=IntPtr.Zero;}if(Process!=null){Process.Dispose();Process=null;}}
}
