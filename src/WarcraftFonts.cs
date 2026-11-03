using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Text;using System.IO;using System.Linq;using System.Reflection;using System.Runtime.InteropServices;

// Fonts are private to this process; no system installation is performed.
// Public builds work without game fonts and fall back to a system serif.
static class WarcraftFonts {
 static readonly PrivateFontCollection collection=new PrivateFontCollection();
 static readonly List<IntPtr> buffers=new List<IntPtr>(),handles=new List<IntPtr>();
 [DllImport("gdi32.dll")]static extern IntPtr AddFontMemResourceEx(IntPtr data,uint size,IntPtr reserved,ref uint count);
 [DllImport("gdi32.dll")]static extern bool RemoveFontMemResourceEx(IntPtr font);
 static WarcraftFonts(){
  Load("FRIZQT__.TTF");Load("MORPHEUS.TTF");
  AppDomain.CurrentDomain.ProcessExit+=delegate{collection.Dispose();foreach(var handle in handles)RemoveFontMemResourceEx(handle);foreach(var buffer in buffers)Marshal.FreeHGlobal(buffer);};
 }
 static void Load(string name){
  try{
   byte[] bytes=null;using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(resource!=null)using(var stream=new MemoryStream()){resource.CopyTo(stream);bytes=stream.ToArray();}}
   if(bytes==null){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fonts",name);if(File.Exists(path))bytes=File.ReadAllBytes(path);}
   if(bytes==null||bytes.Length<12||bytes.Length>2097152)return;
   IntPtr buffer=Marshal.AllocHGlobal(bytes.Length);IntPtr handle=IntPtr.Zero;
   try{Marshal.Copy(bytes,0,buffer,bytes.Length);uint count=0;handle=AddFontMemResourceEx(buffer,(uint)bytes.Length,IntPtr.Zero,ref count);if(handle==IntPtr.Zero||count==0)throw new Exception("Font unavailable");collection.AddMemoryFont(buffer,bytes.Length);buffers.Add(buffer);handles.Add(handle);}
   catch{if(handle!=IntPtr.Zero)RemoveFontMemResourceEx(handle);Marshal.FreeHGlobal(buffer);}
  }catch{/* An optional font must never prevent browsing or joining. */}
 }
 static Font Create(string prefix,float size,FontStyle style){
  var family=collection.Families.FirstOrDefault(f=>f.Name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
  if(family!=null)return new Font(family,size,family.IsStyleAvailable(style)?style:FontStyle.Regular,GraphicsUnit.Point);
  return new Font("Palatino Linotype",size,style,GraphicsUnit.Point);
 }
 internal static Font Body(float size=10,FontStyle style=FontStyle.Regular){return Create("Friz",size,style);}
 internal static Font Title(){return Create("Morpheus",24,FontStyle.Regular);}
}
