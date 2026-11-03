using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

// One-shot game-thread join submission through the native mutex-protected UI command queue.
sealed class NativeJoinBridge : IDisposable {
    readonly JoinGame game;
    internal const long EntryRva=0x261210;
    internal static readonly byte[] Original={0x55,0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x56,0x57,0x53,0x48,0x81,0xec,0xa8,0x02,0,0};
    long entry,cave;byte[] patch;
    [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr address,UIntPtr size,uint type,uint protect);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualFreeEx(IntPtr h,IntPtr address,UIntPtr size,uint type);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualProtectEx(IntPtr h,IntPtr address,UIntPtr size,uint protect,out uint old);
    [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr h,IntPtr address,UIntPtr size);
    [DllImport("ntdll.dll")]static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")]static extern int NtResumeProcess(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetThreadContext(IntPtr thread,IntPtr context);
    internal NativeJoinBridge(JoinGame game){this.game=game;entry=game.Process.MainModule.BaseAddress.ToInt64()+EntryRva;}
    internal static byte[] Jump(long destination,int length){var b=new byte[length];for(int i=0;i<length;i++)b[i]=0x90;b[0]=0xff;b[1]=0x25;b[2]=b[3]=b[4]=b[5]=0;Array.Copy(BitConverter.GetBytes(destination),0,b,6,8);return b;}
    internal static byte[] Stub(long allocation,long entry,long module){
        var code=(byte[])JoinBridgeBytes.Code.Clone();long[] addresses={allocation+4096,module+0x2200,module+0x21c0,module+0x24ed80,module+0x21d0};
        for(int i=0;i<addresses.Length;i++)foreach(int at in JoinBridgeBytes.Relocations[i])Array.Copy(BitConverter.GetBytes(addresses[i]),0,code,at,8);
        var b=new byte[code.Length+Original.Length+14];Array.Copy(code,b,code.Length);Array.Copy(Original,0,b,code.Length,Original.Length);Array.Copy(Jump(entry+Original.Length,14),0,b,code.Length+Original.Length,14);return b;
    }
    internal long Data {get{return cave+4096;}}
    static bool Same(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    void Write(long a,byte[] b){UIntPtr n;if(!Native.WriteProcessMemory(game.Handle,new IntPtr(a),b,(UIntPtr)b.Length,out n)||n.ToUInt64()!=(ulong)b.Length)throw new Exception("Join bridge memory write failed.");}
    void Exchange(byte[] expected,byte[] next) {
        // Never change executable bytes while a game thread can execute them.
        for(int attempt=0;attempt<40;attempt++){
            if(NtSuspendProcess(game.Handle)!=0)throw new Exception("Windows could not pause the game briefly for the join bridge update.");
            bool unsafePc=false;IntPtr allocation=IntPtr.Zero;
            try{
                allocation=Marshal.AllocHGlobal(1248);long aligned=(allocation.ToInt64()+15)&~15L;var context=new IntPtr(aligned);
                game.Process.Refresh();
                foreach(ProcessThread t in game.Process.Threads){
                    IntPtr th=Native.OpenThread(0x48,false,t.Id);if(th==IntPtr.Zero)throw new Exception("A game thread could not be checked; no code changed.");
                    try{
                        Marshal.Copy(new byte[1232],0,context,1232);Marshal.WriteInt32(context,48,0x100001);
                        if(!GetThreadContext(th,context))throw new Exception("A game thread position could not be checked; no code changed.");
                        long pc=Marshal.ReadInt64(context,248);
                        if((pc>=entry&&pc<entry+Original.Length)||(cave!=0&&pc>=cave&&pc<cave+4096))unsafePc=true;
                    }finally{Native.CloseHandle(th);}
                }
                if(!unsafePc&&!(cave!=0&&BitConverter.ToUInt32(game.Read(cave+4096,4),0)==2)){
                    if(!Same(game.Read(entry,expected.Length),expected))throw new Exception("Game command instructions differ. Join bridge stopped.");
                    uint old;if(!VirtualProtectEx(game.Handle,new IntPtr(entry),(UIntPtr)next.Length,0x40,out old))throw new Exception("Cannot update join bridge instructions.");
                    try{Write(entry,next);FlushInstructionCache(game.Handle,new IntPtr(entry),(UIntPtr)next.Length);}
                    catch{Write(entry,expected);FlushInstructionCache(game.Handle,new IntPtr(entry),(UIntPtr)expected.Length);throw;}
                    finally{uint unused;VirtualProtectEx(game.Handle,new IntPtr(entry),(UIntPtr)next.Length,old,out unused);}
                    return;
                }
            }finally{if(allocation!=IntPtr.Zero)Marshal.FreeHGlobal(allocation);NtResumeProcess(game.Handle);}
            Thread.Sleep(2);
        }
        throw new Exception("The game command poll stayed busy. No join hook was changed.");
    }
    void Install(){
        if(!Same(game.Read(entry,game.Expected.Length),game.Expected))throw new Exception("The live game command poll differs from the checked executable. Close any older companion normally before connecting.");
        if(!Same(game.Read(entry,Original.Length),Original))throw new Exception("The game command poll entry is incompatible.");
        cave=VirtualAllocEx(game.Handle,IntPtr.Zero,(UIntPtr)8192,0x3000,4).ToInt64();if(cave==0)throw new Exception("Cannot allocate the join bridge control.");
        try{
            Write(cave,Stub(cave,entry,game.Base));Write(cave+4096,new byte[4096]);uint old;
            if(!VirtualProtectEx(game.Handle,new IntPtr(cave),(UIntPtr)4096,0x20,out old))throw new Exception("Cannot prepare join bridge control.");
            FlushInstructionCache(game.Handle,new IntPtr(cave),(UIntPtr)4096);patch=Jump(cave,Original.Length);Exchange(Original,patch);
        }catch{VirtualFreeEx(game.Handle,new IntPtr(cave),UIntPtr.Zero,0x8000);cave=0;throw;}
    }
    internal void Request(string code,string password,bool send){
        ValidateInput(code,password);byte[] key=System.Text.Encoding.ASCII.GetBytes(code),secret=new System.Text.UTF8Encoding(false,true).GetBytes(password??"");
        try{
            Install();var data=new byte[4096];Array.Copy(BitConverter.GetBytes(send?1:0),0,data,4,4);Array.Copy(BitConverter.GetBytes(key.Length),0,data,40,4);Array.Copy(BitConverter.GetBytes(secret.Length),0,data,44,4);Array.Copy(key,0,data,64,key.Length);Array.Copy(secret,0,data,80,secret.Length);Write(Data,data);Array.Clear(data,0,data.Length);
            Write(Data,BitConverter.GetBytes(1));var watch=Stopwatch.StartNew();
            while(watch.ElapsedMilliseconds<10000){uint status=BitConverter.ToUInt32(game.Read(Data,4),0);if(status==3||status==4)return;if(status==5)throw new Exception("The game could not allocate the join request. No join was sent.");Thread.Sleep(10);}
            throw new Exception("The game did not acknowledge the join request. No automatic retry was made; check the game before trying again.");
        }finally{Array.Clear(secret,0,secret.Length);}
    }
    internal static void ValidateInput(string code,string password){
        if(!System.Text.RegularExpressions.Regex.IsMatch(code??"","\\A[A-Z0-9]{5,8}\\z"))throw new ArgumentException("Invalid room code.");
        if(new System.Text.UTF8Encoding(false,true).GetByteCount(password??"")>128)throw new ArgumentException("Password exceeds 128 UTF-8 bytes.");
    }
    public void Dispose(){
        if(cave==0)return;
        if(game.Process.HasExited){cave=0;return;}
        var pending=Stopwatch.StartNew();while(BitConverter.ToUInt32(game.Read(Data,4),0)==2&&pending.ElapsedMilliseconds<5000)Thread.Sleep(10);
        Exchange(patch,Original);Write(cave+4096,new byte[4096]);VirtualFreeEx(game.Handle,new IntPtr(cave),UIntPtr.Zero,0x8000);cave=0;
    }
}
