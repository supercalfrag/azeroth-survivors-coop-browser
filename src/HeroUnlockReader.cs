using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Text;

sealed class HeroUnlockState {
 internal string Name;internal byte ClassId;internal int ZonesOpen;
 internal readonly HashSet<int> NormalClears=new HashSet<int>();internal readonly Dictionary<int,int> HeroicClears=new Dictionary<int,int>();
 internal string Signature {get{return Name+"/"+ClassId+"/"+ZonesOpen+"/"+string.Join(",",NormalClears.OrderBy(x=>x))+"/"+string.Join(",",HeroicClears.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value));}}
 internal static readonly string[] ZoneNames={"Elwynn Forest","Duskwood","Warsong Gulch","Un'Goro Crater","Arathi Basin","Feralas","Gurubashi Arena","Scarlet Monastery","Alterac Valley","Blackrock Mountain","Gates of Ahn'Qiraj","Naxxramas"};
 internal bool? CanEnter(Lobby room){int zone=Array.IndexOf(ZoneNames,room.Zone);int tier=room.Heroic;if(zone<0||tier<0||tier>5)return null;if(zone>=ZonesOpen)return false;if(tier==0)return true;if(!NormalClears.Contains(zone))return false;int best;HeroicClears.TryGetValue(zone,out best);return tier<=Math.Min(5,best+1);}
 internal void Apply(IEnumerable<Lobby> rooms){foreach(var room in rooms){bool? can=CanEnter(room);room.EligibilityKnown=can.HasValue;room.Opened=can==true;}}
}

