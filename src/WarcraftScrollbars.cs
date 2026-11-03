using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.Runtime.InteropServices;using System.Windows.Forms;

// Repaint native scrollbar chrome, retaining Windows hit-testing, dragging,
// wheel, keyboard and accessibility behavior.
sealed class WarcraftScrollSkin : NativeWindow, IDisposable {
 [StructLayout(LayoutKind.Sequential)]struct Rect {internal int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]struct BarInfo {internal int Size;internal Rect Bounds;internal int Arrow,ThumbTop,ThumbBottom,Reserved;[MarshalAs(UnmanagedType.ByValArray,SizeConst=6)]internal int[] State;}
 [DllImport("user32.dll")]static extern bool GetScrollBarInfo(IntPtr window,int id,ref BarInfo info);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect bounds);
 [DllImport("user32.dll")]static extern IntPtr GetWindowDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 readonly int objectId;
 internal WarcraftScrollSkin(int objectId=-4){this.objectId=objectId;}
 internal void Attach(IntPtr window){if(Handle==window)return;if(Handle!=IntPtr.Zero)ReleaseHandle();if(window!=IntPtr.Zero)AssignHandle(window);}
 protected override void WndProc(ref Message m){
  base.WndProc(ref m);
  if(m.Msg==0x0f||m.Msg==0x85||m.Msg==0x200||m.Msg==0xa0||m.Msg==0x201||m.Msg==0x202||m.Msg==0x317||m.Msg==0x318||m.Msg==0xe0||m.Msg==0xe2||m.Msg==0xe4||m.Msg==0xe6||m.Msg==0xe9||m.Msg==0x18||m.Msg==0x47){
   try{Paint(m.Msg==0x317||m.Msg==0x318?m.WParam:IntPtr.Zero);}catch{/* Keep native scrolling usable if a transient window is disappearing. */}
  }
 }
 internal void PaintNow(){try{Paint(IntPtr.Zero);}catch{}}
 void Paint(IntPtr printDc){
  if(Handle==IntPtr.Zero)return;var info=new BarInfo{Size=Marshal.SizeOf(typeof(BarInfo)),State=new int[6]};Rect window;
  if(!GetScrollBarInfo(Handle,objectId,ref info)||!GetWindowRect(Handle,out window)||(info.State[0]&0x18000)!=0)return;
  var bar=new Rectangle(info.Bounds.Left-window.Left,info.Bounds.Top-window.Top,info.Bounds.Right-info.Bounds.Left,info.Bounds.Bottom-info.Bounds.Top);if(bar.Width<2||bar.Height<2)return;
  IntPtr dc=printDc!=IntPtr.Zero?printDc:GetWindowDC(Handle);if(dc==IntPtr.Zero)return;
  try{using(var g=Graphics.FromHdc(dc))Draw(g,bar,info.Arrow,info.ThumbTop,info.ThumbBottom,info.State);}finally{if(printDc==IntPtr.Zero)ReleaseDC(Handle,dc);}
 }
 static void Draw(Graphics g,Rectangle bar,int arrow,int top,int bottom,int[] states){
  bool vertical=bar.Height>bar.Width;int length=vertical?bar.Height:bar.Width,thickness=vertical?bar.Width:bar.Height;arrow=Math.Max(1,Math.Min(arrow,Math.Min(thickness,length/2)));
  using(var track=new SolidBrush(Color.FromArgb(25,21,16)))g.FillRectangle(track,bar);
  using(var border=new Pen(Color.FromArgb(90,68,37)))g.DrawRectangle(border,bar.X,bar.Y,bar.Width-1,bar.Height-1);
  Rectangle first=vertical?new Rectangle(bar.X,bar.Y,bar.Width,arrow):new Rectangle(bar.X,bar.Y,arrow,bar.Height),last=vertical?new Rectangle(bar.X,bar.Bottom-arrow,bar.Width,arrow):new Rectangle(bar.Right-arrow,bar.Y,arrow,bar.Height);
  Button(g,first,vertical,true,(states[1]&1)!=0);Button(g,last,vertical,false,(states[5]&1)!=0);
  top=Math.Max(arrow,top);bottom=Math.Min(length-arrow,bottom);
  if(bottom>top){var thumb=vertical?new Rectangle(bar.X+2,bar.Y+top,bar.Width-4,bottom-top):new Rectangle(bar.X+top,bar.Y+2,bottom-top,bar.Height-4);if(thumb.Width>0&&thumb.Height>0){using(var fill=new LinearGradientBrush(thumb,Color.FromArgb(132,99,49),Color.FromArgb(64,48,28),vertical?0f:90f))g.FillRectangle(fill,thumb);using(var edge=new Pen(Color.FromArgb(198,155,80)))g.DrawRectangle(edge,thumb.X,thumb.Y,thumb.Width-1,thumb.Height-1);}}
 }
 static void Button(Graphics g,Rectangle r,bool vertical,bool first,bool disabled){
  if(r.Width<2||r.Height<2)return;using(var fill=new SolidBrush(Color.FromArgb(53,40,26)))g.FillRectangle(fill,r);using(var edge=new Pen(Color.FromArgb(112,82,42)))g.DrawRectangle(edge,r.X,r.Y,r.Width-1,r.Height-1);
  int x=r.Left+r.Width/2,y=r.Top+r.Height/2,d=Math.Max(2,Math.Min(r.Width,r.Height)/4);Point[] points=vertical?(first?new[]{new Point(x,y-d),new Point(x-d,y+d/2),new Point(x+d,y+d/2)}:new[]{new Point(x,y+d),new Point(x-d,y-d/2),new Point(x+d,y-d/2)}):(first?new[]{new Point(x-d,y),new Point(x+d/2,y-d),new Point(x+d/2,y+d)}:new[]{new Point(x+d,y),new Point(x-d/2,y-d),new Point(x-d/2,y+d)});
  using(var ink=new SolidBrush(disabled?Color.FromArgb(107,88,57):Color.FromArgb(231,193,111)))g.FillPolygon(ink,points);
 }
 public void Dispose(){if(Handle!=IntPtr.Zero)ReleaseHandle();}
}
sealed class WarcraftGrid : DataGridView {
 readonly Dictionary<ScrollBar,WarcraftScrollSkin> skins=new Dictionary<ScrollBar,WarcraftScrollSkin>();
 internal WarcraftGrid(){ControlAdded+=delegate(object sender,ControlEventArgs e){Watch(e.Control as ScrollBar);};foreach(Control control in Controls)Watch(control as ScrollBar);}
 void Watch(ScrollBar bar){if(bar==null||skins.ContainsKey(bar))return;var skin=new WarcraftScrollSkin();skins.Add(bar,skin);bar.HandleCreated+=delegate{skin.Attach(bar.Handle);};bar.HandleDestroyed+=delegate{skin.Dispose();};if(bar.IsHandleCreated)skin.Attach(bar.Handle);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);ScrollBar vertical=null,horizontal=null;foreach(var bar in skins.Keys)if(bar.Visible){if(bar is VScrollBar)vertical=bar;else if(bar is HScrollBar)horizontal=bar;}if(vertical!=null&&horizontal!=null){var corner=new Rectangle(vertical.Left,horizontal.Top,vertical.Width,horizontal.Height);using(var fill=new SolidBrush(Color.FromArgb(25,21,16)))e.Graphics.FillRectangle(fill,corner);}}
 internal void RefreshScrollbarTheme(){foreach(var pair in skins)if(pair.Key.Visible&&pair.Key.IsHandleCreated){pair.Key.Invalidate();pair.Key.Update();pair.Value.PaintNow();}if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)(()=>{if(!IsDisposed)foreach(var pair in skins)if(pair.Key.Visible)pair.Value.PaintNow();}));}
 protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);RefreshScrollbarTheme();}
 protected override void Dispose(bool disposing){if(disposing)foreach(var skin in skins.Values)skin.Dispose();base.Dispose(disposing);}
}
sealed class WarcraftComboBox : ComboBox {
 [StructLayout(LayoutKind.Sequential)]struct Rect {internal int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]struct ComboInfo {internal int Size;internal Rect Item,Button;internal int State;internal IntPtr Combo,Edit,List;}
 [DllImport("user32.dll")]static extern bool GetComboBoxInfo(IntPtr combo,ref ComboInfo info);
 readonly WarcraftScrollSkin listSkin=new WarcraftScrollSkin(-5);
 protected override void OnDropDown(EventArgs e){base.OnDropDown(e);var info=new ComboInfo{Size=Marshal.SizeOf(typeof(ComboInfo))};if(GetComboBoxInfo(Handle,ref info))listSkin.Attach(info.List);}
 protected override void Dispose(bool disposing){if(disposing)listSkin.Dispose();base.Dispose(disposing);}
}
