using System;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Text;
static class NativeJoinTests {
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAlloc(IntPtr p,UIntPtr size,uint type,uint protect);
 [DllImport("kernel32.dll")]static extern bool VirtualFree(IntPtr p,UIntPtr size,uint type);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate IntPtr Allocate(ulong size,ulong align);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Free(IntPtr pointer,ulong size,ulong align);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Submit(IntPtr queue,IntPtr command);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Shim();
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Tick(IntPtr unused,IntPtr resource);
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 internal static void Run(){
  foreach(string code in new[]{"", "ABC123\n", "abc123", "1234", "ABCDEFGHI"}){bool denied=false;try{NativeJoinBridge.ValidateInput(code,"");}catch(ArgumentException){denied=true;}Check(denied,"Bad join code accepted");}
  NativeJoinBridge.ValidateInput("ABC123","");bool longDenied=false;try{NativeJoinBridge.ValidateInput("ABC123",new string('x',129));}catch(ArgumentException){longDenied=true;}Check(longDenied,"Long password accepted");
  var owned=new HashSet<IntPtr>();int submissions=0;string seenCode=null,seenPassword=null;bool fail=false;
  Allocate allocate=(n,a)=>{if(fail)return IntPtr.Zero;var p=Marshal.AllocHGlobal((int)n);owned.Add(p);return p;};
  Free release=(p,n,a)=>{if(owned.Remove(p))Marshal.FreeHGlobal(p);};Shim shim=()=>{};
  Submit submit=(q,c)=>{submissions++;var code=Marshal.ReadIntPtr(c,16);int cn=(int)Marshal.ReadInt64(c,24);var password=Marshal.ReadIntPtr(c,40);int pn=(int)Marshal.ReadInt64(c,48);byte[] cb=new byte[cn],pb=new byte[pn];Marshal.Copy(code,cb,0,cn);if(pn>0)Marshal.Copy(password,pb,0,pn);seenCode=Encoding.UTF8.GetString(cb);seenPassword=Encoding.UTF8.GetString(pb);release(code,(ulong)cn,1);if(pn>0)release(password,(ulong)pn,1);};
  IntPtr memory=VirtualAlloc(IntPtr.Zero,(UIntPtr)12288,0x3000,0x40),resource=Marshal.AllocHGlobal(16);Check(memory!=IntPtr.Zero,"Own-process native allocation failed");
  try{
   Marshal.WriteIntPtr(resource,resource);long root=memory.ToInt64(),data=root+4096,entry=root+8192;
   var code=(byte[])JoinBridgeBytes.Code.Clone();long[] values={data,Marshal.GetFunctionPointerForDelegate(shim).ToInt64(),Marshal.GetFunctionPointerForDelegate(allocate).ToInt64(),Marshal.GetFunctionPointerForDelegate(submit).ToInt64(),Marshal.GetFunctionPointerForDelegate(release).ToInt64()};
   for(int i=0;i<values.Length;i++)foreach(int at in JoinBridgeBytes.Relocations[i])Array.Copy(BitConverter.GetBytes(values[i]),0,code,at,8);
   Marshal.Copy(code,0,memory,code.Length);Marshal.Copy(NativeJoinBridge.Original,0,new IntPtr(root+code.Length),NativeJoinBridge.Original.Length);var jump=NativeJoinBridge.Jump(entry,14);Marshal.Copy(jump,0,new IntPtr(root+code.Length+NativeJoinBridge.Original.Length),jump.Length);
   byte[] end={0x48,0x81,0xc4,0xa8,0x02,0,0,0x5b,0x5f,0x5e,0x41,0x5c,0x41,0x5d,0x41,0x5e,0x41,0x5f,0x5d,0xc3};Marshal.Copy(end,0,new IntPtr(entry),end.Length);
   var tick=(Tick)Marshal.GetDelegateForFunctionPointer(memory,typeof(Tick));
   foreach(bool send in new[]{false,true}){
    Marshal.Copy(new byte[4096],0,new IntPtr(data),4096);Marshal.WriteInt32(new IntPtr(data),1);Marshal.WriteInt32(new IntPtr(data+4),send?1:0);Marshal.WriteInt32(new IntPtr(data+40),6);Marshal.WriteInt32(new IntPtr(data+44),4);Marshal.Copy(Encoding.ASCII.GetBytes("ABC123"),0,new IntPtr(data+64),6);Marshal.Copy(Encoding.ASCII.GetBytes("test"),0,new IntPtr(data+80),4);
    tick(IntPtr.Zero,resource);Check(Marshal.ReadInt32(new IntPtr(data))==(send?3:4),"Wrong completion state");Check(owned.Count==0,"Native strings leaked");tick(IntPtr.Zero,resource);
   }
   Check(submissions==1&&seenCode=="ABC123"&&seenPassword=="test","Join not submitted exactly once with correct strings");
   fail=true;Marshal.WriteInt32(new IntPtr(data),1);tick(IntPtr.Zero,resource);Check(Marshal.ReadInt32(new IntPtr(data))==5&&submissions==1&&owned.Count==0,"Allocation failure sent a command");
   Console.WriteLine("PASS: native Join stub dry-run, exactly-once queue submission, password/code transfer, cleanup, allocation failure and input validation; own-process fixtures only.");
  }finally{foreach(var p in owned)Marshal.FreeHGlobal(p);Marshal.FreeHGlobal(resource);VirtualFree(memory,UIntPtr.Zero,0x8000);GC.KeepAlive(allocate);GC.KeepAlive(release);GC.KeepAlive(submit);GC.KeepAlive(shim);}
 }
}