// This reader has no process-write, allocation, suspension or hook APIs.
sealed class HeroUnlockReader : IDisposable {
 [StructLayout(LayoutKind.Sequential)]struct MBI {internal IntPtr Base,Allocation;internal uint AllocationProtect;internal ushort Partition;internal UIntPtr Size;internal uint State,Protect,Type;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")]static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] buffer,UIntPtr size,out UIntPtr got);
 [DllImport("kernel32.dll")]static extern UIntPtr VirtualQueryEx(IntPtr process,IntPtr address,out MBI info,UIntPtr size);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 Process process;IntPtr handle;long module,moduleEnd,heroSlot,profileSlot;bool located;readonly List<Tuple<long,long>> codeRanges=new List<Tuple<long,long>>();
 static ulong U(byte[] b,int at){return BitConverter.ToUInt64(b,at);}static bool Pointer(ulong x){return x>=0x10000&&x<0x7ffffffeffffUL;}
 byte[] Read(long address,int count){if(address<0x10000||count<0||count>2097152)throw new Exception("Invalid hero memory range.");var b=new byte[count];UIntPtr got;if(!ReadProcessMemory(handle,new IntPtr(address),b,(UIntPtr)count,out got)||got.ToUInt64()!=(ulong)count)throw new Exception("The game's hero data changed. Refresh to read it again.");return b;}
 void Connect(){
  if(process!=null&&!process.HasExited)return;Dispose();var games=Process.GetProcessesByName("AzerothSurvivors");if(games.Length!=1){foreach(var game in games)game.Dispose();throw new Exception("Open one copy of Azeroth Survivors to check the selected hero.");}process=games[0];
  module=process.MainModule.BaseAddress.ToInt64();moduleEnd=module+process.MainModule.ModuleMemorySize;handle=OpenProcess(0x410,false,process.Id);if(handle==IntPtr.Zero){Dispose();throw new Exception("Windows did not allow read-only hero access.");}
  var headers=Read(module,4096);int pe=BitConverter.ToInt32(headers,60),count=BitConverter.ToUInt16(headers,pe+6),table=pe+24+BitConverter.ToUInt16(headers,pe+20);if(count>64||table+count*40>headers.Length)throw new Exception("Invalid game code sections.");codeRanges.Clear();
  for(int i=0;i<count;i++){int at=table+i*40;if((BitConverter.ToUInt32(headers,at+36)&0x20000000)==0)continue;long first=module+BitConverter.ToUInt32(headers,at+12),last=first+Math.Max(BitConverter.ToUInt32(headers,at+8),BitConverter.ToUInt32(headers,at+16));if(first>=module&&last<=moduleEnd)codeRanges.Add(Tuple.Create(first,last));}

 }
 bool CodePointer(ulong pointer){return codeRanges.Any(r=>pointer>=(ulong)r.Item1&&pointer<(ulong)r.Item2);}
 static bool ValidClass(byte id){return ClassIcons.Names.ContainsKey(id);}
 string Text(byte[] header,int at){ulong cap=U(header,at),ptr=U(header,at+8),n=U(header,at+16);if(n<1||n>128||n>cap||cap>4096||!Pointer(ptr))throw new Exception("Invalid selected-hero text.");string text=new UTF8Encoding(false,true).GetString(Read((long)ptr,(int)n));if(text.Any(char.IsControl))throw new Exception("Invalid selected-hero name.");return text;}
 long Slot(long address,int size){var b=Read(address-16,40);if(U(b,0)!=8||U(b,8)!=(ulong)size||!CodePointer(U(b,24))||!Pointer(U(b,16)))throw new Exception("Hero resource ownership changed.");return (long)U(b,16);}
 byte[] Vector(byte[] profile,int at,int stride,int maximum){ulong cap=U(profile,at),ptr=U(profile,at+8),n=U(profile,at+16);if(n>cap||cap>(ulong)maximum||(n>0&&!Pointer(ptr)))throw new Exception("Invalid hero progression list.");return n==0?new byte[0]:Read((long)ptr,checked((int)n*stride));}
 HeroUnlockState Snapshot(long hs,long ps){
  long hero=Slot(hs,64),profile=Slot(ps,0x298);byte[] h=Read(hero,64),p=Read(profile,0x298);
  if(!ValidClass(h[48])||h[49]<1||h[49]>11||h[50]>1)throw new Exception("Invalid selected-hero identity.");
  var state=new HeroUnlockState{Name=Text(h,0),ClassId=h[48],ZonesOpen=1};string zoneKey=Text(h,24);if(!new[]{"elwynn","duskwood","wsg","ungoro","ab","feralas","arena","gurubashi","sm","av","brm","aq","naxx"}.Contains(zoneKey))throw new Exception("Selected hero zone is not recognized.");
  byte[] zones=Vector(p,0x18,16,64),normal=Vector(p,0xd8,16,128),heroic=Vector(p,0x1c8,16,128);var classes=new HashSet<byte>();
  for(int i=0;i<zones.Length;i+=16){byte cl=zones[i];ulong n=U(zones,i+8);if(!ValidClass(cl)||n>12||!classes.Add(cl))throw new Exception("Invalid class zone progress.");if(cl==state.ClassId)state.ZonesOpen=(int)Math.Max(1,n);}
  var normalKeys=new HashSet<string>();for(int i=0;i<normal.Length;i+=16){byte cl=normal[i];ulong zone=U(normal,i+8);if(!ValidClass(cl)||zone>11||!normalKeys.Add(cl+":"+zone))throw new Exception("Invalid Normal clear list.");if(cl==state.ClassId)state.NormalClears.Add((int)zone);}
  var heroicKeys=new HashSet<string>();for(int i=0;i<heroic.Length;i+=16){ulong zone=U(heroic,i);byte cl=heroic[i+8],tier=heroic[i+9];if(!ValidClass(cl)||zone>11||tier<1||tier>5||!heroicKeys.Add(cl+":"+zone))throw new Exception("Invalid Heroic clear list.");int best;state.HeroicClears.TryGetValue((int)zone,out best);state.HeroicClears[(int)zone]=Math.Max(best,tier);}
  if(!Vector(p,0x18,16,64).SequenceEqual(zones)||!Vector(p,0xd8,16,128).SequenceEqual(normal)||!Vector(p,0x1c8,16,128).SequenceEqual(heroic))throw new Exception("Hero progression changed during the read. Refresh again.");
  if(Slot(hs,64)!=hero||Slot(ps,0x298)!=profile||!Read(hero,64).SequenceEqual(h)||!Read(profile,0x298).SequenceEqual(p))throw new Exception("The hero changed during the read. Refresh again.");
  return state;
 }
 void Locate(){
  var heroes=new Dictionary<long,List<long>>();var profiles=new Dictionary<long,List<long>>();long address=0;MBI info;
  while(address<0x7ffffffeffffL){
   if(VirtualQueryEx(handle,new IntPtr(address),out info,(UIntPtr)Marshal.SizeOf(typeof(MBI)))==UIntPtr.Zero)break;long start=info.Base.ToInt64(),size=(long)info.Size.ToUInt64(),end=start+size;if(end<=address)break;
   uint protect=info.Protect&255;
   if(info.State==0x1000&&info.Type==0x20000&&(info.Protect&0x100)==0&&(protect==4||protect==8||protect==0x40||protect==0x80)&&size<=64L*1048576){
    for(long off=0;off<size;off+=1048576){int count=(int)Math.Min(1048576+48,size-off);byte[] b=new byte[count];UIntPtr got;ReadProcessMemory(handle,new IntPtr(start+off),b,(UIntPtr)count,out got);int n=(int)got.ToUInt64();
     for(int i=16;i+24<=n;i+=8){ulong drop=U(b,i+8),length=U(b,i-8),ptr=U(b,i);if(U(b,i-16)!=8||!Pointer(ptr))continue;Dictionary<long,List<long>> target=null;
      if(!CodePointer(drop))continue;if(length==64)target=heroes;else if(length==0x298)target=profiles;else continue;
      List<long> slots;if(!target.TryGetValue((long)ptr,out slots)){slots=new List<long>();target[(long)ptr]=slots;}slots.Add(start+off+i);
     }
    }
   }
   address=end;
  }
  var candidates=new List<Tuple<long,long,HeroUnlockState>>();foreach(var hero in heroes)foreach(var profile in profiles){try{var state=Snapshot(hero.Value[0],profile.Value[0]);candidates.Add(Tuple.Create(hero.Value[0],profile.Value[0],state));}catch{}}
  if(candidates.Count!=1)throw new Exception("Could not identify one active selected hero and profile. Keep the main screen open and refresh.");heroSlot=candidates[0].Item1;profileSlot=candidates[0].Item2;located=true;
 }
 internal HeroUnlockState ReadSelected(){Connect();if(!located)Locate();try{return Snapshot(heroSlot,profileSlot);}catch{located=false;Locate();return Snapshot(heroSlot,profileSlot);}}
 public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}if(process!=null){process.Dispose();process=null;}located=false;heroSlot=profileSlot=0;codeRanges.Clear();}
}
