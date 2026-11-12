using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kbd = BeatlesLauncher.Controls;

namespace BeatlesLauncher {
 // One settings row: title (+ optional hint) on the left, one or more controls on the right.
 sealed class SettingRow {
  public Label Title,Hint;
  public Control[] Items;
  public int[] Widths;
  public int Height;
  public Divider Line;
 }
 public sealed class LauncherForm : ResponsiveWindow {
  static readonly Color Muted=Aero.Muted;
  static readonly Color Warning=Color.FromArgb(255,150,118);
  static readonly int[] ResolutionWidths={960,1280,1600,1920,2560};
  static readonly int[] ResolutionHeights={540,720,900,1080,1440};
  static readonly string[] PageNames={"Graphics","Audio","Controllers","Game files & saves"};
  static readonly string[] PageSubtitles={"Display, frame rate & image quality","Sound output & microphone","Players, instruments & user name","Game executable, saves, logs & DLC"};
  static readonly string[] PageHints={"Display, quality & presets","Dolby Digital & microphone","Players, instruments & name","Executable, saves, logs & DLC"};
  static readonly Glyph[] PageGlyphs={Glyph.Monitor,Glyph.Speaker,Glyph.Gamepad,Glyph.Folder};
  Settings settings;
  FileDisplay path;
  Label status,filesStatus;
  ComboBox resolution,frameRate,antiAliasing;
  GlassSlider renderScale,anisotropic;
  CheckBox fullscreen,performanceMode,dolbyDigital;
  Button play,browse,presetLow,presetMedium,presetHigh,audioControls,editControls,saves,logs,importDlc;
  AeroTabs tabs;
  AeroPagePanel graphicsPanel,qualityPanel,audioPanel,controllersPanel,filesPanel;
  readonly List<SettingRow> displayRows=new List<SettingRow>(),qualityRows=new List<SettingRow>(),audioRows=new List<SettingRow>(),controllerRows=new List<SettingRow>(),fileRows=new List<SettingRow>();
  readonly PlayerInstrumentCard[] playerCards=new PlayerInstrumentCard[4];
  FlatPanel rail,side;
  RailButton railHome,railSettings;
  NavItem[] homeRows,sideItems;SleeveArt art;
  Divider homeRule,homeRule2,sideRule;
  Label crumb,chip,summary,footerHint,sideCrumb,sideNote,pageTitle,pageSubtitle;
  Control[] homeControls,settingsControls;
  Timer deviceStatusTimer;
  CheckResult check;
  Process game;
  int validationGeneration,view,page;
  bool preview,validating;
  TextBox userBox;Label userLabel;Label heading,fileHint,instrumentTitle,version,performanceHint,renderScaleLabel,renderScaleValue,anisotropicLabel,anisotropicValue;
  bool updatingGraphicsControls;

