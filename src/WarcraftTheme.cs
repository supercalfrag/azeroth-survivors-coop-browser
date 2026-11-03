using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class WarcraftTheme {
    internal static void Combo(ComboBox box){
        box.FlatStyle=FlatStyle.Flat;box.BackColor=Color.FromArgb(28,22,16);box.ForeColor=Color.FromArgb(229,207,154);box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=22;
        box.DrawItem+=delegate(object sender,DrawItemEventArgs e){
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var brush=new SolidBrush(selected?Color.FromArgb(89,65,29):Color.FromArgb(28,22,16)))e.Graphics.FillRectangle(brush,e.Bounds);
            if(e.Index>=0)TextRenderer.DrawText(e.Graphics,box.GetItemText(box.Items[e.Index]),box.Font,new Rectangle(e.Bounds.X+5,e.Bounds.Y,e.Bounds.Width-5,e.Bounds.Height),Color.FromArgb(239,218,171),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        };
    }
}
sealed class WarcraftPanel:TableLayoutPanel {
    internal WarcraftPanel(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
    protected override void OnPaintBackground(PaintEventArgs e){
        Rectangle r=ClientRectangle;if(r.Width<1||r.Height<1)return;
        using(var fill=new LinearGradientBrush(r,Color.FromArgb(47,37,25),Color.FromArgb(14,13,11),90f))e.Graphics.FillRectangle(fill,r);
        // Fine stone grain, drawn procedurally without external game assets.
        using(var line=new Pen(Color.FromArgb(12,211,183,119)))for(int y=18;y<r.Height-12;y+=6)e.Graphics.DrawLine(line,12,y,r.Width-13,y);
        using(var outer=new Pen(Color.FromArgb(12,10,8),7))e.Graphics.DrawRectangle(outer,4,4,r.Width-9,r.Height-9);
        using(var dark=new Pen(Color.FromArgb(71,52,28),5))e.Graphics.DrawRectangle(dark,8,8,r.Width-17,r.Height-17);
        using(var gold=new Pen(Color.FromArgb(164,126,63),1))e.Graphics.DrawRectangle(gold,6,6,r.Width-13,r.Height-13);
        using(var light=new Pen(Color.FromArgb(209,172,99),1))e.Graphics.DrawRectangle(light,11,11,r.Width-23,r.Height-23);
        foreach(var point in new[]{new Point(11,11),new Point(r.Width-12,11),new Point(11,r.Height-12),new Point(r.Width-12,r.Height-12)}){
            using(var shadow=new SolidBrush(Color.FromArgb(15,12,9)))e.Graphics.FillEllipse(shadow,point.X-5,point.Y-5,11,11);
            using(var stud=new LinearGradientBrush(new Rectangle(point.X-3,point.Y-3,7,7),Color.FromArgb(233,198,122),Color.FromArgb(80,55,23),90f))e.Graphics.FillEllipse(stud,point.X-3,point.Y-3,7,7);
        }
    }
}
sealed class WarcraftTitle:Label {
    protected override void OnPaint(PaintEventArgs e){
        var r=ClientRectangle;r.Offset(1,2);TextRenderer.DrawText(e.Graphics,Text,Font,r,Color.Black,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        using(var line=new Pen(Color.FromArgb(126,94,43)))e.Graphics.DrawLine(line,0,Height-8,Width-12,Height-8);
    }
}
sealed class WarcraftButton:Button {
    bool hover,pressed;
    internal WarcraftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Padding=new Padding(10,3,10,3);Margin=new Padding(5,0,3,0);Font=new Font("Segoe UI",9,FontStyle.Bold);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnPaint(PaintEventArgs e){
        var r=ClientRectangle;if(r.Width<3||r.Height<3)return;
        Color top=Enabled?(hover?Color.FromArgb(139,47,31):Color.FromArgb(103,32,24)):Color.FromArgb(52,43,32),bottom=Color.FromArgb(39,15,12);
        if(pressed){var swap=top;top=bottom;bottom=swap;}
        using(var fill=new LinearGradientBrush(r,top,bottom,90f))e.Graphics.FillRectangle(fill,r);
        using(var edge=new Pen(Color.FromArgb(148,109,53),2))e.Graphics.DrawRectangle(edge,1,1,r.Width-3,r.Height-3);
        using(var light=new Pen(Color.FromArgb(207,169,97)))e.Graphics.DrawLine(light,3,3,r.Width-4,3);
        TextRenderer.DrawText(e.Graphics,Text,Font,r,Enabled?Color.FromArgb(255,219,133):Color.FromArgb(132,116,86),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,r.Width-8,r.Height-8),Color.Goldenrod,top);
    }
}
