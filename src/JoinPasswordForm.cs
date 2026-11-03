using System;using System.Drawing;using System.Runtime.InteropServices;using System.Windows.Forms;
static class JoinWindow {
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
 internal static void Focus(IntPtr window){if(IsIconic(window))ShowWindowAsync(window,9);SetForegroundWindow(window);}
}
sealed class JoinPasswordForm:Form {
 readonly TextBox input=new TextBox();internal string Password {get{return input.Text;}}
 internal JoinPasswordForm(string room){
  Text="Join password-protected room";ClientSize=new Size(440,160);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;
  BackColor=Color.FromArgb(22,18,14);ForeColor=Color.FromArgb(225,211,173);Font=WarcraftFonts.Body();
  Controls.Add(new Label{Text="Password for "+room,Location=new Point(18,18),Size=new Size(402,32),AutoEllipsis=true});
  input.Location=new Point(18,56);input.Size=new Size(402,28);input.UseSystemPasswordChar=true;input.MaxLength=128;input.BackColor=Color.FromArgb(30,25,20);input.ForeColor=ForeColor;Controls.Add(input);
  var accept=new WarcraftButton{Text="Join",DialogResult=DialogResult.OK,Location=new Point(204,108),Size=new Size(100,32)};var cancel=new WarcraftButton{Text="Cancel",DialogResult=DialogResult.Cancel,Location=new Point(320,108),Size=new Size(100,32)};Controls.Add(accept);Controls.Add(cancel);AcceptButton=accept;CancelButton=cancel;Shown+=delegate{input.Focus();};
 }
 protected override void Dispose(bool disposing){if(disposing)input.Clear();base.Dispose(disposing);}
}