  int RowControlHeight(Control control) {
   if(control is ComboBox)return Math.Max(S(34),control.PreferredSize.Height);
   if(control is TextBox)return Math.Max(S(34),control.PreferredSize.Height);
   return 0;
  }
  // Lays rows out top to bottom; narrow panels stack the controls under the title.
  int LayoutRows(List<SettingRow> rows,int x,int width,int y) {
   bool stack=width<S(430);int gap=S(8);
   foreach(var row in rows) {
    int count=row.Items.Length,total=gap*(count-1);for(int i=0;i<count;i++)total+=S(row.Widths[i]);
    double factor=stack&&total>width?(double)(width-gap*(count-1))/Math.Max(1,total-gap*(count-1)):1;
    int controlHeight=S(row.Height);foreach(var item in row.Items)controlHeight=Math.Max(controlHeight,RowControlHeight(item));
    int textWidth=stack?width:Math.Max(S(120),width-total-S(24));
    int titleHeight=Math.Max(S(22),TextHeight(row.Title,textWidth,22)-S(2)),hintHeight=row.Hint==null?0:TextHeight(row.Hint,textWidth,16),textHeight=titleHeight+hintHeight;
    int rowHeight=stack?S(12)+textHeight+S(8)+controlHeight+S(12):Math.Max(S(58),Math.Max(textHeight,controlHeight)+S(22));
    int titleY=stack?y+S(12):y+(rowHeight-textHeight)/2;
    Place(row.Title,x,titleY,textWidth,titleHeight);if(row.Hint!=null)Place(row.Hint,x,titleY+titleHeight,textWidth,hintHeight);
    int itemX=stack?x:x+width-total,itemY=stack?titleY+textHeight+S(8):y+(rowHeight-controlHeight)/2;
    for(int i=0;i<count;i++) {
     int itemWidth=(int)Math.Round(S(row.Widths[i])*(stack?factor:1)),height=RowControlHeight(row.Items[i]);if(height==0)height=S(row.Height);
     Place(row.Items[i],itemX,itemY+(controlHeight-height)/2,itemWidth,height);itemX+=itemWidth+gap;
    }
    Place(row.Line,x,y+rowHeight-1,width,1);y+=rowHeight;
   }
   return y;
  }
  int PageWidth(Panel panel){return Math.Max(S(200),panel.ClientSize.Width-2*S(22)-SystemInformation.VerticalScrollBarWidth);}
  protected override void LayoutContent(){
   if(version==null)return;ChromeLayout();
   int width=ClientSize.Width,height=ClientSize.Height,railWidth=S(70);
   Place(rail,0,0,railWidth,height);
   railHome.UiScale=railSettings.UiScale=UiScale;
   Place(railHome,0,S(34),railWidth,S(56));Place(railSettings,0,S(98),railWidth,S(56));
   if(view==0)LayoutHome(railWidth,width,height);else LayoutSettings(railWidth,width,height);
  }
  void LayoutHome(int railWidth,int width,int height) {
   bool tight=height<S(620);
   int x=railWidth+S(44),w=Math.Max(S(240),Math.Min(S(520),width-x-S(44))),y=S(40);
   Place(crumb,x,y,Math.Max(1,w-S(110)),S(24));Place(chip,x+w-S(106),y,S(106),S(24));y+=S(32);
   if(!tight){Place(homeRule,x,y,w,1);y+=S(18);}
   int titleHeight=tight?S(44):S(62);Place(heading,x,y,w,titleHeight);y+=titleHeight+(tight?S(2):S(6));
   if(!tight){Place(homeRule2,x,y,w,1);y+=S(14);}
   Place(summary,x,y,w,S(26));y+=S(28);
   int statusHeight=TextHeight(status,w,18);Place(status,x,y,w,statusHeight);y+=statusHeight+(tight?S(10):S(16));
   int playHeight=tight?S(46):Math.Min(S(60),Math.Max(S(46),height/12));Place(play,x,y,w,playHeight);y+=playHeight+(tight?S(12):S(20));
   int rowGap=tight?S(6):S(10),rowHeight=tight?S(40):Math.Min(S(60),Math.Max(S(46),(height-y-S(70))/4-rowGap));
   for(int i=0;i<homeRows.Length;i++){homeRows[i].UiScale=UiScale;Place(homeRows[i],x,y,w,rowHeight);y+=rowHeight+rowGap;}
   int artRoom=width-(x+w)-S(44)-S(40);
   art.Visible=artRoom>=S(190)&&!tight;
   if(art.Visible){int aw=Math.Min(S(320),artRoom),ah=aw*436/353,maxH=height-S(150);if(ah>maxH){ah=maxH;aw=ah*353/436;}Place(art,width-S(40)-aw-(Math.Min(S(320),artRoom)-aw)/2,(height-ah)/2-S(10),aw,ah);}
   int footerY=height-S(36);
   version.Visible=y<=footerY+S(6)&&width>=S(520);Place(version,width-S(40)-S(190),footerY,S(190),S(24));
   int hintWidth=width-x-S(40)-S(200);footerHint.Visible=y<=footerY&&hintWidth>=S(220);Place(footerHint,x,footerY,Math.Max(1,hintWidth),S(24));
  }
  void LayoutSettings(int railWidth,int width,int height) {
   bool narrow=width<S(900);int sideWidth=narrow?S(230):S(290);
   Place(side,railWidth,0,sideWidth,height);
   int padding=S(22),inner=sideWidth-2*padding;
   Place(sideCrumb,padding,S(40),inner,S(24));Place(sideRule,padding,S(72),inner,1);
   int itemY=S(92),itemHeight=Math.Min(S(62),Math.Max(S(46),(height-itemY-S(110))/4-S(8)));
   for(int i=0;i<sideItems.Length;i++){sideItems[i].UiScale=UiScale;Place(sideItems[i],S(14),itemY,sideWidth-S(28),itemHeight);itemY+=itemHeight+S(8);}
   int noteHeight=TextHeight(sideNote,inner,16);Place(sideNote,padding,height-S(24)-noteHeight,inner,noteHeight);sideNote.Visible=itemY+noteHeight+S(24)<=height;
   int x=railWidth+sideWidth+(narrow?S(26):S(44)),w=Math.Max(S(200),width-x-(narrow?S(24):S(40)));
   Place(pageTitle,x,S(34),Math.Max(1,w-S(90)),S(52));Place(pageSubtitle,x,S(86),w,S(24));
   int top=S(122),pageHeight=Math.Max(S(120),height-top-S(46));
   Place(tabs,x,top,w,pageHeight);tabs.UiScale=UiScale;Place(audioPanel,x,top,w,pageHeight);Place(controllersPanel,x,top,w,pageHeight);Place(filesPanel,x,top,w,pageHeight);
   LayoutGraphics();LayoutAudio();LayoutControllers();LayoutFiles();
   int footerY=height-S(36);version.Visible=width>=S(520)&&pageHeight>0;Place(version,width-S(40)-S(190),footerY,S(190),S(24));
  }
  void LayoutGraphics(){
   int padding=S(22),bw=PageWidth(graphicsPanel);int y=LayoutRows(displayRows,padding,bw,S(6));graphicsPanel.AutoScrollMinSize=new Size(0,y+S(8));
   bw=PageWidth(qualityPanel);y=LayoutRows(qualityRows,padding,bw,S(6));qualityPanel.AutoScrollMinSize=new Size(0,y+S(8));
  }
  void LayoutAudio(){
   int padding=S(22),bw=PageWidth(audioPanel);int y=LayoutRows(audioRows,padding,bw,S(6));audioPanel.AutoScrollMinSize=new Size(0,y+S(8));
  }
  void LayoutControllers(){
   int padding=S(22),bw=PageWidth(controllersPanel);int y=LayoutRows(controllerRows,padding,bw,S(6));
   y+=S(12);Place(instrumentTitle,padding,y,bw,S(36));y+=S(42);
   int columns=bw>=S(600)?4:bw>=S(320)?2:1,cardWidth=bw/columns,cardHeight=S(118);
   for(int i=0;i<playerCards.Length;i++){playerCards[i].UiScale=UiScale;Place(playerCards[i],padding+i%columns*cardWidth,y+i/columns*cardHeight,i==playerCards.Length-1?bw-i%columns*cardWidth:cardWidth,cardHeight);}
   y+=(int)Math.Ceiling(playerCards.Length/(double)columns)*cardHeight+S(8);controllersPanel.AutoScrollMinSize=new Size(0,y);
  }
  void LayoutFiles(){
   int padding=S(22),bw=PageWidth(filesPanel),y=S(16);
   if(bw>=S(470)){Place(path,padding,y,bw-S(166),S(62));Place(browse,padding+bw-S(150),y+S(10),S(150),S(42));y+=S(70);}
   else{Place(path,padding,y,bw,S(62));y+=S(68);Place(browse,padding,y,bw,S(38));y+=S(44);}
   int h=TextHeight(fileHint,bw,18);Place(fileHint,padding,y,bw,h);y+=h+S(2);
   h=TextHeight(filesStatus,bw,18);Place(filesStatus,padding,y,bw,h);y+=h+S(10);
   y=LayoutRows(fileRows,padding,bw,y);filesPanel.AutoScrollMinSize=new Size(0,y+S(8));
  }

