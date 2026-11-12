using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BeatlesLauncher {
 public enum Glyph {Star,Gear,Monitor,Speaker,Gamepad,Folder}
 // Line icons drawn with GDI+ so the launcher needs no extra image assets.
 static class Glyphs {
  public static void Draw(Graphics g,Glyph glyph,RectangleF r,Color color,float width) {
   var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;
   float x=r.X,y=r.Y,w=r.Width,h=r.Height;
   using(var pen=new Pen(color,width){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round})using(var brush=new SolidBrush(color)) {
    switch(glyph) {
     case Glyph.Star:Aero.Star(g,brush,x+w*.5f,y+h*.54f,w*.5f,w*.22f);break;
     case Glyph.Gear:
      g.DrawEllipse(pen,x+w*.26f,y+h*.26f,w*.48f,h*.48f);g.DrawEllipse(pen,x+w*.41f,y+h*.41f,w*.18f,h*.18f);
      for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,x+w*.5f+(float)Math.Cos(a)*w*.32f,y+h*.5f+(float)Math.Sin(a)*h*.32f,x+w*.5f+(float)Math.Cos(a)*w*.46f,y+h*.5f+(float)Math.Sin(a)*h*.46f);}
      break;
     case Glyph.Monitor:
      using(var p=Aero.Round(new RectangleF(x+w*.06f,y+h*.12f,w*.88f,h*.58f),w*.07f))g.DrawPath(pen,p);
      g.DrawLine(pen,x+w*.5f,y+h*.7f,x+w*.5f,y+h*.86f);g.DrawLine(pen,x+w*.3f,y+h*.88f,x+w*.7f,y+h*.88f);
      break;
     case Glyph.Speaker:
      g.DrawPolygon(pen,new[]{new PointF(x+w*.1f,y+h*.4f),new PointF(x+w*.3f,y+h*.4f),new PointF(x+w*.52f,y+h*.2f),new PointF(x+w*.52f,y+h*.8f),new PointF(x+w*.3f,y+h*.6f),new PointF(x+w*.1f,y+h*.6f)});
      g.DrawArc(pen,x+w*.46f,y+h*.32f,w*.3f,h*.36f,-55,110);g.DrawArc(pen,x+w*.4f,y+h*.2f,w*.5f,h*.6f,-55,110);
      break;
     case Glyph.Gamepad:
      using(var p=Aero.Round(new RectangleF(x+w*.04f,y+h*.26f,w*.92f,h*.5f),w*.2f))g.DrawPath(pen,p);
      g.DrawLine(pen,x+w*.27f,y+h*.38f,x+w*.27f,y+h*.64f);g.DrawLine(pen,x+w*.14f,y+h*.51f,x+w*.4f,y+h*.51f);
      g.FillEllipse(brush,x+w*.62f,y+h*.39f,w*.1f,h*.1f);g.FillEllipse(brush,x+w*.76f,y+h*.52f,w*.1f,h*.1f);
      break;
     case Glyph.Folder:
      g.DrawPolygon(pen,new[]{new PointF(x+w*.08f,y+h*.22f),new PointF(x+w*.38f,y+h*.22f),new PointF(x+w*.48f,y+h*.34f),new PointF(x+w*.92f,y+h*.34f),new PointF(x+w*.92f,y+h*.8f),new PointF(x+w*.08f,y+h*.8f)});
      break;
    }
   }
   g.Restore(state);
  }
 }
 // Opaque flat sidebar surface that still shows the retro scene through its translucency.
 public sealed class FlatPanel : Panel, ISurface {
  public int Alpha=226;
  public FlatPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  public void DrawSurface(Graphics g){
   if(Width<2||Height<2)return;
   using(var b=new SolidBrush(Color.FromArgb(Alpha,26,11,36)))g.FillRectangle(b,0,0,Width,Height);
   using(var p=new Pen(Color.FromArgb(150,236,106,120)))g.DrawLine(p,Width-1,0,Width-1,Height);
  }
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);DrawSurface(e.Graphics);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);var window=FindForm() as AeroWindow;if(e.Button==MouseButtons.Left&&e.Y<53&&window!=null)window.DragWindow();}
 }
 public sealed class Divider : Control {
  public Divider(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;TabStop=false;}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);using(var p=new Pen(Color.FromArgb(110,236,106,120)))e.Graphics.DrawLine(p,0,0,Width,0);}
 }
 // Icon button for the narrow left rail (Home / Settings).
 public sealed class RailButton : Button {
  public Glyph GlyphKind;public bool Selected;public float UiScale=1;bool hover;
  public RailButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;Aero.PaintBackground(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;float s=Math.Max(.75f,UiScale);
   float box=Math.Min(Width-6*s,Height-4*s);var tile=new RectangleF((Width-box)/2f,(Height-box)/2f,box,box);
   if(Selected||hover){
    using(var p=Aero.Round(tile,10*s))using(var b=new SolidBrush(Selected?Color.FromArgb(78,236,106,120):Color.FromArgb(40,255,243,214)))g.FillPath(b,p);
    if(Selected)using(var b=new SolidBrush(Aero.Coral))g.FillRectangle(b,0,tile.Y+3*s,3*s,tile.Height-6*s);
   }
   float icon=box*.5f;Glyphs.Draw(g,GlyphKind,new RectangleF(tile.X+(box-icon)/2f,tile.Y+(box-icon)/2f,icon,icon),Selected||hover?Aero.Ink:Aero.Muted,1.7f*s);
   if(Focused&&ShowFocusCues)using(var p=new Pen(Color.FromArgb(200,Aero.Gold))){p.DashStyle=DashStyle.Dot;using(var path=Aero.Round(new RectangleF(tile.X+2,tile.Y+2,tile.Width-4,tile.Height-4),8*s))g.DrawPath(p,path);}
  }
 }
 // Menu row: icon, title (optionally a subtitle), current value and a chevron.
 public sealed class NavItem : Button {
  public Glyph GlyphKind;public string Subtitle,Value;public bool Selected;public float UiScale=1;bool hover,pressed;
  public NavItem(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;ForeColor=Aero.Ink;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;Aero.PaintBackground(this,g);if(Width<8||Height<8)return;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;float s=Math.Max(.75f,UiScale);
   var r=new RectangleF(1,1,Width-3,Height-3);
   Color top=Selected?Color.FromArgb(238,110,60,124):Color.FromArgb(214,92,52,108),bottom=Selected?Color.FromArgb(238,74,36,90):Color.FromArgb(214,58,30,74);
   if(hover){top=Aero.Mix(top,Color.White,.1f);bottom=Aero.Mix(bottom,Color.White,.08f);}if(pressed){top=Aero.Mix(top,Color.Black,.12f);bottom=Aero.Mix(bottom,Color.Black,.12f);}
   using(var p=Aero.Round(r,10*s)){
    using(var b=new LinearGradientBrush(r,top,bottom,90f))g.FillPath(b,p);
    using(var pen=new Pen(Selected?Aero.Coral:hover?Color.FromArgb(230,255,226,150):Color.FromArgb(150,236,106,120),Selected?2f:1.2f))g.DrawPath(pen,p);
   }
   float x=18*s,icon=22*s;Glyphs.Draw(g,GlyphKind,new RectangleF(x,(Height-icon)/2f,icon,icon),Selected||hover?Aero.Gold:Aero.Ink,1.5f*s);x+=icon+16*s;
   float chev=Width-24*s,cy=Height/2f,right=chev-14*s;
   using(var p=new Pen(Aero.Muted,1.6f*s){StartCap=LineCap.Round,EndCap=LineCap.Round}){g.DrawLine(p,chev,cy-5*s,chev+5*s,cy);g.DrawLine(p,chev+5*s,cy,chev,cy+5*s);}
   float valueWidth=0;
   if(!String.IsNullOrEmpty(Value))using(var f=Aero.UI(9*s)){valueWidth=Math.Min(Math.Max(0,right-x)*.45f,g.MeasureString(Value,f).Width+8*s);Aero.Text(g,Value,f,Aero.Muted,new RectangleF(right-valueWidth,0,valueWidth,Height),StringAlignment.Far);}
   float textWidth=Math.Max(1,right-valueWidth-x-8*s);
   using(var f=Aero.Display(13*s)){
    if(String.IsNullOrEmpty(Subtitle))Aero.Text(g,Text,f,Aero.Ink,new RectangleF(x,0,textWidth,Height-2));
    else{Aero.Text(g,Text,f,Aero.Ink,new RectangleF(x,Height*.08f,textWidth,Height*.5f));using(var sub=Aero.UI(8.5f*s))Aero.Text(g,Subtitle,sub,Aero.Muted,new RectangleF(x,Height*.52f,textWidth,Height*.38f));}
   }
   if(Focused&&ShowFocusCues)using(var p=new Pen(Color.FromArgb(200,Aero.Gold))){p.DashStyle=DashStyle.Dot;using(var path=Aero.Round(new RectangleF(4,4,Width-10,Height-10),8*s))g.DrawPath(p,path);}
  }
 }
}
