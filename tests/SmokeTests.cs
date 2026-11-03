using System;using System.IO;using System.Text;using System.Linq;using System.Reflection;using System.Drawing;using System.Windows.Forms;
class SmokeTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Text(BinaryWriter w,string s){byte[] b=Encoding.UTF8.GetBytes(s);w.Write((uint)b.Length);w.Write(b);}
 static byte[] List(){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
  w.Write((byte)20);w.Write((uint)1);Text(w,"ABC123");Text(w,"Example lobby");Text(w,"Synthetic test data");Text(w,"Example host");w.Write((byte)5);Text(w,"elwynn");w.Write((byte)2);w.Write((byte)0);w.Write((byte)1);Text(w,"US");w.Write((uint)2);w.Write(new byte[]{5,9});Text(w,"");w.Write((byte)4);w.Write((byte)2);return m.ToArray();}}
 static void Rejected(byte[] b){bool rejected=false;try{DirectLobbyReader.Decode(b);}catch{rejected=true;}Check(rejected,"Invalid list accepted");}
 [STAThread]static int Main(){try{
  NativeJoinTests.Run();byte[] data=List();var rows=DirectLobbyReader.Decode(data);Check(rows.Count==1&&rows[0].Maximum==4&&rows[0].Players==2&&rows[0].Heroic==2&&rows[0].Zone=="Elwynn Forest"&&rows[0].ClassIds.SequenceEqual(new byte[]{5,9}),"Protocol fields differ");
  for(int i=0;i<data.Length;i++)Rejected(data.Take(i).ToArray());Rejected(data.Concat(new byte[]{1}).ToArray());var wrong=(byte[])data.Clone();wrong[0]=0;Rejected(wrong);wrong=(byte[])data.Clone();wrong[1]=255;wrong[2]=255;Rejected(wrong);
  using(var m=new MemoryStream(DirectLobbyReader.Hello("0.139.0")))using(var r=new BinaryReader(m)){Check(r.ReadByte()==1&&r.ReadUInt32()==3,"Hello protocol changed");Check(r.ReadUInt32()==7&&Encoding.UTF8.GetString(r.ReadBytes(7))=="0.139.0"&&r.ReadUInt32()==0&&r.ReadByte()==1&&m.Position==m.Length,"Hello layout changed");}
  var hero=new HeroUnlockState{Name="Example hero",ClassId=5,ZonesOpen=2};hero.NormalClears.Add(0);hero.HeroicClears[0]=2;Check(hero.CanEnter(rows[0])==true,"Heroic eligibility failed");rows[0].Heroic=4;Check(hero.CanEnter(rows[0])==false,"Locked tier accepted");rows[0].Zone="Unknown";Check(hero.CanEnter(rows[0])==null,"Unknown zone guessed");rows[0].Zone="Un'Goro Crater";rows[0].Heroic=0;Check(hero.CanEnter(rows[0])==false,"Locked normal zone accepted");
  using(var bitmap=new Bitmap(28,28))using(var g=Graphics.FromImage(bitmap))ClassIcons.Draw(g,5,new Rectangle(0,0,28,28));
  Application.EnableVisualStyles();using(var form=new BrowserForm(false)){
   // connectOnShow=false and manual refresh: display only, no game or network access.
   var interval=(ComboBox)typeof(BrowserForm).GetField("interval",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);var timer=(System.Windows.Forms.Timer)typeof(BrowserForm).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
   Check(interval.SelectedItem.ToString()=="Every 5 sec"&&timer.Interval==5000,"Default refresh is not 5 seconds");Check(interval.Items.Cast<string>().SequenceEqual(new[]{"Manual","Every 1 sec","Every 3 sec","Every 5 sec","Every 10 sec"}),"Refresh choices differ");interval.SelectedIndex=1;Check(timer.Interval==1000,"1-second refresh mismatch");interval.SelectedIndex=2;Check(timer.Interval==3000,"3-second refresh mismatch");interval.SelectedIndex=0;Check(!timer.Enabled,"Manual refresh did not stop timer");interval.SelectedIndex=3;
   var f=typeof(BrowserForm).GetField("rooms",BindingFlags.Instance|BindingFlags.NonPublic);f.SetValue(form,DirectLobbyReader.Decode(data));typeof(BrowserForm).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
   form.Opacity=0;form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));string outdir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","artifacts");Directory.CreateDirectory(outdir);bitmap.Save(Path.Combine(outdir,"offline-preview.png"));}
  }
  Console.WriteLine("PASS: synthetic lobby/hello decoding, every truncated prefix, malformed and trailing data, eligibility boundaries, generated badges and offline UI render. No game or service accessed.");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