  SettingRow AddRow(AeroPagePanel panel,List<SettingRow> list,string title,string hint,int height,int[] widths,params Control[] items) {
   var row=new SettingRow {Items=items,Widths=widths,Height=height};
   row.Title=AddLabel(panel,title,Rectangle.Empty,10.5f,FontStyle.Bold,Aero.Ink);row.Title.TextAlign=ContentAlignment.MiddleLeft;
   if(hint!=null){row.Hint=AddLabel(panel,hint,Rectangle.Empty,9,FontStyle.Regular,Muted);row.Hint.TextAlign=ContentAlignment.TopLeft;}
   foreach(var item in items)if(item.Parent!=panel)panel.Controls.Add(item);
   row.Line=new Divider();panel.Controls.Add(row.Line);list.Add(row);return row;
  }
  Label ValueLabel(AeroPagePanel panel,string text,Color color){var label=AddLabel(panel,text,Rectangle.Empty,9,FontStyle.Bold,color);label.TextAlign=ContentAlignment.MiddleRight;return label;}
  AeroCheckBox Toggle(string name,string label,bool value,string description) {
   return new AeroCheckBox {Name=name,Text="",Checked=value,AccessibleName=label,AccessibleDescription=description};
  }

  public LauncherForm(string initialXex,bool rendering) {
   preview=rendering;ShowInTaskbar=!rendering;settings=rendering?new Settings():GameFiles.LoadSettings();if(!String.IsNullOrEmpty(initialXex))settings.XexPath=initialXex;
   try { Icon=new Icon(Path.Combine(GameFiles.AppRoot,"launcher.ico")); } catch { }
   Text="The Beatles: Rock Band · PC";
   ClientSize=new Size(1000,700);MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;KeyPreview=true;WindowButtons(true);

   rail=new FlatPanel();Controls.Add(rail);
   railHome=new RailButton {GlyphKind=Glyph.Star,Name="railHome",Text="Home",AccessibleName="Home",Selected=true};railHome.Click+=delegate{Navigate(0,-1);};rail.Controls.Add(railHome);
   railSettings=new RailButton {GlyphKind=Glyph.Gear,Name="railSettings",Text="Settings",AccessibleName="Settings"};railSettings.Click+=delegate{Navigate(1,-1);};rail.Controls.Add(railSettings);

   // Home view
   crumb=AddLabel(this,"ROCK BAND: THE BEATLES  /  LAUNCHER",Rectangle.Empty,9,FontStyle.Bold,Muted);crumb.TextAlign=ContentAlignment.MiddleLeft;
   chip=AddLabel(this,"● Setup",Rectangle.Empty,9,FontStyle.Bold,Warning);chip.TextAlign=ContentAlignment.MiddleRight;
   homeRule=new Divider();Controls.Add(homeRule);homeRule2=new Divider();Controls.Add(homeRule2);
   heading=AddLabel(this,"Choose your game",Rectangle.Empty,34,FontStyle.Regular,Aero.Ink);heading.TextAlign=ContentAlignment.MiddleLeft;
   summary=AddLabel(this,"",Rectangle.Empty,15,FontStyle.Regular,Aero.Gold);summary.TextAlign=ContentAlignment.MiddleLeft;
   status=AddLabel(this,"Choose your executable to get started.",Rectangle.Empty,9,FontStyle.Regular,Muted);
   play=MakeButton("Play",Rectangle.Empty,true);play.Font=Aero.Display(25);((AeroButton)play).PlayIcon=true;play.Enabled=false;play.Click+=Play;Controls.Add(play);AcceptButton=play;
   homeRows=new NavItem[PageNames.Length];
   for(int i=0;i<homeRows.Length;i++){int target=i;var item=new NavItem {Name="home"+i,Text=PageNames[i],GlyphKind=PageGlyphs[i],AccessibleName=PageNames[i]};item.Click+=delegate{Navigate(1,target);};Controls.Add(item);homeRows[i]=item;}
   footerHint=AddLabel(this,"Settings save as you make them. Changes apply the next time the game starts.",Rectangle.Empty,10,FontStyle.Regular,Aero.Ink);footerHint.TextAlign=ContentAlignment.MiddleLeft;
   art=new SleeveArt();Controls.Add(art);
   homeControls=new Control[]{art,crumb,chip,homeRule,homeRule2,heading,summary,status,play,homeRows[0],homeRows[1],homeRows[2],homeRows[3],footerHint};

   // Settings view: sidebar menu + one page at a time
   side=new FlatPanel();Controls.Add(side);
   sideCrumb=AddLabel(side,"ROCK BAND  /  SETTINGS",Rectangle.Empty,9,FontStyle.Bold,Muted);sideCrumb.TextAlign=ContentAlignment.MiddleLeft;
   sideRule=new Divider();side.Controls.Add(sideRule);
   sideItems=new NavItem[PageNames.Length];
   for(int i=0;i<sideItems.Length;i++){int target=i;var item=new NavItem {Name="side"+i,Text=PageNames[i],Subtitle=PageHints[i],GlyphKind=PageGlyphs[i],AccessibleName=PageNames[i]};item.Click+=delegate{Navigate(1,target);};side.Controls.Add(item);sideItems[i]=item;}
   sideNote=AddLabel(side,"Changes save as you make them. Some take effect the next time the game starts.",Rectangle.Empty,8,FontStyle.Regular,Muted);sideNote.TextAlign=ContentAlignment.TopLeft;
   pageTitle=AddLabel(this,PageNames[0],Rectangle.Empty,30,FontStyle.Regular,Aero.Ink);pageTitle.TextAlign=ContentAlignment.MiddleLeft;
   pageSubtitle=AddLabel(this,PageSubtitles[0],Rectangle.Empty,9,FontStyle.Regular,Muted);pageSubtitle.TextAlign=ContentAlignment.MiddleLeft;
   settingsControls=new Control[]{side,pageTitle,pageSubtitle};

   graphicsPanel=new AeroPagePanel {AutoScroll=true};qualityPanel=new AeroPagePanel {AutoScroll=true};
   audioPanel=new AeroPagePanel {AutoScroll=true};controllersPanel=new AeroPagePanel {AutoScroll=true};filesPanel=new AeroPagePanel {AutoScroll=true};
   tabs=new AeroTabs();tabs.AddPage("Display & frame rate",graphicsPanel);tabs.AddPage("Image quality",qualityPanel);Controls.Add(tabs);
   Controls.Add(audioPanel);Controls.Add(controllersPanel);Controls.Add(filesPanel);

   // Graphics > Display & frame rate
   presetLow=MakeButton("Low",Rectangle.Empty,false);presetLow.Name="presetLow";presetLow.AccessibleDescription="Apply a low graphics preset";presetLow.Click+=delegate{ApplyGraphicsPreset(0);};
   presetMedium=MakeButton("Medium",Rectangle.Empty,false);presetMedium.Name="presetMedium";presetMedium.AccessibleDescription="Apply a medium graphics preset";presetMedium.Click+=delegate{ApplyGraphicsPreset(1);};
   presetHigh=MakeButton("High",Rectangle.Empty,false);presetHigh.Name="presetHigh";presetHigh.AccessibleDescription="Apply a high graphics preset";presetHigh.Click+=delegate{ApplyGraphicsPreset(2);};
   AddRow(graphicsPanel,displayRows,"Preset","Sets resolution, internal scale, filtering and anti-aliasing together. Changes apply when the game starts.",34,new[]{84,84,84},presetLow,presetMedium,presetHigh);
   resolution=new AeroCombo {AccessibleName="Resolution"};resolution.Items.AddRange(new object[]{"960 × 540","1280 × 720","1600 × 900","1920 × 1080","2560 × 1440"});resolution.SelectedIndex=ResolutionIndex(settings.Width,settings.Height);
   resolution.SelectedIndexChanged+=delegate{if(!updatingGraphicsControls)PersistUiSettings("Resolution");};
   AddRow(graphicsPanel,displayRows,"Resolution","The window or fullscreen output size.",34,new[]{230},resolution);
   fullscreen=Toggle("fullscreen","Fullscreen",settings.Fullscreen,"Run the game fullscreen.");fullscreen.CheckedChanged+=delegate{PersistUiSettings("Fullscreen");};
   AddRow(graphicsPanel,displayRows,"Fullscreen",null,30,new[]{44},fullscreen);
   frameRate=new AeroCombo {Name="frameRate",AccessibleName="Frame rate limit"};frameRate.Items.AddRange(new object[]{"VSync","30 FPS","60 FPS","75 FPS","120 FPS","144 FPS"});frameRate.SelectedIndex=FrameRateIndex(settings.FrameRateLimit);
   frameRate.SelectedIndexChanged+=FrameRateChanged;
   AddRow(graphicsPanel,displayRows,"Frame rate","Fixed rates also enable VSync.",34,new[]{230},frameRate);

   // Graphics > Image quality
   performanceMode=Toggle("performanceMode","Performance mode",settings.PerformanceMode,"Reduce shader compilation stalls during scene transitions. New effects may appear a few frames later. Applies on the next game start.");performanceMode.CheckedChanged+=PerformanceChanged;
   var performanceRow=AddRow(qualityPanel,qualityRows,"Performance mode","Smoother scene transitions. New effects may appear a few frames later. Applies on next start.",30,new[]{44},performanceMode);performanceHint=performanceRow.Hint;
   renderScaleValue=ValueLabel(qualityPanel,"100%",Aero.Gold);
   renderScale=new GlassSlider {Name="renderScale",AccessibleName="Internal resolution scale",AccessibleDescription="Increase the game's internal rendering resolution. Higher values use substantially more graphics memory and processing."};renderScale.ValueChanged+=GraphicsSliderChanged;
   var renderRow=AddRow(qualityPanel,qualityRows,"Internal resolution scale","Higher values use more GPU and video memory.",30,new[]{56,230},renderScaleValue,renderScale);renderScaleLabel=renderRow.Title;
   anisotropicValue=ValueLabel(qualityPanel,"4×",Aero.Gold);
   anisotropic=new GlassSlider {Name="anisotropic",AccessibleName="Anisotropic filtering",AccessibleDescription="Sharper textures viewed at an angle; higher levels may reduce performance."};anisotropic.ValueChanged+=GraphicsSliderChanged;
   var anisotropicRow=AddRow(qualityPanel,qualityRows,"Anisotropic filtering","Sharper textures at an angle.",30,new[]{56,230},anisotropicValue,anisotropic);anisotropicLabel=anisotropicRow.Title;
   antiAliasing=new AeroCombo {Name="antiAliasing",AccessibleName="Anti-aliasing"};antiAliasing.Items.AddRange(new object[]{"Off","FXAA"});antiAliasing.SelectedIndexChanged+=GraphicsControlChanged;
   AddRow(qualityPanel,qualityRows,"Anti-aliasing","Smooths jagged edges.",34,new[]{230},antiAliasing);

   // Audio
   dolbyDigital=Toggle("dolbyDigital","Dolby Digital (Experimental)",settings.DolbyDigital,"Experimental Dolby Digital 5.1 over HDMI. When disabled, the game uses stereo PCM.");dolbyDigital.CheckedChanged+=DolbyDigitalChanged;
   AddRow(audioPanel,audioRows,"Dolby Digital (Experimental)","Stereo PCM is used by default. Dolby Digital requires an HDMI audio device.",30,new[]{44},dolbyDigital);
   audioControls=MakeButton("Open",Rectangle.Empty,false);audioControls.Click+=EditControls;
   AddRow(audioPanel,audioRows,"Audio & microphone","Microphone, game audio delay and buffer options are in Edit controls.",36,new[]{150},audioControls);

   // Controllers
   userBox=new TextBox {MaxLength=15,Text=settings.UserName,BorderStyle=BorderStyle.FixedSingle,Font=Aero.UI(11,FontStyle.Regular),ForeColor=Aero.Plum,BackColor=Aero.Cream};
   userBox.Leave+=delegate{userBox.Text=GameFiles.CleanUserName(userBox.Text);PersistUiSettings("User name");};
   var userRow=AddRow(controllersPanel,controllerRows,"User name (shown in game)",null,34,new[]{230},userBox);userLabel=userRow.Title;
   editControls=MakeButton("Edit controls",Rectangle.Empty,false);editControls.Click+=EditControls;
   AddRow(controllersPanel,controllerRows,"Controls","Key bindings, devices and instruments for each player.",36,new[]{150},editControls);
   instrumentTitle=AddLabel(controllersPanel,"Players",Rectangle.Empty,18,FontStyle.Regular,Aero.Coral);
   for(int i=0;i<playerCards.Length;i++){playerCards[i]=new PlayerInstrumentCard {Player="Player "+(i+1)};controllersPanel.Controls.Add(playerCards[i]);}

   // Game files & saves
   path=new FileDisplay {Value=settings.XexPath};filesPanel.Controls.Add(path);
   browse=MakeButton("Choose XEX",Rectangle.Empty,false);browse.Click+=Browse;filesPanel.Controls.Add(browse);
   fileHint=AddLabel(filesPanel,"Game files are read from their original folder.",Rectangle.Empty,9,FontStyle.Regular,Muted);
   filesStatus=AddLabel(filesPanel,status.Text,Rectangle.Empty,9,FontStyle.Regular,Muted);
   status.TextChanged+=delegate{filesStatus.Text=status.Text;RefreshLayout();};
   status.ForeColorChanged+=delegate{filesStatus.ForeColor=status.ForeColor;};
   saves=MakeButton("Open folder",Rectangle.Empty,false);saves.Click+=delegate{OpenFolder("userdata");};
   AddRow(filesPanel,fileRows,"Saves","Profiles and progress stored beside the launcher.",36,new[]{150},saves);
   logs=MakeButton("Open folder",Rectangle.Empty,false);logs.Click+=delegate{OpenFolder("logs");};
   AddRow(filesPanel,fileRows,"Logs","Session logs for troubleshooting.",36,new[]{150},logs);
   importDlc=MakeButton("Import DLC",Rectangle.Empty,false);importDlc.Click+=delegate{using(var manager=new DlcForm())manager.ShowDialog(this);};
   AddRow(filesPanel,fileRows,"Downloadable content","Install additional song packages.",36,new[]{150},importDlc);

   RefreshGraphicsControls();
   version=AddLabel(this,"V1.2.2 by Ju4nM4rt1n",Rectangle.Empty,10,FontStyle.Regular,Aero.Ink);version.TextAlign=ContentAlignment.MiddleRight;
   ApplyView();RefreshControls();RefreshLayout();
   if(!rendering){deviceStatusTimer=new Timer{Interval=2000};deviceStatusTimer.Tick+=delegate{if(Visible)RefreshPlayerCards();};deviceStatusTimer.Start();}
   if(rendering){check=GameFiles.Check(settings.XexPath);ApplyCheck();}
   else Shown+=async delegate {if(!String.IsNullOrWhiteSpace(settings.XexPath))await ValidateSelection();};
  }

