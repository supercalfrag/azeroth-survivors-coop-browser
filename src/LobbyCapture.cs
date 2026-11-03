using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

// Exact-build capture detour. Copies the current lobby view into a private
// buffer; never changes room data or joins/hosts a room.
sealed class LobbyCapture : IDisposable {
    readonly LobbyProcess game;
    internal const long EntryRva=0xbb140;
    internal static readonly byte[] Original={0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x56,0x57,0x55,0x53,0x48,0x81,0xec,0x08,0x04,0,0};
    long entry,cave;byte[] patch;
    [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr address,UIntPtr size,uint type,uint protect);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualFreeEx(IntPtr h,IntPtr address,UIntPtr size,uint type);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualProtectEx(IntPtr h,IntPtr address,UIntPtr size,uint protect,out uint old);
    [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr h,IntPtr address,UIntPtr size);
    [DllImport("ntdll.dll")]static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")]static extern int NtResumeProcess(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetThreadContext(IntPtr thread,IntPtr context);
    internal LobbyCapture(LobbyProcess game){this.game=game;entry=game.Process.MainModule.BaseAddress.ToInt64()+game.Layout.Rva;}
    internal static byte[] Jump(long destination,int length){var b=new byte[length];for(int i=0;i<length;i++)b[i]=0x90;b[0]=0xff;b[1]=0x25;b[2]=b[3]=b[4]=b[5]=0;Array.Copy(BitConverter.GetBytes(destination),0,b,6,8);return b;}
    internal static byte[] Stub(long allocation,long entry){
        byte[] code=(byte[])LobbyCaptureBytes.Code.Clone();Array.Copy(BitConverter.GetBytes(allocation+4096),0,code,LobbyCaptureBytes.PointerOffset,8);
        var b=new byte[code.Length+Original.Length+14];Array.Copy(code,b,code.Length);Array.Copy(Original,0,b,code.Length,Original.Length);Array.Copy(Jump(entry+Original.Length,14),0,b,code.Length+Original.Length,14);return b;
    }
    internal long Data {get{return cave+4096;}}
    static bool Same(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    void Write(long a,byte[] b){UIntPtr n;if(!Native.WriteProcessMemory(game.Handle,new IntPtr(a),b,(UIntPtr)b.Length,out n)||n.ToUInt64()!=(ulong)b.Length)throw new Exception("Lobby-reader memory write failed.");}
    void Exchange(byte[] expected,byte[] next) {
        // Never change executable bytes while a game thread can execute them.
        for(int attempt=0;attempt<40;attempt++){
            if(NtSuspendProcess(game.Handle)!=0)throw new Exception("Windows could not pause the game briefly for the lobby-reader update.");
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
                if(!unsafePc){
                    if(!Same(game.Read(entry,expected.Length),expected))throw new Exception("Lobby-renderer instructions differ. Lobby reader stopped.");
                    uint old;if(!VirtualProtectEx(game.Handle,new IntPtr(entry),(UIntPtr)next.Length,0x40,out old))throw new Exception("Cannot update lobby-reader instructions.");
                    try{Write(entry,next);FlushInstructionCache(game.Handle,new IntPtr(entry),(UIntPtr)next.Length);}
                    catch{Write(entry,expected);FlushInstructionCache(game.Handle,new IntPtr(entry),(UIntPtr)expected.Length);throw;}
                    finally{uint unused;VirtualProtectEx(game.Handle,new IntPtr(entry),(UIntPtr)next.Length,old,out unused);}
                    return;
                }
            }finally{if(allocation!=IntPtr.Zero)Marshal.FreeHGlobal(allocation);NtResumeProcess(game.Handle);}
            Thread.Sleep(2);
        }
        throw new Exception("The lobby view stayed busy. No lobby hook was changed.");
    }
    internal void Install(){
        if(!Same(game.Read(entry,game.Layout.Code.Length),game.Layout.Code))throw new Exception("The live lobby renderer differs from the checked executable. Close any older companion normally before connecting.");
        if(!Same(game.Read(entry,Original.Length),Original))throw new Exception("The lobby renderer entry is incompatible.");
        cave=VirtualAllocEx(game.Handle,IntPtr.Zero,(UIntPtr)2097152,0x3000,4).ToInt64();if(cave==0)throw new Exception("Cannot allocate the lobby-reader control.");
        try{
            Write(cave,Stub(cave,entry));Write(cave+4096,new byte[64]);uint old;
            if(!VirtualProtectEx(game.Handle,new IntPtr(cave),(UIntPtr)4096,0x20,out old))throw new Exception("Cannot prepare lobby-reader control.");
            FlushInstructionCache(game.Handle,new IntPtr(cave),(UIntPtr)4096);patch=Jump(cave,Original.Length);Exchange(Original,patch);
        }catch{VirtualFreeEx(game.Handle,new IntPtr(cave),UIntPtr.Zero,0x8000);cave=0;throw;}
    }
    public void Dispose(){
        if(cave==0)return;
        Exchange(patch,Original);VirtualFreeEx(game.Handle,new IntPtr(cave),UIntPtr.Zero,0x8000);cave=0;
    }
}
