using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

static class ClassIcons {
    internal static readonly Dictionary<byte,string> Names=new Dictionary<byte,string>{{1,"Warrior"},{2,"Paladin"},{3,"Hunter"},{4,"Rogue"},{5,"Priest"},{7,"Shaman"},{8,"Mage"},{9,"Warlock"},{11,"Druid"}};
    static readonly Dictionary<byte,Point> Cells=new Dictionary<byte,Point>{{1,new Point(0,0)},{8,new Point(1,0)},{4,new Point(2,0)},{11,new Point(3,0)},{3,new Point(0,1)},{7,new Point(1,1)},{5,new Point(2,1)},{9,new Point(3,1)},{2,new Point(0,2)}};
    static readonly Bitmap Atlas=Load();
    static Bitmap Load(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClassAtlas.png")){if(stream==null)return CreateBadges();using(var image=Image.FromStream(stream))return new Bitmap(image);}}
    // Original, generated badges: no extracted game textures are distributed.
    static Bitmap CreateBadges(){
        var atlas=new Bitmap(256,256);
        Color[] colors={Color.FromArgb(199,156,110),Color.FromArgb(105,204,240),Color.FromArgb(255,245,105),Color.FromArgb(255,125,10),Color.FromArgb(171,212,115),Color.FromArgb(70,115,230),Color.White,Color.FromArgb(148,130,201),Color.FromArgb(245,140,186)};
        byte[] ids={1,8,4,11,3,7,5,9,2};
        string[] marks={"W","M","R","D","H","S","P","WL","PL"};
        using(var g=Graphics.FromImage(atlas))using(var font=new Font("Segoe UI",22,FontStyle.Bold))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}){
            g.Clear(Color.Transparent);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            for(int i=0;i<ids.Length;i++){
                Point cell=Cells[ids[i]];var r=new Rectangle(cell.X*64+2,cell.Y*64+2,60,60);
                using(var fill=new SolidBrush(Color.FromArgb(30,25,20)))g.FillEllipse(fill,r);
                using(var border=new Pen(colors[i],3))g.DrawEllipse(border,r);
                using(var ink=new SolidBrush(colors[i]))g.DrawString(marks[i],font,ink,r,format);
            }
        }return atlas;
    }
    internal static void Draw(Graphics graphics,byte id,Rectangle bounds){Point cell;if(!Cells.TryGetValue(id,out cell))return;graphics.DrawImage(Atlas,bounds,new Rectangle(cell.X*64,cell.Y*64,64,64),GraphicsUnit.Pixel);}
}