  // view 0 = home, 1 = settings; page selects the settings section (-1 keeps the current one).
  public void Navigate(int newView,int newPage) {
   view=newView==0?0:1;if(newPage>=0)page=Math.Min(PageNames.Length-1,newPage);
   ApplyView();RefreshLayout();
  }
  void ApplyView() {
   bool home=view==0;
   foreach(var control in homeControls)control.Visible=home;
   foreach(var control in settingsControls)control.Visible=!home;
   tabs.Visible=!home&&page==0;audioPanel.Visible=!home&&page==1;controllersPanel.Visible=!home&&page==2;filesPanel.Visible=!home&&page==3;
   railHome.Selected=home;railSettings.Selected=!home;railHome.Invalidate();railSettings.Invalidate();
   for(int i=0;i<sideItems.Length;i++){sideItems[i].Selected=page==i;sideItems[i].Invalidate();}
   pageTitle.Text=PageNames[page];pageSubtitle.Text=PageSubtitles[page];
   if(home)AcceptButton=play;else AcceptButton=null;
   Invalidate(true);
  }
  protected override bool ProcessCmdKey(ref Message message,Keys keys) {
   if(keys==Keys.Escape&&view==1){Navigate(0,-1);return true;}
   return base.ProcessCmdKey(ref message,keys);
  }

