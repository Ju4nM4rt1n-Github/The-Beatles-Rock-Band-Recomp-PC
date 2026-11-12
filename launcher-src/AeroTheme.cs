using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BeatlesLauncher {
 public static class Aero {
  // Night-sky palette of the Beatles menus: plum background, cream text, coral accents.
  public static readonly Color Ink=Color.FromArgb(255,243,214),Muted=Color.FromArgb(236,160,170),Cream=Color.FromArgb(255,241,214),Teal=Color.FromArgb(158,222,150),Coral=Color.FromArgb(236,106,120),Plum=Color.FromArgb(44,20,56),Gold=Color.FromArgb(255,226,120);
  public static void Star(Graphics g,Brush brush,float cx,float cy,float outer,float inner){
   var points=new PointF[10];for(int i=0;i<10;i++){double angle=-Math.PI/2+i*Math.PI/5;float radius=i%2==0?outer:inner;points[i]=new PointF(cx+(float)Math.Cos(angle)*radius,cy+(float)Math.Sin(angle)*radius);}
   g.FillPolygon(brush,points);
  }
  static readonly PrivateFontCollection fonts=new PrivateFontCollection();static IntPtr fontMemory;
  [DllImport("gdi32.dll")]static extern IntPtr AddFontMemResourceEx(IntPtr data,uint length,IntPtr reserved,ref uint count);
  static Aero() {
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BeatlesLauncher.Display.ttf")) {
    if(stream==null)return;var bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);fontMemory=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,fontMemory,bytes.Length);fonts.AddMemoryFont(fontMemory,bytes.Length);uint count=0;AddFontMemResourceEx(fontMemory,(uint)bytes.Length,IntPtr.Zero,ref count);
   }
  }
  public static Font Display(float size){return fonts.Families.Length>0?new Font(fonts.Families[0],size*96f/72,FontStyle.Regular,GraphicsUnit.Pixel):new Font("Georgia",size*96f/72,FontStyle.Regular,GraphicsUnit.Pixel);}
  public static Font UI(float size,FontStyle style=FontStyle.Regular){return new Font("Georgia",size*96f/72,size<=9.5f&&style==FontStyle.Regular?FontStyle.Regular:style|FontStyle.Bold,GraphicsUnit.Pixel);}
  public static GraphicsPath Round(RectangleF r,float radius) {
   var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
   p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
  }
  public static Color Mix(Color a,Color b,float t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
  public static void Text(Graphics g,string value,Font font,Color color,RectangleF bounds,StringAlignment align=StringAlignment.Near) {
   using(var brush=new SolidBrush(color))using(var format=new StringFormat {Alignment=align,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter})g.DrawString(value,font,brush,bounds,format);
  }
  public static Label Label(Control parent,string value,Rectangle box,Font font,Color color) {
   var result=new AeroLabel {Text=value,Bounds=box,Font=font,ForeColor=color,AutoSize=false};parent.Controls.Add(result);return result;
  }
  static Point Origin(Control control,Control root){var point=Point.Empty;for(var p=control;p!=null&&p!=root;p=p.Parent)point.Offset(p.Left,p.Top);return point;}
  // Paint only the background layers. Asking WinForms to simulate transparency
  // on a Button/CheckBox replays sibling controls on the live Windows surface.
  public static void PaintBackground(Control control,Graphics graphics) {
   var state=graphics.Save();
   try {
    var window=control.FindForm() as AeroWindow;
    if(window==null){graphics.Clear(Cream);return;}
    var origin=Origin(control,window);graphics.TranslateTransform(-origin.X,-origin.Y);window.DrawBackdrop(graphics);
    var parents=new System.Collections.Generic.List<Control>();
    for(var p=control.Parent;p!=null&&p!=window;p=p.Parent){if(p is ISurface)parents.Add(p);}
    parents.Reverse();foreach(var surface in parents){var local=graphics.Save();var offset=Origin(surface,window);graphics.TranslateTransform(offset.X,offset.Y);((ISurface)surface).DrawSurface(graphics);graphics.Restore(local);}
   } finally {graphics.Restore(state);}
  }
  public static AeroButton Button(string value,Rectangle box,bool primary=false){return new AeroButton {Text=value,Bounds=box,Primary=primary,Font=UI(10,FontStyle.Bold)};}
 }
 // Panels that paint a translucent surface which child controls must repaint beneath themselves.
 public interface ISurface {void DrawSurface(Graphics g);}
 // The retro night scene: stars, concentric discs, clouds and rolling green hills.
 static class Retro {
  static void Disc(Graphics g,float cx,float cy,float r,int kind){
   Color[] outer={Color.FromArgb(226,92,124),Color.FromArgb(214,112,170),Color.FromArgb(250,172,128)},ring={Color.FromArgb(252,236,204),Color.FromArgb(255,232,196),Color.FromArgb(255,238,200)},inner={Color.FromArgb(240,154,122),Color.FromArgb(255,214,150),Color.FromArgb(232,92,128)};
   using(var b=new SolidBrush(outer[kind]))g.FillEllipse(b,cx-r,cy-r,r*2,r*2);
   using(var b=new SolidBrush(ring[kind]))g.FillEllipse(b,cx-r*.72f,cy-r*.72f,r*1.44f,r*1.44f);
   using(var b=new SolidBrush(inner[kind]))g.FillEllipse(b,cx-r*.52f,cy-r*.52f,r*1.04f,r*1.04f);
  }
  static void Cloud(Graphics g,float cx,float baseY,float s){
   float[][] lumps={new[]{-78f,6f,30f},new[]{-42f,-16f,42f},new[]{4f,-30f,50f},new[]{54f,-12f,40f},new[]{90f,8f,28f},new[]{0f,10f,44f}};
   using(var b=new SolidBrush(Color.FromArgb(238,134,138)))foreach(var l in lumps)g.FillEllipse(b,cx+(l[0]-l[2])*s,baseY+(l[1]-l[2]+7)*s,l[2]*2*s,l[2]*2*s);
   using(var b=new SolidBrush(Color.FromArgb(255,214,160)))foreach(var l in lumps)g.FillEllipse(b,cx+(l[0]-l[2]+3)*s,baseY+(l[1]-l[2]-3)*s,(l[2]*2-6)*s,(l[2]*2-6)*s);
   using(var b=new SolidBrush(Color.FromArgb(255,238,182)))foreach(var l in lumps)g.FillEllipse(b,cx+(l[0]-l[2]+6)*s,baseY+(l[1]-l[2]-6)*s,(l[2]*2-18)*s,(l[2]*2-18)*s);
  }
  static void Hill(Graphics g,int w,float y,float bend,Color top,Color bottom,int h){
   using(var p=new GraphicsPath()){p.AddBezier(-10,y,w*.28f,y-bend,w*.62f,y+bend,w+10,y-bend*.4f);p.AddLine(w+10,y-bend*.4f,w+10,h+4);p.AddLine(w+10,h+4,-10,h+4);p.CloseFigure();using(var b=new LinearGradientBrush(new RectangleF(0,y-bend,w,h-y+bend+4),top,bottom,90f))g.FillPath(b,p);}
  }
  public static Bitmap Render(Size size){
   int w=Math.Max(1,size.Width),h=Math.Max(1,size.Height);var bitmap=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
   using(var g=Graphics.FromImage(bitmap)){
    g.SmoothingMode=SmoothingMode.AntiAlias;float s=Math.Max(.8f,Math.Min(1.8f,Math.Min(w/1000f,h/700f)));
    using(var b=new LinearGradientBrush(new Rectangle(0,0,w,h),Color.Black,Color.Black,90f)){b.InterpolationColors=new ColorBlend{Colors=new[]{Color.FromArgb(22,9,34),Color.FromArgb(46,20,58),Color.FromArgb(98,46,86)},Positions=new[]{0f,.55f,1f}};g.FillRectangle(b,0,0,w,h);}
    var random=new Random(11);
    for(int i=0;i<44;i++){float x=(float)random.NextDouble()*w,y=(float)random.NextDouble()*h*.8f,size2=(1.5f+(float)random.NextDouble()*5f)*s;bool pink=random.Next(3)>0;
     using(var b=new SolidBrush(pink?Color.FromArgb(210,214,108,172):Color.FromArgb(235,255,240,200)))if(size2<3.2f*s)g.FillEllipse(b,x,y,size2,size2);else Aero.Star(g,b,x,y,size2,size2*.45f);}
    Disc(g,w*.985f,h*.2f,44*s,0);Disc(g,w*.04f,h*.58f,30*s,1);Disc(g,w*.62f,h*.03f,18*s,2);Disc(g,w*.30f,h*.01f,24*s,0);Disc(g,w*.99f,h*.46f,40*s,1);Disc(g,w*.01f,h*.18f,16*s,2);Disc(g,w*.70f,h*.12f,12*s,1);Disc(g,w*.20f,h*.57f,14*s,0);
    Cloud(g,w*.08f,h-70*s,1.1f*s);Cloud(g,w*.93f,h-84*s,1.2f*s);Cloud(g,w*.55f,h-62*s,.8f*s);
    Hill(g,w,h-96*s,26*s,Color.FromArgb(44,92,70),Color.FromArgb(30,70,56),h);Hill(g,w,h-72*s,-22*s,Color.FromArgb(78,142,88),Color.FromArgb(46,108,70),h);Hill(g,w,h-44*s,20*s,Color.FromArgb(130,188,104),Color.FromArgb(82,150,84),h);
    Disc(g,w*.41f,h-6*s,30*s,0);Disc(g,w*.66f,h-4*s,22*s,1);Disc(g,w*1.0f,h-2*s,34*s,2);
   }
   return bitmap;
  }
 }
 public class AeroWindow : Form {
  [DllImport("user32.dll")]static extern bool ReleaseCapture();
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
  public AeroWindow(){SetStyle(ControlStyles.ResizeRedraw|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);DoubleBuffered=true;BackColor=Aero.Cream;ForeColor=Aero.Ink;Font=Aero.UI(10);FormBorderStyle=FormBorderStyle.None;AutoScaleMode=AutoScaleMode.None;}
  protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ClassStyle|=0x20000;return p;}}
  public void WindowButtons(bool minimize) {
   var close=new AeroButton {Bounds=new Rectangle(ClientSize.Width-48,14,30,28),Chrome=2,TabStop=false,AccessibleName="Close",Anchor=AnchorStyles.Top|AnchorStyles.Right};close.Click+=delegate{Close();};Controls.Add(close);
   if(minimize){var min=new AeroButton {Bounds=new Rectangle(ClientSize.Width-83,14,30,28),Chrome=1,TabStop=false,AccessibleName="Minimize",Anchor=AnchorStyles.Top|AnchorStyles.Right};min.Click+=delegate{WindowState=FormWindowState.Minimized;};Controls.Add(min);}
  }
  public void DragWindow(){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&e.Y<53)DragWindow();}
  protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>1&&Height>1)using(var path=Aero.Round(new RectangleF(0,0,Width,Height),21)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
  Bitmap backdrop;Size backdropSize;
  internal void DrawBackdrop(Graphics g){
   if(backdrop==null||backdropSize!=ClientSize){if(backdrop!=null)backdrop.Dispose();backdropSize=ClientSize;backdrop=Retro.Render(ClientSize);}
   g.DrawImage(backdrop,0,0,backdrop.Width,backdrop.Height);
  }
  protected override void Dispose(bool disposing){if(disposing&&backdrop!=null){backdrop.Dispose();backdrop=null;}base.Dispose(disposing);}
  protected override void OnPaintBackground(PaintEventArgs e){DrawBackdrop(e.Graphics);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Aero.Round(new RectangleF(.5f,.5f,Width-1,Height-1),21))using(var pen=new Pen(Color.FromArgb(200,236,106,120),1.6f))e.Graphics.DrawPath(pen,path);}
 }
 public sealed class AeroLabel : Label {
  public AeroLabel(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);var align=TextAlign;using(var format=new StringFormat {Trimming=StringTrimming.EllipsisCharacter,Alignment=align==ContentAlignment.TopRight||align==ContentAlignment.MiddleRight||align==ContentAlignment.BottomRight?StringAlignment.Far:align==ContentAlignment.TopCenter||align==ContentAlignment.MiddleCenter||align==ContentAlignment.BottomCenter?StringAlignment.Center:StringAlignment.Near,LineAlignment=align==ContentAlignment.BottomLeft||align==ContentAlignment.BottomCenter||align==ContentAlignment.BottomRight?StringAlignment.Far:align==ContentAlignment.MiddleLeft||align==ContentAlignment.MiddleCenter||align==ContentAlignment.MiddleRight?StringAlignment.Center:StringAlignment.Near})using(var brush=new SolidBrush(ForeColor))e.Graphics.DrawString(Text,Font,brush,ClientRectangle,format);}
 }
 public sealed class AeroButton : Button {
  public bool Primary,PlayIcon,Link,Highlight;public int Chrome;
  bool hover,pressed;
  public AeroButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;ForeColor=Aero.Ink;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics;Aero.PaintBackground(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
   if(Chrome!=0){if(hover)using(var fill=new SolidBrush(Chrome==2?Color.FromArgb(70,236,106,120):Color.FromArgb(40,255,243,214)))using(var p=Aero.Round(new RectangleF(1,1,Width-2,Height-2),8))g.FillPath(fill,p);using(var p=new Pen(Aero.Ink,1.6f)){if(Chrome==1)g.DrawLine(p,10,16,20,16);else{g.DrawLine(p,11,9,19,17);g.DrawLine(p,19,9,11,17);}}return;}
   if(Link){Aero.Text(g,Text,Font,hover?Aero.Gold:Aero.Ink,new RectangleF(0,0,Width,Height),StringAlignment.Center);if(hover)using(var p=new Pen(Color.FromArgb(120,Aero.Gold)))g.DrawLine(p,12,Height-5,Width-12,Height-5);return;}
   var r=new RectangleF(1,1,Width-3,Height-5);float radius=Math.Min(r.Height/2f,Primary?26:14);
   Color top=Primary?Color.FromArgb(136,204,104):Highlight?Color.FromArgb(255,238,160):Color.FromArgb(84,44,100);
   Color bottom=Primary?Color.FromArgb(48,126,74):Highlight?Color.FromArgb(240,186,92):Color.FromArgb(50,24,66);
   if(hover){top=Aero.Mix(top,Color.White,.16f);bottom=Aero.Mix(bottom,Color.White,.1f);}if(pressed){top=Aero.Mix(top,Color.Black,.14f);bottom=Aero.Mix(bottom,Color.Black,.12f);}if(!Enabled){top=Primary?Color.FromArgb(78,96,84):Color.FromArgb(62,42,72);bottom=Primary?Color.FromArgb(52,66,58):Color.FromArgb(46,30,56);}
   using(var shadow=Aero.Round(new RectangleF(r.X,r.Y+3,r.Width,r.Height),radius))using(var b=new SolidBrush(Color.FromArgb(90,6,0,14)))g.FillPath(b,shadow);
   using(var p=Aero.Round(r,radius)){
    using(var b=new LinearGradientBrush(r,top,bottom,90f))g.FillPath(b,p);
    var state=g.Save();g.SetClip(p);using(var b=new LinearGradientBrush(new RectangleF(0,r.Y,Width,r.Height*.5f),Color.FromArgb(Primary||Highlight?95:34,255,255,255),Color.FromArgb(6,255,255,255),90f))g.FillRectangle(b,0,r.Y,Width,r.Height*.5f);g.Restore(state);
    using(var line=new Pen(Primary?Color.FromArgb(200,255,243,214):Highlight?Color.FromArgb(200,255,250,220):hover?Color.FromArgb(240,255,226,150):Color.FromArgb(230,236,106,120),Primary?1.8f:1.5f))g.DrawPath(line,p);
   }
   var textRect=new RectangleF(4,0,Width-8,Height-4);
   if(PlayIcon){float cy=(Height-4)/2f;using(var fill=new SolidBrush(Color.FromArgb(Enabled?60:30,255,243,214)))g.FillEllipse(fill,23,cy-17,34,34);using(var b=new SolidBrush(Enabled?Aero.Gold:Color.FromArgb(150,170,150)))Aero.Star(g,b,40,cy+1,13,5.6f);textRect.X+=20;textRect.Width-=20;}
   Color color=!Enabled?(Primary?Color.FromArgb(170,190,170):Color.FromArgb(142,118,150)):Primary?Color.FromArgb(255,248,224):Highlight?Aero.Plum:Aero.Ink;
   if(Primary&&Enabled){var shadow=textRect;shadow.Y+=1.5f;Aero.Text(g,Text,Font,Color.FromArgb(150,18,66,40),shadow,StringAlignment.Center);}Aero.Text(g,Text,Font,color,textRect,StringAlignment.Center);
   if(Focused&&ShowFocusCues)using(var p=new Pen(Color.FromArgb(200,Aero.Gold))){p.DashStyle=DashStyle.Dot;using(var path=Aero.Round(new RectangleF(6,6,Width-14,Height-16),Math.Max(3,radius-4)))g.DrawPath(p,path);}
  }
 }
 public sealed class Card : Panel, ISurface {
  public Card(){SetStyle(ControlStyles.Opaque|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  public void DrawSurface(Graphics g){g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Aero.Round(new RectangleF(1,1,Width-3,Height-3),14))using(var b=new LinearGradientBrush(ClientRectangle,Color.FromArgb(214,92,52,108),Color.FromArgb(214,58,30,74),90f))g.FillPath(b,p);}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);DrawSurface(e.Graphics);}
 }
 public sealed class AeroPagePanel : Panel, ISurface {
  public AeroPagePanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  public void DrawSurface(Graphics g){if(Width<2||Height<2)return;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var path=Aero.Round(new RectangleF(0,0,Width-1,Height-1),14))using(var fill=new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,Color.FromArgb(224,43,22,55),Color.FromArgb(218,32,15,43),90f))g.FillPath(fill,path);}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);DrawSurface(e.Graphics);base.OnPaint(e);}
 }
 public sealed class AeroTabs : Panel {
  readonly List<Panel> pages=new List<Panel>();
  readonly List<AeroButton> buttons=new List<AeroButton>();
  float scale=1;int selectedIndex;
  public float UiScale{get{return scale;}set{scale=Math.Max(.75f,value);Invalidate(true);PerformLayout();}}
  public int SelectedIndex{get{return selectedIndex;}set{if(value<0||value>=pages.Count||selectedIndex==value)return;selectedIndex=value;RefreshSelection();}}
  public int PageCount{get{return pages.Count;}}
  public string PageName(int index){return pages[index].Tag as string;}
  public AeroTabs(){SetStyle(ControlStyles.Opaque|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  public void AddPage(string title,Panel page){
   page.Tag=title;page.Visible=pages.Count==0;pages.Add(page);Controls.Add(page);
   var button=(AeroButton)Aero.Button(title,Rectangle.Empty,false);button.Font=Aero.UI(10,FontStyle.Bold);button.Click+=delegate{SelectedIndex=buttons.IndexOf(button);};buttons.Add(button);Controls.Add(button);RefreshSelection();PerformLayout();
  }
  void RefreshSelection(){for(int i=0;i<pages.Count;i++){pages[i].Visible=i==selectedIndex;buttons[i].Highlight=i==selectedIndex;if(i==selectedIndex)pages[i].BringToFront();}for(int i=0;i<buttons.Count;i++)buttons[i].BringToFront();Invalidate();}
  protected override void OnLayout(LayoutEventArgs e){
   base.OnLayout(e);int gap=(int)Math.Round(8*scale),header=(int)Math.Round(42*scale),buttonHeight=Math.Max(1,header-(int)Math.Round(4*scale));int buttonWidth=Math.Max(1,(Width-gap*(buttons.Count-1))/Math.Max(1,buttons.Count));
   for(int i=0;i<buttons.Count;i++)buttons[i].Bounds=new Rectangle(i*(buttonWidth+gap),(header-buttonHeight)/2, i==buttons.Count-1?Math.Max(1,Width-i*(buttonWidth+gap)):buttonWidth,buttonHeight);
   for(int i=0;i<pages.Count;i++)pages[i].Bounds=new Rectangle(0,header,Width,Math.Max(1,Height-header));
  }
  protected override void OnPaintBackground(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);base.OnPaint(e);}
 }
 public sealed class GlassSlider : Control {
  int value=100;bool dragging;public event EventHandler ValueChanged;
  public int Value{get{return value;}set{int next=Math.Max(0,Math.Min(100,value));if(this.value==next)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
  public GlassSlider(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.Slider;AccessibleName="Volume";}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float y=Height/2f,x=10+(Width-20)*Value/100f;using(var p=new Pen(Color.FromArgb(88,52,100),5)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,10,y,Width-10,y);}if(x>10)using(var b=new LinearGradientBrush(new RectangleF(9,y-3,Math.Max(2,x-8),6),Color.FromArgb(236,106,120),Color.FromArgb(255,214,110),0f))using(var p=new Pen(b,5)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,10,y,x,y);}using(var shadow=new SolidBrush(Color.FromArgb(80,6,0,14)))g.FillEllipse(shadow,x-9,y-7,18,18);using(var b=new LinearGradientBrush(new RectangleF(x-8,y-9,16,18),Color.FromArgb(255,252,236),Color.FromArgb(255,214,160),90f))g.FillEllipse(b,x-8,y-9,16,18);using(var p=new Pen(Color.FromArgb(220,236,106,120)))g.DrawEllipse(p,x-8,y-9,16,18);using(var p=new Pen(Color.White))g.DrawArc(p,x-6,y-7,12,13,200,140);if(Focused&&ShowFocusCues)using(var p=new Pen(Aero.Gold)){p.DashStyle=DashStyle.Dot;g.DrawEllipse(p,x-11,y-12,22,24);}}
  void FromMouse(int x){Value=(int)Math.Round((x-10)*100.0/Math.Max(1,Width-20));}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();dragging=true;Capture=true;FromMouse(e.X);}}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)FromMouse(e.X);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
  protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value++;else if(e.KeyCode==Keys.Home)Value=0;else if(e.KeyCode==Keys.End)Value=100;else return;e.Handled=true;base.OnKeyDown(e);}
  protected override void OnMouseWheel(MouseEventArgs e){Value+=e.Delta>0?5:-5;base.OnMouseWheel(e);}
 }
 public sealed class AeroCheckBox : CheckBox {
  public AeroCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;Cursor=Cursors.Hand;Font=Aero.UI(10);}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;Aero.PaintBackground(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;float s=Font.Size/(10*96f/72),y=(Height-19*s)/2;
   using(var path=Aero.Round(new RectangleF(2*s,y,34*s,19*s),9*s))using(var b=new SolidBrush(Checked?Color.FromArgb(104,190,100):Color.FromArgb(104,66,118)))g.FillPath(b,path);
   float x=(Checked?19:3)*s;using(var b=new SolidBrush(Color.White))g.FillEllipse(b,x,y+s,16*s,17*s);Aero.Text(g,Text,Font,Aero.Ink,new RectangleF(45*s,0,Math.Max(1,Width-45*s),Height));if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle((int)(43*s),3,Math.Max(1,Width-(int)(46*s)),Height-6));}

 }
 public sealed class AeroCombo : ComboBox {
  public AeroCombo(){  DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;ItemHeight=24;BackColor=Aero.Cream;ForeColor=Aero.Plum;Font=Aero.UI(10);}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Math.Max(24,Font.Height+8);}
    protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;using(var b=new SolidBrush((e.State&DrawItemState.Selected)!=0&&(e.State&DrawItemState.ComboBoxEdit)==0?Color.FromArgb(244,160,150):BackColor))e.Graphics.FillRectangle(b,e.Bounds);Aero.Text(e.Graphics,Items[e.Index].ToString(),Font,ForeColor,new RectangleF(e.Bounds.X+9,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height));}
 }
 public sealed class FileDisplay : Control {
  readonly ToolTip tip=new ToolTip {AutoPopDelay=15000};string value="";public string Value{get{return value;}set{this.value=value??"";AccessibleDescription=this.value;tip.SetToolTip(this,this.value);Invalidate();}}
  public FileDisplay(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;AccessibleName="Game file";}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;Aero.PaintBackground(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Aero.Round(new RectangleF(1,1,Width-3,Height-3),10)){using(var b=new LinearGradientBrush(ClientRectangle,Color.FromArgb(96,52,108),Color.FromArgb(60,30,76),90f))g.FillPath(b,p);using(var pen=new Pen(Color.FromArgb(220,236,106,120),1.4f))g.DrawPath(pen,p);}float scale=Height/62f;g.ScaleTransform(scale,scale);float logicalWidth=Width/scale;using(var b=new LinearGradientBrush(new RectangleF(14,15,29,29),Color.FromArgb(255,238,160),Color.FromArgb(236,106,120),45f))g.FillEllipse(b,14,15,29,29);using(var p=new Pen(Color.FromArgb(255,226,200)))g.DrawEllipse(p,14,15,29,29);using(var b=new SolidBrush(Aero.Plum))g.FillEllipse(b,26,27,5,5);using(var f=Aero.UI(11,FontStyle.Bold))Aero.Text(g,String.IsNullOrEmpty(value)?"Choose your game":Path.GetFileName(value),f,Aero.Ink,new RectangleF(55,7,logicalWidth-68,25));using(var f=Aero.UI(9.5f))Aero.Text(g,String.IsNullOrEmpty(value)?"default.xex from your extracted copy":Path.GetFileName(Path.GetDirectoryName(value)),f,Aero.Muted,new RectangleF(55,31,logicalWidth-68,22));}
  protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
 }
 public sealed class PlayerInstrumentCard : Control {
  static readonly System.Collections.Generic.Dictionary<string,Bitmap> artwork=new System.Collections.Generic.Dictionary<string,Bitmap>();
  static readonly object artworkLock=new object();
  string instrument="guitar",player="Player 1",device="Disconnected";public float UiScale=1;
  public string Instrument{get{return instrument;}set{instrument=value??"guitar";Invalidate();}}
  public string Player{get{return player;}set{player=value??"";Invalidate();}}
  public string Device{get{return device;}set{device=value??"Disconnected";Invalidate();}}
  public PlayerInstrumentCard(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  public static string DeviceCaption(string id,System.Collections.Generic.IEnumerable<InputDevice> devices){
   if(id=="keyboard")return "Keyboard";
   if(String.IsNullOrEmpty(id))return "Disconnected";
   var match=devices==null?null:devices.FirstOrDefault(d=>String.Equals(d.Id,id,StringComparison.OrdinalIgnoreCase));
   return match==null||String.IsNullOrWhiteSpace(match.Name)?"Disconnected":match.Name;
  }
  static Bitmap Artwork(string name){
   lock(artworkLock){Bitmap image;if(artwork.TryGetValue(name,out image))return image;
    using(var stream=typeof(PlayerInstrumentCard).Assembly.GetManifestResourceStream("BeatlesLauncher.Instrument."+name+".png")){if(stream==null)throw new InvalidOperationException("Missing instrument artwork resource: "+name);using(var source=new Bitmap(stream))image=new Bitmap(source);}
    artwork.Add(name,image);return image;
   }
  }
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){
   Aero.PaintBackground(this,e.Graphics);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float s=Math.Max(.75f,UiScale);
   float size=Math.Max(1,Math.Min(S(62),Math.Min(Width-S(12),Height*.48f))),x=(Width-size)/2f,y=S(3);
   var image=Artwork(instrument);if(image!=null)g.DrawImage(image,new RectangleF(x,y,size,size));
   y+=size+S(2);using(var nameFont=Aero.UI(9*s,FontStyle.Bold))Aero.Text(g,InstrumentName(instrument),nameFont,Aero.Ink,new RectangleF(S(3),y,Width-S(6),S(19)),StringAlignment.Center);
   y+=S(18);using(var playerFont=Aero.UI(8.5f*s,FontStyle.Bold))Aero.Text(g,player,playerFont,Aero.Gold,new RectangleF(S(3),y,Width-S(6),S(18)),StringAlignment.Center);
   y+=S(17);using(var deviceFont=Aero.UI(8*s))Aero.Text(g,device,deviceFont,device=="Disconnected"?Aero.Muted:Aero.Ink,new RectangleF(S(3),y,Width-S(6),Height-y-S(2)),StringAlignment.Center);
  }
  int S(float value){return (int)Math.Round(value*UiScale);}
  static string InstrumentName(string value){return value=="drums"?"Drums":value=="bass"?"Bass":value=="vocals"?"Vocals":"Guitar";}
 }
 public sealed class SleeveArt : Control {
  Bitmap cover;
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
  public SleeveArt(){SetStyle(ControlStyles.Opaque|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);var window=FindForm() as AeroWindow;if(e.Button==MouseButtons.Left&&window!=null)window.DragWindow();}
  static void Bubble(Graphics g,float x,float y,float r){using(var b=new LinearGradientBrush(new RectangleF(x-r,y-r,r*2,r*2),Color.FromArgb(65,255,255,255),Color.FromArgb(8,235,255,230),65f))g.FillEllipse(b,x-r,y-r,r*2,r*2);using(var p=new Pen(Color.FromArgb(125,246,255,240),1.1f))g.DrawEllipse(p,x-r,y-r,r*2,r*2);using(var p=new Pen(Color.FromArgb(190,255,255,249),1.7f))g.DrawArc(p,x-r+4,y-r+4,r*2-8,r*2-8,205,90);}
  static void Wave(Graphics g,float y,Color color,float bend){using(var p=new GraphicsPath()){p.AddBezier(-30,y,95,y-bend,210,y+bend,410,y-12);p.AddLine(410,y-12,410,680);p.AddLine(410,680,-30,680);p.CloseFigure();using(var b=new SolidBrush(color))g.FillPath(b,p);}}
  static void Leaf(Graphics g,float x,float y,float height,float lean,Color color){using(var p=new GraphicsPath()){p.AddBezier(x,y,x-25,y-height*.5f,x+lean-25,y-height,x+lean,y-height);p.AddBezier(x+lean,y-height,x+lean+18,y-height*.4f,x+7,y-height*.2f,x,y);using(var b=new SolidBrush(color))g.FillPath(b,p);}}
  static void Submarine(Graphics g){
   using(var b=new SolidBrush(Color.FromArgb(28,0,73,79)))g.FillEllipse(b,66,449,244,16);
   using(var tube=new GraphicsPath()){tube.AddLine(183,370,183,332);tube.AddBezier(183,332,183,313,191,309,209,309);tube.AddLine(209,309,220,309);using(var p=new Pen(Color.FromArgb(162,123,44),18){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawPath(p,tube);using(var p=new Pen(Color.FromArgb(247,205,69),14){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawPath(p,tube);using(var p=new Pen(Color.FromArgb(255,241,170),3){LineJoin=LineJoin.Round})g.DrawPath(p,tube);}
   using(var p=Aero.Round(new RectangleF(33,390,58,29),8)){using(var b=new LinearGradientBrush(new RectangleF(33,390,58,29),Color.FromArgb(255,231,116),Color.FromArgb(222,164,43),90f))g.FillPath(b,p);using(var pen=new Pen(Color.FromArgb(151,117,52),1.4f))g.DrawPath(pen,p);}
   using(var b=new SolidBrush(Color.FromArgb(234,182,49)))g.FillPolygon(b,new[]{new PointF(102,432),new PointF(85,451),new PointF(127,441)});
   using(var p=Aero.Round(new RectangleF(213,348,58,29),13))using(var b=new LinearGradientBrush(new RectangleF(213,348,58,29),Color.FromArgb(255,237,139),Color.FromArgb(227,174,49),90f))g.FillPath(b,p);
   var body=new RectangleF(65,363,252,81);
   using(var p=Aero.Round(body,39)){using(var b=new LinearGradientBrush(body,Color.FromArgb(255,238,143),Color.FromArgb(233,173,31),90f))g.FillPath(b,p);using(var pen=new Pen(Color.FromArgb(160,121,45),1.7f))g.DrawPath(pen,p);var s=g.Save();g.SetClip(p);using(var b=new SolidBrush(Color.FromArgb(90,255,255,220)))g.FillEllipse(b,49,345,285,53);g.Restore(s);}
   using(var p=new Pen(Color.FromArgb(130,255,251,188),2))g.DrawArc(p,73,368,236,68,185,155);
   for(int i=0;i<4;i++){float x=105+i*49;using(var b=new LinearGradientBrush(new RectangleF(x-15,389,30,30),Color.FromArgb(255,251,186),Color.FromArgb(180,132,42),90f))g.FillEllipse(b,x-15,389,30,30);using(var b=new LinearGradientBrush(new RectangleF(x-11,393,22,22),Color.FromArgb(99,206,211),Color.FromArgb(29,113,136),90f))g.FillEllipse(b,x-11,393,22,22);using(var p=new Pen(Color.FromArgb(141,105,36),1.2f))g.DrawEllipse(p,x-15,389,30,30);using(var p=new Pen(Color.FromArgb(220,235,255,238),1.6f))g.DrawArc(p,x-8,396,16,16,200,105);}
   using(var p=new Pen(Color.FromArgb(163,137,56),1.2f))g.DrawLine(p,106,433,264,433);
   using(var b=new SolidBrush(Color.FromArgb(246,230,136)))g.FillEllipse(b,27,382,11,25);
  }
  public static void SaveIcon(string path){using(var bitmap=new Bitmap(256,256)){using(var g=Graphics.FromImage(bitmap)){g.SmoothingMode=SmoothingMode.AntiAlias;using(var circle=new GraphicsPath()){circle.AddEllipse(1,1,254,254);g.SetClip(circle);using(var b=new LinearGradientBrush(new Rectangle(0,0,256,256),Color.FromArgb(219,247,219),Color.FromArgb(35,160,175),90f))g.FillRectangle(b,0,0,256,256);Bubble(g,189,62,40);using(var wave=new SolidBrush(Color.FromArgb(50,0,139,151)))g.FillEllipse(wave,-100,166,480,220);var old=g.Save();using(var matrix=new Matrix(.78f,0,0,.78f,-4,-174))g.Transform=matrix;Submarine(g);g.Restore(old);g.ResetClip();using(var pen=new Pen(Color.FromArgb(190,255,255,239),2))g.DrawEllipse(pen,2,2,251,251);}}var handle=bitmap.GetHicon();try{using(var icon=(Icon)Icon.FromHandle(handle).Clone())using(var stream=File.Create(path))icon.Save(stream);}finally{DestroyIcon(handle);}}}
  Bitmap photo;bool photoTried;
  Bitmap Photo(){if(!photoTried){photoTried=true;try{using(var s=typeof(SleeveArt).Assembly.GetManifestResourceStream("BeatlesLauncher.Cover.jpg"))if(s!=null)photo=new Bitmap(s);}catch{}}return photo;}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);var art=Photo();if(art!=null){var g=e.Graphics;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;float scale=Math.Max((float)Width/art.Width,(float)Height/art.Height);float sw=Width/scale,sh=Height/scale;var src=new RectangleF((art.Width-sw)/2,(art.Height-sh)/2,sw,sh);using(var clip=Aero.Round(new RectangleF(0,0,Width-1,Height-1),17)){g.SetClip(clip);g.DrawImage(art,new RectangleF(0,0,Width,Height),src,GraphicsUnit.Pixel);g.ResetClip();using(var pen=new Pen(Color.FromArgb(220,236,106,120),1.6f))g.DrawPath(pen,clip);}return;}if(cover==null){cover=new Bitmap(760,1256);cover.SetResolution(96,96);using(var g=Graphics.FromImage(cover)){g.ScaleTransform(2,2);PaintCover(g);}}e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(cover,ClientRectangle);}
  protected override void Dispose(bool disposing){if(disposing){if(cover!=null)cover.Dispose();if(photo!=null)photo.Dispose();}base.Dispose(disposing);}
  void PaintCover(Graphics g){int Width=380,Height=628;var ClientRectangle=new Rectangle(0,0,Width,Height);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
   using(var clip=Aero.Round(new RectangleF(0,0,Width-1,Height-1),17)){g.SetClip(clip);using(var b=new LinearGradientBrush(ClientRectangle,Color.White,Color.Teal,90f)){b.InterpolationColors=new ColorBlend {Colors=new[]{Color.FromArgb(222,247,224),Color.FromArgb(123,211,221),Color.FromArgb(17,118,133)},Positions=new[]{0f,.47f,1f}};g.FillRectangle(b,ClientRectangle);}
    using(var p=new GraphicsPath()){p.AddEllipse(83,92,420,420);using(var b=new PathGradientBrush(p){CenterColor=Color.FromArgb(125,255,255,234),SurroundColors=new[]{Color.FromArgb(0,255,255,230)}})g.FillPath(b,p);}
    using(var b=new SolidBrush(Color.FromArgb(25,255,255,245))){g.FillPolygon(b,new[]{new PointF(204,-20),new PointF(247,-20),new PointF(339,558),new PointF(296,551)});g.FillPolygon(b,new[]{new PointF(278,-20),new PointF(291,-20),new PointF(362,409),new PointF(344,402)});}
    Wave(g,340,Color.FromArgb(45,62,175,188),45);Wave(g,377,Color.FromArgb(60,28,151,177),35);Wave(g,480,Color.FromArgb(90,12,132,147),45);Wave(g,521,Color.FromArgb(110,27,147,144),35);Wave(g,579,Color.FromArgb(85,115,187,116),33);
    Bubble(g,306,302,46);Bubble(g,45,472,24);Bubble(g,323,497,15);Bubble(g,92,286,10);Bubble(g,340,232,8);Bubble(g,20,391,8);Bubble(g,354,559,24);
    Leaf(g,20,650,148,30,Color.FromArgb(125,32,128,107));Leaf(g,12,650,101,84,Color.FromArgb(150,107,190,130));Leaf(g,345,650,118,-33,Color.FromArgb(150,103,188,124));Leaf(g,370,650,175,-12,Color.FromArgb(100,18,120,109));Leaf(g,382,650,101,-83,Color.FromArgb(115,164,211,137));
    Submarine(g);
    using(var f=new Font("Georgia",25,FontStyle.Italic))Aero.Text(g,"the",f,Aero.Ink,new RectangleF(0,68,Width,45),StringAlignment.Center);
    using(var f=Aero.Display(58)){using(var p=new GraphicsPath()){p.AddString("BEATLES",f.FontFamily,(int)FontStyle.Regular,80,new PointF(0,0),StringFormat.GenericTypographic);var b=p.GetBounds();using(var m=new Matrix()){m.Translate(28,120);m.Scale((Width-56)/b.Width,69/b.Height);m.Translate(-b.X,-b.Y);p.Transform(m);}using(var shadow=new SolidBrush(Color.FromArgb(100,255,255,239))){var s=g.Save();g.TranslateTransform(0,2);g.FillPath(shadow,p);g.Restore(s);}using(var ink=new SolidBrush(Aero.Ink))g.FillPath(ink,p);}}
    using(var f=Aero.UI(12,FontStyle.Bold)){string title="R O C K   B A N D";Aero.Text(g,title,f,Aero.Ink,new RectangleF(0,208,Width,30),StringAlignment.Center);}
    using(var f=Aero.UI(9))Aero.Text(g,"F O R   P C",f,Color.FromArgb(54,109,109),new RectangleF(0,243,Width,25),StringAlignment.Center);
    using(var f=Aero.UI(8.3f))Aero.Text(g,"THE BEATLES : ROCK BAND",f,Color.FromArgb(210,240,251,223),new RectangleF(0,Height-47,Width,25),StringAlignment.Center);
   }
   g.ResetClip();using(var line=new Pen(Color.FromArgb(205,255,255,242),1))using(var path=Aero.Round(new RectangleF(.5f,.5f,Width-2,Height-2),17))g.DrawPath(line,path);
  }
 }
}
