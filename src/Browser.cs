using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class Lobby {
    internal string Code,Name,Description,Host,Class,Country,Zone,Difficulty;
    internal byte[] ClassIds=new byte[0];
    internal string ClassSummary {get{string known=string.Join(", ",ClassIds.GroupBy(x=>x).Select(g=>ClassIcons.Names[g.Key]+(g.Count()>1?" x"+g.Count():"")));int missing=Math.Max(0,Players-ClassIds.Length);return (known.Length==0?"No classes reported":known)+(missing>0?"; "+missing+" not yet reported":"");}}
    internal int Players,Maximum,Heroic;internal bool Password,Opened;internal bool EligibilityKnown=true;
    internal string CountryLabel {get{try{return new RegionInfo(Country).EnglishName+" ("+Country+")";}catch{return string.IsNullOrEmpty(Country)?"Unknown":Country;}}}
}
sealed class BrowserForm : Form {
    readonly DataGridView grid=new WarcraftGrid();readonly TextBox search=new TextBox();
    readonly ComboBox country=new WarcraftComboBox(),zone=new WarcraftComboBox(),difficulty=new WarcraftComboBox(),interval=new WarcraftComboBox();
    readonly CheckBox canJoin=new CheckBox(),noPassword=new CheckBox();
    readonly Label status=new Label(),details=new Label(),heroLabel=new Label();readonly HeroUnlockReader heroReader=new HeroUnlockReader();HeroUnlockState heroState;readonly Button refresh=new WarcraftButton(),copy=new WarcraftButton(),join=new WarcraftButton();
    readonly ComboBox source=new WarcraftComboBox();readonly TextBox version=new TextBox();DirectLobbyReader directReader;
    readonly Timer timer=new Timer();List<Lobby> rooms=new List<Lobby>();LobbyReader reader;bool busy,joining,filtering,closeRequested,stale;
    internal BrowserForm(bool connectOnShow=true){
        Text="Azeroth Co-op Browser v13";ClientSize=new Size(1410,780);MinimumSize=new Size(1020,650);StartPosition=FormStartPosition.CenterScreen;
        Font=WarcraftFonts.Body();BackColor=Color.FromArgb(22,18,14);ForeColor=Color.FromArgb(225,211,173);
        var root=new WarcraftPanel{Dock=DockStyle.Fill,Padding=new Padding(25),ColumnCount=1,RowCount=7};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));root.RowStyles.Add(new RowStyle(SizeType.Absolute,30));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,76));root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));Controls.Add(root);
        root.Controls.Add(new WarcraftTitle{Text="AZEROTH  /  CO-OP BROWSER",Font=WarcraftFonts.Title(),ForeColor=Color.FromArgb(246,208,119),Dock=DockStyle.Fill},0,0);
        var filters=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};root.Controls.Add(filters,0,1);
        filters.Controls.Add(new Label{Text="Search",AutoSize=true,Padding=new Padding(0,5,6,0)});
        search.BackColor=Color.FromArgb(18,15,12);search.ForeColor=ForeColor;search.BorderStyle=BorderStyle.FixedSingle;search.Width=230;search.Margin=new Padding(0,3,14,3);filters.Controls.Add(search);
        foreach(var c in new[]{country,zone,difficulty}){WarcraftTheme.Combo(c);c.DropDownStyle=ComboBoxStyle.DropDownList;c.Width=c==country?220:c==zone?220:145;c.Margin=new Padding(0,3,12,3);filters.Controls.Add(c);c.SelectedIndexChanged+=delegate{if(!filtering)Render();};}
        country.Items.Add("All countries");zone.Items.Add("All zones");difficulty.Items.Add("All difficulties");country.SelectedIndex=zone.SelectedIndex=difficulty.SelectedIndex=0;
        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};root.Controls.Add(tools,0,3);heroLabel.Text="Selected hero will be checked when rooms refresh.";heroLabel.Dock=DockStyle.Fill;heroLabel.ForeColor=Color.FromArgb(213,193,140);root.Controls.Add(heroLabel,0,2);
        canJoin.Text="Can Join";canJoin.AutoSize=true;
        noPassword.Text="No password";noPassword.AutoSize=true;
        tools.Controls.Add(new Label{Text="Source:",AutoSize=true,Padding=new Padding(0,5,3,0)});WarcraftTheme.Combo(source);source.DropDownStyle=ComboBoxStyle.DropDownList;source.Width=150;source.Items.AddRange(new object[]{"Direct service","Game view"});source.SelectedIndex=0;tools.Controls.Add(source);tools.Controls.Add(new Label{Text="Game version:",AutoSize=true,Padding=new Padding(8,5,3,0)});version.Width=80;version.Text=DirectLobbyReader.DetectVersion();tools.Controls.Add(version);canJoin.Enabled=true;tools.Controls.Add(canJoin);tools.Controls.Add(noPassword);tools.Controls.Add(new Label{Text="Refresh:",AutoSize=true,Padding=new Padding(16,5,3,0)});
        WarcraftTheme.Combo(interval);interval.DropDownStyle=ComboBoxStyle.DropDownList;interval.Width=130;interval.Items.AddRange(new object[]{"Manual","Every 1 sec","Every 3 sec","Every 5 sec","Every 10 sec"});interval.SelectedIndex=3;timer.Interval=5000;tools.Controls.Add(interval);
        refresh.Text="Refresh now";refresh.AutoSize=true;tools.Controls.Add(refresh);copy.Text="Copy join code";copy.AutoSize=true;copy.Enabled=false;tools.Controls.Add(copy);join.Text="Join in game";join.AutoSize=true;join.Enabled=false;tools.Controls.Add(join);
        grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=grid.AllowUserToDeleteRows=grid.AllowUserToResizeRows=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        grid.RowHeadersVisible=false;grid.BackgroundColor=Color.FromArgb(24,21,17);grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;grid.GridColor=Color.FromArgb(61,48,32);
        grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(51,41,29),ForeColor=Color.FromArgb(235,199,115),Font=WarcraftFonts.Body(10,FontStyle.Bold)};
        grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(24,21,17),ForeColor=Color.FromArgb(226,215,185),SelectionBackColor=Color.FromArgb(91,76,38),SelectionForeColor=Color.White,Padding=new Padding(5)};
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(31,27,21);grid.RowTemplate.Height=34;grid.ColumnHeadersHeight=36;
        AddColumn("Room",260);AddColumn("Country",175);AddColumn("Zone",180);AddColumn("Difficulty",105);AddColumn("Players",75);AddColumn("Party classes",175);AddColumn("Password",90);AddColumn("Code",90);AddColumn("Your hero",120);
        grid.CellPainting+=PaintClasses;
        root.Controls.Add(grid,0,4);details.BackColor=Color.FromArgb(25,20,15);details.Dock=DockStyle.Fill;details.Padding=new Padding(0,10,0,0);root.Controls.Add(details,0,5);
        status.Dock=DockStyle.Fill;status.Text="Direct service reads rooms without the game or its Co-op window open.";status.ForeColor=Color.FromArgb(176,160,121);root.Controls.Add(status,0,6);
        source.SelectedIndexChanged+=delegate{ChangeSource();};version.TextChanged+=delegate{if(directReader!=null){directReader.Dispose();directReader=null;}stale=true;copy.Enabled=join.Enabled=false;};search.TextChanged+=delegate{Render();};canJoin.CheckedChanged+=delegate{Render();};noPassword.CheckedChanged+=delegate{Render();};refresh.Click+=async delegate{await RefreshRooms();};
        interval.SelectedIndexChanged+=delegate{timer.Stop();if(interval.SelectedIndex>0){timer.Interval=new[]{0,1000,3000,5000,10000}[interval.SelectedIndex];if(connectOnShow&&Visible)timer.Start();}};
        timer.Tick+=async delegate{await RefreshRooms();};grid.SelectionChanged+=delegate{ShowDetails();};grid.CellDoubleClick+=async delegate(object s,DataGridViewCellEventArgs e){if(e.RowIndex>=0&&e.RowIndex<grid.Rows.Count)await JoinRoom(grid.Rows[e.RowIndex].Tag as Lobby);};copy.Click+=delegate{CopyCode();};join.Click+=async delegate{await JoinRoom(Selected());};
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy||joining){closeRequested=true;timer.Stop();e.Cancel=true;status.Text="Finishing the current capture and disconnecting...";}};
        FormClosed+=delegate{timer.Dispose();heroReader.Dispose();if(directReader!=null)directReader.Dispose();if(reader!=null)try{reader.Dispose();}catch(Exception e){MessageBox.Show("The lobby reader could not detach. Restart the game before reconnecting.\n"+e.Message,"Lobby reader",MessageBoxButtons.OK,MessageBoxIcon.Information);}};Shown+=async delegate{if(connectOnShow){await RefreshRooms();if(!closeRequested&&!IsDisposed&&interval.SelectedIndex>0)timer.Start();}};
    }
    void PaintClasses(object sender,DataGridViewCellPaintingEventArgs e){
        if(e.RowIndex<0||e.ColumnIndex<0||grid.Columns[e.ColumnIndex].Name!="Party classes")return;
        var room=grid.Rows[e.RowIndex].Tag as Lobby;if(room==null)return;
        e.PaintBackground(e.ClipBounds,true);e.Paint(e.ClipBounds,DataGridViewPaintParts.Border);
        int size=Math.Min(28,e.CellBounds.Height-6),x=e.CellBounds.X+7,y=e.CellBounds.Y+(e.CellBounds.Height-size)/2;

        foreach(byte id in room.ClassIds){if(x+size>e.CellBounds.Right-3)break;ClassIcons.Draw(e.Graphics,id,new Rectangle(x,y,size,size));x+=size+4;}
        for(int missing=room.ClassIds.Length;missing<room.Players;missing++){if(x+size>e.CellBounds.Right-3)break;var box=new Rectangle(x,y,size,size);using(var pen=new Pen(Color.FromArgb(123,106,73)))e.Graphics.DrawRectangle(pen,box);TextRenderer.DrawText(e.Graphics,"?",grid.Font,box,Color.Silver,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);x+=size+4;}
        e.Handled=true;
    }
    void AddColumn(string name,int width){grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,HeaderText=name,Width=width,SortMode=DataGridViewColumnSortMode.Automatic});}
    Lobby Selected(){return grid.SelectedRows.Count==1?grid.SelectedRows[0].Tag as Lobby:null;}
    void ShowDetails(){var r=Selected();copy.Enabled=r!=null&&!stale&&!joining;join.Enabled=copy.Enabled&&!joining&&!busy;details.Text=r==null?"Choose a room to see its description. Double-click a room to join it in the game.":r.Name+"  •  Host: "+r.Host+" ("+r.Class+")\n"+(string.IsNullOrEmpty(r.Description)?"No description supplied.":r.Description);}
    async Task JoinRoom(Lobby room){
        if(room==null||busy||joining||stale||closeRequested)return;
        if(room.Players>=room.Maximum){status.Text="This room is full. Refresh and choose another room.";return;}
        if(room.EligibilityKnown&&!room.Opened){status.Text="This room is locked for the selected hero.";return;}
        if(!rooms.Any(r=>object.ReferenceEquals(r,room))){status.Text="The room list changed. Select the room again.";return;}
        string password="";
        joining=true;timer.Stop();join.Enabled=refresh.Enabled=copy.Enabled=source.Enabled=version.Enabled=false;
        try{
            if(room.Password){using(var prompt=new JoinPasswordForm(room.Name)){if(prompt.ShowDialog(this)!=DialogResult.OK)return;password=prompt.Password;}}
            status.Text="Sending join request to Azeroth Survivors...";
            string code=room.Code,secret=password;var result=await Task.Run(()=>{
                IntPtr window;using(var game=new JoinGame())using(var bridge=new NativeJoinBridge(game)){bridge.Request(code,secret,true);window=game.Process.MainWindowHandle;}
                string panelError=null;try{using(var game=new JoinGame(0x2e9900,254))using(var panel=new PartyPanelBridge(game)){panel.Open();}}catch(Exception panelFailure){panelError=panelFailure.Message;}
                return Tuple.Create(window,panelError);
            });
            status.Text="Join request sent for "+room.Name+(result.Item2==null?". Party panel opened in the game.":". Open Co-op in the game: "+result.Item2);
            if(result.Item1!=IntPtr.Zero)JoinWindow.Focus(result.Item1);
        }catch(Exception e){status.Text="Could not join: "+e.Message;}
        finally{
            password=null;joining=false;refresh.Enabled=source.Enabled=true;version.Enabled=source.SelectedIndex==0;ShowDetails();
            if(closeRequested)Close();else if(interval.SelectedIndex>0)timer.Start();
        }
    }
    void CopyCode(){var r=Selected();if(r==null||stale)return;try{Clipboard.SetText(r.Code);status.Text="Copied "+r.Code+". In the game choose Join by code and paste it.";}catch(Exception e){status.Text="Could not copy: "+e.Message;}}
    void ChangeSource(){
        if(busy||joining)return;
        if(directReader!=null){directReader.Dispose();directReader=null;}
        if(reader!=null){try{reader.Dispose();reader=null;}catch(Exception e){status.Text=e.Message;return;}}
        canJoin.Checked=false;canJoin.Enabled=true;heroState=null;heroLabel.Text=source.SelectedIndex==0?"Selected hero will be checked when rooms refresh.":"Game view supplies hero eligibility.";version.Enabled=source.SelectedIndex==0;rooms.Clear();Render();stale=true;copy.Enabled=join.Enabled=false;
        status.Text=source.SelectedIndex==0?"Direct service: the game can stay on its main screen to check the selected hero.":"Game view: leave Co-op > Lobbies open to read hero eligibility.";
    }
    internal async Task RefreshRooms(){
        if(busy||joining||IsDisposed||closeRequested)return;busy=true;refresh.Enabled=false;source.Enabled=false;version.Enabled=false;status.Text=source.SelectedIndex==0?"Requesting rooms from the lobby service...":"Reading the game's live room list...";
        bool direct=source.SelectedIndex==0;
        try{
            List<Lobby> next;
            if(direct){if(directReader==null)directReader=new DirectLobbyReader(version.Text.Trim());next=await Task.Run(()=>directReader.ReadRooms());}
            else{if(reader==null)reader=await Task.Run(()=>LobbyReader.Connect());next=await Task.Run(()=>reader.ReadRooms());}
            if(IsDisposed)return;
            if(direct){
                heroState=null;status.Text="Checking the selected hero and unlocks...";
                try{heroState=await Task.Run(()=>heroReader.ReadSelected());heroState.Apply(next);heroLabel.Text="Selected: "+heroState.Name+" / "+ClassIcons.Names[heroState.ClassId]+"  |  Normal zones: "+heroState.ZonesOpen+" / 12  |  Checked "+DateTime.Now.ToString("HH:mm:ss");}
                catch(Exception heroError){heroReader.Dispose();foreach(var room in next){room.EligibilityKnown=false;room.Opened=false;}heroLabel.Text="Hero unchecked: "+heroError.Message+" Can Join hides unchecked rooms.";}
            }else{heroLabel.Text="Game view supplies eligibility for the selected hero.";}
            if(IsDisposed)return;rooms=next;stale=false;
            filtering=true;try{UpdateFilter(country,"All countries",rooms.Select(r=>r.CountryLabel));UpdateFilter(zone,"All zones",rooms.Select(r=>r.Zone));UpdateFilter(difficulty,"All difficulties",rooms.Select(r=>r.Difficulty));}finally{filtering=false;}
            Render();status.Text=grid.Rows.Count+" of "+rooms.Count+" rooms returned | Read at "+DateTime.Now.ToString("HH:mm:ss")+(direct?(heroState!=null?" | Direct service; hero checked":" | Direct service; hero unchecked"):" | Keep the game's lobby list open");
        }catch(Exception e){if(!IsDisposed){stale=true;status.Text=e.Message+" Previous list is not refreshed.";copy.Enabled=join.Enabled=false;}if(directReader!=null){directReader.Dispose();directReader=null;}if(reader!=null){try{reader.Dispose();}catch(Exception cleanup){status.Text+=" Detach: "+cleanup.Message;}reader=null;}}
        finally{busy=false;if(!IsDisposed){refresh.Enabled=true;source.Enabled=true;version.Enabled=source.SelectedIndex==0;if(closeRequested)Close();}}
    }
    static void UpdateFilter(ComboBox box,string all,IEnumerable<string> values){string selected=box.SelectedItem as string??all;var entries=new[]{all}.Concat(values.Distinct().OrderBy(s=>s)).ToArray();if(!entries.Contains(selected))entries=entries.Concat(new[]{selected}).ToArray();box.Items.Clear();box.Items.AddRange(entries);box.SelectedItem=selected;}
    void Render(){
        if(grid.Columns.Count==0)return;var selected=Selected();string code=selected==null?null:selected.Code;
        int first=grid.FirstDisplayedScrollingRowIndex;string top=first>=0&&first<grid.Rows.Count?((Lobby)grid.Rows[first].Tag).Code:null;
        var sort=grid.SortedColumn;var direction=grid.SortOrder;grid.SuspendLayout();
        try{
            grid.Rows.Clear();string q=search.Text.Trim();
            foreach(var r in rooms.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(x=>x.Code)){
                if(country.SelectedIndex>0&&r.CountryLabel!=(string)country.SelectedItem)continue;
                if(zone.SelectedIndex>0&&r.Zone!=(string)zone.SelectedItem)continue;
                if(difficulty.SelectedIndex>0&&r.Difficulty!=(string)difficulty.SelectedItem)continue;
                if(canJoin.Checked&&(!r.EligibilityKnown||!r.Opened))continue;
                if(noPassword.Checked&&r.Password)continue;
                if(q.Length>0&&(r.Name+" "+r.Description+" "+r.Host+" "+r.Code).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)<0)continue;
                int i=grid.Rows.Add(r.Name,r.CountryLabel,r.Zone,r.Difficulty,r.Players+" / "+r.Maximum,r.ClassSummary,r.Password?"Required":"None",r.Code,r.EligibilityKnown?(r.Opened?"Can enter":"Locked"):"Not checked");grid.Rows[i].Tag=r;grid.Rows[i].Cells["Party classes"].ToolTipText=r.ClassSummary;
            }
            if(sort!=null)grid.Sort(sort,direction==SortOrder.Descending?System.ComponentModel.ListSortDirection.Descending:System.ComponentModel.ListSortDirection.Ascending);
            grid.ClearSelection();foreach(DataGridViewRow row in grid.Rows){var r=(Lobby)row.Tag;if(r.Code==code)row.Selected=true;if(r.Code==top)grid.FirstDisplayedScrollingRowIndex=row.Index;}
        }finally{grid.ResumeLayout();}ShowDetails();
        if(!busy)status.Text=grid.Rows.Count+" of "+rooms.Count+" rooms shown";
    }
}
static class BrowserProgram {
    [STAThread]static int Main(string[] args){
        if(args.Length>=1&&args[0]=="--probe"){try{using(var r=LobbyReader.Connect()){var rows=r.ReadRooms();Console.WriteLine("Rooms="+rows.Count);Console.WriteLine("Countries="+rows.Select(x=>x.Country).Distinct().Count());Console.WriteLine("First="+(rows.Count>0?rows[0].Name:"none"));if(args.Length==2){var data=rows.Select(x=>new Dictionary<string,object>{{"Code",x.Code},{"Name",x.Name},{"Description",x.Description},{"Host",x.Host},{"Class",x.Class},{"ClassIds",x.ClassIds},{"Country",x.Country},{"Zone",x.Zone},{"Difficulty",x.Difficulty},{"Players",x.Players},{"Maximum",x.Maximum},{"Password",x.Password},{"Opened",x.Opened}}).ToArray();File.WriteAllText(args[1],new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(data));}}return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new BrowserForm());return 0;
    }
}