  string PresetName() {
   int resolutionIndex=resolution.SelectedIndex,scale=RenderScaleFromSlider(renderScale.Value),filtering=AnisotropicFromSlider(anisotropic.Value),aa=antiAliasing.SelectedIndex;
   if(resolutionIndex==0&&scale==1&&filtering==0&&aa==0)return "Low";
   if(resolutionIndex==1&&scale==1&&filtering==3&&aa==0)return "Medium";
   if(resolutionIndex==3&&scale==2&&filtering==5&&aa==1)return "High";
   return "Custom";
  }
  void RefreshHome() {
   if(version==null||homeRows==null||path==null||dolbyDigital==null||resolution==null||frameRate==null||antiAliasing==null)return;
   string preset=PresetName();
   ((AeroButton)presetLow).Highlight=preset=="Low";((AeroButton)presetMedium).Highlight=preset=="Medium";((AeroButton)presetHigh).Highlight=preset=="High";presetLow.Invalidate();presetMedium.Invalidate();presetHigh.Invalidate();
   summary.Text=resolution.Text+"  ·  "+frameRate.Text+"  ·  "+(fullscreen.Checked?"Fullscreen":"Windowed");
   int assigned=0;foreach(var device in new[]{settings.InputDevice,settings.Player2Device,settings.Player3Device,settings.Player4Device})if(!String.IsNullOrEmpty(device))assigned++;
   bool ready=check!=null&&check.Ready,hasGame=!String.IsNullOrWhiteSpace(path.Value);
   homeRows[0].Value=preset;homeRows[1].Value=dolbyDigital.Checked?"Dolby Digital":"Stereo";
   homeRows[2].Value=assigned==0?"No devices":assigned==1?"1 player":assigned+" players";
   homeRows[3].Value=validating?"Checking":ready?"Ready":hasGame?"Needs attention":"Choose game";
   for(int i=0;i<homeRows.Length;i++)homeRows[i].Invalidate();
   heading.Text=validating?"Checking your game":ready?"Ready to play":hasGame?"Needs attention":"Choose your game";
   chip.Text=validating?"● Checking":ready?"● Ready":"● Not ready";chip.ForeColor=validating?Muted:ready?Aero.Teal:Warning;
  }
  void RefreshControls() {
   settings.Keys=Kbd.Normalize(settings.Keys);settings.DrumKeys=Kbd.Normalize(settings.DrumKeys,"drums");
   instrumentTitle.Text="Players";RefreshPlayerCards();RefreshLayout();
  }
  void RefreshPlayerCards(){
   var devices=ControllerDevices.List();string[] assignedDevices={settings.InputDevice,settings.Player2Device,settings.Player3Device,settings.Player4Device};string[] instruments={settings.Instrument,settings.Player2Instrument,settings.Player3Instrument,settings.Player4Instrument};
   for(int i=0;i<playerCards.Length;i++){playerCards[i].Instrument=instruments[i];playerCards[i].Player="Player "+(i+1);playerCards[i].Device=PlayerInstrumentCard.DeviceCaption(assignedDevices[i],devices);}
   RefreshHome();RefreshLayout();
  }
  void EditControls(object sender,EventArgs args) {
   using(var editor=new ControlsForm(settings,false)) {
    if(editor.ShowDialog(this)!=DialogResult.OK)return;
    var previous=settings.Keys;var previousDevice=settings.InputDevice;var previousProfiles=settings.DeviceBindings;var previousDrums=settings.DrumKeys;var previousDrumProfiles=settings.DrumDeviceBindings;var previousInstrument=settings.Instrument;var previousPlayer2Device=settings.Player2Device;var previousPlayer2Instrument=settings.Player2Instrument;var previousPlayer3Device=settings.Player3Device;var previousPlayer4Device=settings.Player4Device;var previousPlayer4Instrument=settings.Player4Instrument;    var previousPlayer3Instrument=settings.Player3Instrument;var previousMicrophone=settings.Microphone;
        settings.Microphone=editor.Microphone;settings.MicGain=editor.MicGain;settings.AudioBuffer=editor.AudioBuffer;settings.MicLatency=editor.MicLatency;    settings.Keys=editor.Bindings;settings.InputDevice=editor.DeviceId;settings.DeviceBindings=editor.DeviceProfiles;settings.Instrument=editor.Instrument;settings.DrumKeys=editor.DrumBindings;settings.DrumDeviceBindings=editor.DrumDeviceProfiles;settings.Player2Device=editor.Player2DeviceId;settings.Player2Instrument=editor.Player2Instrument;settings.Player3Device=editor.Player3DeviceId;settings.Player3Instrument=editor.Player3Instrument;settings.Player4Device=editor.Player4DeviceId;settings.Player4Instrument=editor.Player4Instrument;
    try {GameFiles.WriteJson(GameFiles.SettingsPath,settings);RefreshControls();status.Text="Controls saved. They apply the next time you start the game.";status.ForeColor=Muted;}
    catch(Exception error) {settings.Keys=previous;settings.InputDevice=previousDevice;settings.DeviceBindings=previousProfiles;settings.DrumKeys=previousDrums;settings.DrumDeviceBindings=previousDrumProfiles;settings.Instrument=previousInstrument;settings.Player2Device=previousPlayer2Device;settings.Player2Instrument=previousPlayer2Instrument;settings.Player3Device=previousPlayer3Device;settings.Player3Instrument=previousPlayer3Instrument;settings.Player4Device=previousPlayer4Device;settings.Player4Instrument=previousPlayer4Instrument;settings.Microphone=previousMicrophone;MessageBox.Show(this,"Could not save controls.\n\n"+error.Message,"Controls",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void PerformanceChanged(object sender,EventArgs args) {
   bool previous=settings.PerformanceMode;settings.PerformanceMode=performanceMode.Checked;
   if(!PersistUiSettings("Performance mode")) {
    performanceMode.CheckedChanged-=PerformanceChanged;settings.PerformanceMode=previous;performanceMode.Checked=previous;performanceMode.CheckedChanged+=PerformanceChanged;
   }
  }
  static int ResolutionIndex(int width,int height) {
   for(int i=0;i<ResolutionWidths.Length;i++)if(ResolutionWidths[i]==width&&ResolutionHeights[i]==height)return i;
   long pixels=(long)Math.Max(1,width)*Math.Max(1,height);int nearest=0;long distance=long.MaxValue;
   for(int i=0;i<ResolutionWidths.Length;i++){long current=Math.Abs((long)ResolutionWidths[i]*ResolutionHeights[i]-pixels);if(current<distance){nearest=i;distance=current;}}
   return nearest;
  }
  static int RenderScaleFromSlider(int value){return 1+(int)Math.Round(Math.Max(0,Math.Min(100,value))/50.0,MidpointRounding.AwayFromZero);}
  static int AnisotropicFromSlider(int value){return (int)Math.Round(Math.Max(0,Math.Min(100,value))/20.0,MidpointRounding.AwayFromZero);}
  static string AnisotropicCaption(int value){return new[]{"Off","1×","2×","4×","8×","16×"}[Math.Max(0,Math.Min(5,value))];}
  static int FrameRateIndex(int value){switch(value){case 30:return 1;case 60:return 2;case 75:return 3;case 120:return 4;case 144:return 5;default:return 0;}}
  static int FrameRateValue(int index){return index>=1&&index<=5?new[]{0,30,60,75,120,144}[index]:0;}
  void RefreshGraphicsControls() {
    updatingGraphicsControls=true;
    renderScale.Value=(Math.Max(1,Math.Min(3,settings.ResolutionScale))-1)*50;
    anisotropic.Value=Math.Max(0,Math.Min(5,settings.AnisotropicOverride))*20;
    antiAliasing.SelectedIndex=settings.AntiAliasing=="fxaa"?1:0;
    frameRate.SelectedIndex=FrameRateIndex(settings.FrameRateLimit);
    updatingGraphicsControls=false;
    renderScaleValue.Text=RenderScaleFromSlider(renderScale.Value)*100+"%";
    anisotropicValue.Text=AnisotropicCaption(AnisotropicFromSlider(anisotropic.Value));
    RefreshHome();
   }
  void GraphicsSliderChanged(object sender,EventArgs args) {
    if(updatingGraphicsControls)return;
    var slider=(GlassSlider)sender;int value=slider==renderScale?(RenderScaleFromSlider(slider.Value)-1)*50:AnisotropicFromSlider(slider.Value)*20;
    if(slider.Value!=value){updatingGraphicsControls=true;slider.Value=value;updatingGraphicsControls=false;}
    renderScaleValue.Text=RenderScaleFromSlider(renderScale.Value)*100+"%";
    anisotropicValue.Text=AnisotropicCaption(AnisotropicFromSlider(anisotropic.Value));
    SaveGraphicsControls();
   }
  void GraphicsControlChanged(object sender,EventArgs args){if(!updatingGraphicsControls)SaveGraphicsControls();}
  void SaveGraphicsControls() {
    int previousScale=settings.ResolutionScale,previousAnisotropic=settings.AnisotropicOverride;string previousAntiAliasing=settings.AntiAliasing;
    settings.ResolutionScale=RenderScaleFromSlider(renderScale.Value);settings.AnisotropicOverride=AnisotropicFromSlider(anisotropic.Value);
    settings.AntiAliasing=antiAliasing.SelectedIndex==1?"fxaa":"none";
    if(!PersistUiSettings("Graphics settings")){settings.ResolutionScale=previousScale;settings.AnisotropicOverride=previousAnisotropic;settings.AntiAliasing=previousAntiAliasing;RefreshGraphicsControls();}
   }
  void FrameRateChanged(object sender,EventArgs args){
   if(updatingGraphicsControls)return;
   int previous=settings.FrameRateLimit;settings.FrameRateLimit=FrameRateValue(frameRate.SelectedIndex);
   if(!PersistUiSettings("Frame rate")){settings.FrameRateLimit=previous;updatingGraphicsControls=true;frameRate.SelectedIndex=FrameRateIndex(previous);updatingGraphicsControls=false;}
  }
  void ApplyGraphicsPreset(int preset) {
    int previousWidth=settings.Width,previousHeight=settings.Height,previousScale=settings.ResolutionScale,previousAnisotropic=settings.AnisotropicOverride;
    string previousAntiAliasing=settings.AntiAliasing;
    updatingGraphicsControls=true;
    if(preset==0){resolution.SelectedIndex=0;renderScale.Value=0;anisotropic.Value=0;antiAliasing.SelectedIndex=0;}
    else if(preset==1){resolution.SelectedIndex=1;renderScale.Value=0;anisotropic.Value=60;antiAliasing.SelectedIndex=0;}
    else{resolution.SelectedIndex=3;renderScale.Value=50;anisotropic.Value=100;antiAliasing.SelectedIndex=1;}
    updatingGraphicsControls=false;
    renderScaleValue.Text=RenderScaleFromSlider(renderScale.Value)*100+"%";anisotropicValue.Text=AnisotropicCaption(AnisotropicFromSlider(anisotropic.Value));
    settings.Width=ResolutionWidths[resolution.SelectedIndex];settings.Height=ResolutionHeights[resolution.SelectedIndex];settings.ResolutionScale=RenderScaleFromSlider(renderScale.Value);settings.AnisotropicOverride=AnisotropicFromSlider(anisotropic.Value);
    settings.AntiAliasing=antiAliasing.SelectedIndex==1?"fxaa":"none";
    if(!PersistUiSettings("Graphics preset")){settings.Width=previousWidth;settings.Height=previousHeight;settings.ResolutionScale=previousScale;settings.AnisotropicOverride=previousAnisotropic;settings.AntiAliasing=previousAntiAliasing;resolution.SelectedIndex=ResolutionIndex(previousWidth,previousHeight);RefreshGraphicsControls();}
   }
  void DolbyDigitalChanged(object sender,EventArgs args) {
    bool previous=settings.DolbyDigital;settings.DolbyDigital=dolbyDigital.Checked;
    if(!PersistUiSettings("Dolby Digital")) {
     dolbyDigital.CheckedChanged-=DolbyDigitalChanged;settings.DolbyDigital=previous;dolbyDigital.Checked=previous;dolbyDigital.CheckedChanged+=DolbyDigitalChanged;
    }
  }
  bool PersistUiSettings(string setting){
   if(preview){RefreshHome();return true;}
   try{
    int index=Math.Max(0,resolution.SelectedIndex);
    settings.XexPath=path.Value;settings.Width=ResolutionWidths[index];settings.Height=ResolutionHeights[index];
    settings.Fullscreen=fullscreen.Checked;settings.PerformanceMode=performanceMode.Checked;settings.DolbyDigital=dolbyDigital.Checked;
    settings.ResolutionScale=RenderScaleFromSlider(renderScale.Value);settings.AnisotropicOverride=AnisotropicFromSlider(anisotropic.Value);
    settings.AntiAliasing=antiAliasing.SelectedIndex==1?"fxaa":"none";settings.FrameRateLimit=FrameRateValue(frameRate.SelectedIndex);settings.VSync=true;
    settings.UserName=GameFiles.CleanUserName(userBox.Text);
    GameFiles.WriteJson(GameFiles.SettingsPath,settings);RefreshHome();return true;
   }catch(Exception error){
    MessageBox.Show(this,"Could not save "+setting.ToLowerInvariant()+".\n\n"+error.Message,setting,MessageBoxButtons.OK,MessageBoxIcon.Error);return false;
   }
  }
  static Label AddLabel(Control parent,string text,Rectangle box,float size,FontStyle style,Color color) {
   return Aero.Label(parent,text,box,size>=16?Aero.Display(size):Aero.UI(size,style),color);
  }
  static Button MakeButton(string text,Rectangle box,bool primary) {
   return Aero.Button(text,box,primary);
  }
  async void Browse(object sender,EventArgs args) {
   using(var dialog=new OpenFileDialog {Title="Choose The Beatles: Rock Band executable",Filter="Game executable (default.xex)|default.xex|Xbox 360 executables (*.xex)|*.xex",CheckFileExists=true,Multiselect=false}) {
    if(!String.IsNullOrWhiteSpace(settings.XexPath)&&File.Exists(settings.XexPath))dialog.InitialDirectory=Path.GetDirectoryName(settings.XexPath);
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;settings.XexPath=dialog.FileName;path.Value=dialog.FileName;PersistUiSettings("Game executable");await ValidateSelection();
   }
  }
  async Task ValidateSelection() {
   int generation=++validationGeneration;play.Enabled=false;validating=true;status.ForeColor=Muted;status.Text="Checking the executable and disc files…";RefreshHome();
   string selected=settings.XexPath;var result=await Task.Run(delegate {return GameFiles.Check(selected);});
   if(IsDisposed||generation!=validationGeneration)return;validating=false;check=result;ApplyCheck();
  }
  void ApplyCheck() {status.Text=check.Ready?"Your game is ready.":check.Message;status.ForeColor=check.Ready?Aero.Teal:Warning;play.Enabled=check.Ready;RefreshHome();Invalidate();}
  async void Play(object sender,EventArgs args) {
   await ValidateSelection();if(check==null||!check.Ready)return;
   if(Process.GetProcessesByName("beatlesrockband-guitar").Any()) {MessageBox.Show(this,"A game is already running. Close it before starting another.","Game already running",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
    var assignedDevices=new[]{settings.InputDevice,settings.Player2Device,settings.Player3Device,settings.Player4Device};for(int i=0;i<assignedDevices.Length;i++){string id=assignedDevices[i];if(String.IsNullOrEmpty(id)||id=="keyboard")continue;ControllerDevices.State state;if(!ControllerDevices.Read(id,out state)){MessageBox.Show(this,"Player "+(i+1)+" controller is disconnected. Reconnect it or choose another device in Edit controls.","Connect your controller",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}}
   userBox.Text=GameFiles.CleanUserName(userBox.Text);if(!PersistUiSettings("Game settings"))return;
      try {
       play.Enabled=false;game=GameFiles.Start(settings,check.Root);if(game==null)throw new Exception("Windows could not start the game.");
    game.Exited+=GameExited;game.EnableRaisingEvents=true;status.Text="The game is running.";Hide();
   } catch(Exception error) {play.Enabled=true;MessageBox.Show(this,"Could not start the game. Extract the package to a writable folder.\n\n"+error.Message,"Could not start",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void GameExited(object sender,EventArgs args) {
   if(IsDisposed||Disposing)return;
   try {BeginInvoke(new Action(delegate {
    int code=game.ExitCode;game.Dispose();game=null;Show();Activate();play.Enabled=check!=null&&check.Ready;
    status.Text=code==0?"Game closed. Ready to play again.":"The game closed. This session’s log is in Logs.";
    status.ForeColor=code==0?Muted:Warning;
   }));}catch(InvalidOperationException){}
  }
  void OpenFolder(string name) {try {string folder=Path.Combine(GameFiles.AppRoot,name);Directory.CreateDirectory(folder);Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});}catch(Exception error){MessageBox.Show(this,error.Message,"Could not open folder");}}

  protected override bool ShowWithoutActivation {get{return preview;}}
  protected override void Dispose(bool disposing){if(disposing&&deviceStatusTimer!=null){deviceStatusTimer.Stop();deviceStatusTimer.Dispose();deviceStatusTimer=null;}base.Dispose(disposing);}
  public void Render(string output) {Show();Application.DoEvents();PerformLayout();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);}}
 }
 static class Program {
  [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
  [STAThread]static int Main(string[] args) {
   try {
    if(args.Length>=3&&args[0]=="--self-test") {
     var result=GameFiles.Check(args[2]);var settings=new Settings {XexPath=args[2]};
     File.WriteAllText(args[1],"ready="+result.Ready+"\nmessage="+result.Message+"\nhash="+result.Hash+"\narguments="+GameFiles.Arguments(settings,result.Root??"C:/No game",Path.Combine(GameFiles.AppRoot,"logs","test.log")),System.Text.Encoding.UTF8);return result.Ready?0:2;
    }
    if(args.Length>=3&&args[0]=="--launch-once") {
     var result=GameFiles.Check(args[2]);if(!result.Ready){File.WriteAllText(args[1],result.Message);return 2;}
     var process=GameFiles.Start(new Settings {XexPath=args[2]},result.Root);File.WriteAllText(args[1],process.Id.ToString());return 0;
    }
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    if(args.Length>=2&&args[0]=="--render-icon") {SleeveArt.SaveIcon(args[1]);return 0;}
    if(args.Length>=2&&args[0]=="--render-controls") {using(var form=new ControlsForm(Kbd.Defaults(),true)){form.Render(args[1]);}return 0;}
    if(args.Length>=2&&args[0]=="--render-preview") {
     // Optional: --render-preview <png> [xex] [settings page 0-3]
     using(var form=new LauncherForm(args.Length>2&&args[2]!="-"?args[2]:null,true)){int pageIndex;if(args.Length>3&&Int32.TryParse(args[3],out pageIndex))form.Navigate(1,pageIndex);form.Render(args[1]);}return 0;
    }
    Application.Run(new LauncherForm(args.Length>1&&args[0]=="--xex"?args[1]:null,false));return 0;
   }catch(Exception error){if(args.Length>1&&args[0].StartsWith("--"))File.WriteAllText(args[1],error.ToString());else MessageBox.Show(error.Message,"The Beatles: Rock Band PC",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
  }
 }
}
