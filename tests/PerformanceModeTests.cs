using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BeatlesLauncher;
class PerformanceModeTests {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static T Field<T>(LauncherForm form,string name){return (T)typeof(LauncherForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
 [STAThread] static int Main(){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Check(new Settings().PerformanceMode,"new users keep the transition fix enabled");
  Check(new Settings().ResolutionScale==1&&new Settings().AnisotropicOverride==3&&new Settings().AntiAliasing=="none"&&new Settings().FrameRateLimit==0&&new Settings().VSync,"new users get safe graphics defaults");
  Check(!new Settings().DolbyDigital,"new users keep Dolby Digital encoding disabled unless requested");
  Check(new Settings().MicLatency==5,"new users start with the stable microphone delay");
  using(var controls=new ControlsForm(new Settings(),true)){
   var latency=(ComboBox)typeof(ControlsForm).GetField("micLatency",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controls);
   Check(latency.SelectedIndex==0&&((InputDevice)latency.Items[0]).Id=="5"&&latency.Items.Count==5,"mic delay selector starts at the stable 5 ms minimum");
  }
  File.WriteAllText(GameFiles.SettingsPath,"{\"MicLatency\":1}");
  Check(GameFiles.LoadSettings().MicLatency==5,"experimental 1 ms profiles migrate to the stable minimum");
  File.WriteAllText(GameFiles.SettingsPath,"{\"XexPath\":\"C:/Legal game/default.xex\",\"Volume\":73,\"AudioChannels\":6,\"Keys\":[{\"Key\":\"whammy\",\"Value\":\"Numpad0\"}]}");
  var settings=GameFiles.LoadSettings();Check(settings.PerformanceMode&&settings.Volume==73&&settings.Keys["whammy"]=="Numpad0"&&settings.ResolutionScale==1&&settings.VSync,"older profiles keep working with graphics defaults");
  foreach(bool enabled in new[]{false,true}){
   settings.PerformanceMode=enabled;GameFiles.WriteJson(GameFiles.SettingsPath,settings);settings=GameFiles.LoadSettings();
   Check(settings.PerformanceMode==enabled,"performance mode survives save/reload: "+enabled);
   settings.ResolutionScale=2;settings.AnisotropicOverride=5;settings.AntiAliasing="fxaa";settings.FrameRateLimit=144;
   string args=GameFiles.Arguments(settings,"C:/Legal game","test.log");
   Check(args.Contains("--d3d12_nonblocking_pipeline_creation="+enabled.ToString().ToLowerInvariant()),"explicit runtime override: "+enabled);
   Check(args.Contains("--resolution_scale=2")&&args.Contains("--anisotropic_override=5")&&args.Contains("--swap_post_effect=fxaa")&&args.Contains("--video_mode_refresh_rate=144")&&args.Contains("--vsync=true"),"graphics settings reach the runtime arguments");
   settings.DolbyDigital=enabled;
   args=GameFiles.Arguments(settings,"C:/Legal game","test.log");
   Check(args.Contains("--audio_dolby_digital="+enabled.ToString().ToLowerInvariant()),"Dolby Digital mode reaches the native runtime: "+enabled);
   Check(args.Contains("--audio_maxqframes="+(enabled?16:8)),"Dolby buffering is raised only for experimental AC-3: "+enabled);
   Check(args.Contains("--game_data_root=C:/Legal game")&&args.Contains("--controller_device=keyboard")&&args.Contains("--windows_audio"),"performance mode preserves root, device and audio arguments");
  }
  foreach(int fps in new[]{30,60,75,120,144}){
   var fpsSettings=new Settings{FrameRateLimit=fps};string fpsArgs=GameFiles.Arguments(fpsSettings,"C:/Legal game","test.log");
   Check(fpsArgs.Contains("--video_mode_refresh_rate="+fps)&&fpsArgs.Contains("--vsync=true"),"FPS cap is forwarded to the engine: "+fps);
  }
  var vsyncSettings=new Settings{FrameRateLimit=0};
  Check(!GameFiles.Arguments(vsyncSettings,"C:/Legal game","test.log").Contains("--video_mode_refresh_rate=")&&GameFiles.Arguments(vsyncSettings,"C:/Legal game","test.log").Contains("--vsync=true"),"VSync mode leaves the monitor refresh rate in control");
  var microphoneSettings=new Settings {Instrument="vocals",MicLatency=1};
  string microphoneArgs=GameFiles.Arguments(microphoneSettings,"C:/Legal game","test.log");
  Check(microphoneArgs.Contains("--virtual_microphone_latency_ms=5")&&!microphoneArgs.Contains("--virtual_microphone_wasapi="),"vocal input uses stable capture and clamps old 1 ms values to 5 ms");
  microphoneSettings.Instrument="guitar";
  Check(!GameFiles.Arguments(microphoneSettings,"C:/Legal game","test.log").Contains("--virtual_microphone_wasapi="),"non-vocal input does not enable microphone capture");
  using(var form=new LauncherForm(null,false)){
   Check(Field<CheckBox>(form,"performanceMode").Checked,"launcher restores enabled profile");
   Field<FileDisplay>(form,"path").Value="C:/Latest game/default.xex";
   Field<ComboBox>(form,"resolution").SelectedIndex=3;
   Field<CheckBox>(form,"fullscreen").Checked=true;
   Field<TextBox>(form,"userBox").Text="Latest Player";
   Field<CheckBox>(form,"performanceMode").Checked=false;
   Check(!GameFiles.LoadSettings().PerformanceMode,"unchecking is saved immediately without starting the game");
   Field<CheckBox>(form,"performanceMode").Checked=true;
   Check(GameFiles.LoadSettings().PerformanceMode,"checking is saved immediately without starting the game");
   Field<CheckBox>(form,"dolbyDigital").Checked=true;
   Check(GameFiles.LoadSettings().DolbyDigital,"Dolby Digital mode is saved immediately");
   Field<GlassSlider>(form,"renderScale").Value=100;
   Check(GameFiles.LoadSettings().ResolutionScale==3,"internal resolution slider saves its snapped value");
   Field<GlassSlider>(form,"anisotropic").Value=100;
   Check(GameFiles.LoadSettings().AnisotropicOverride==5,"anisotropic slider saves 16x filtering");
   var aa=(ComboBox)Field<Control>(form,"antiAliasing");
   Check(aa.Items.Count==2&&aa.Items[0].ToString()=="Off"&&aa.Items[1].ToString()=="FXAA","anti-aliasing offers only Off and FXAA");
   aa.SelectedIndex=1;
   Check(GameFiles.LoadSettings().AntiAliasing=="fxaa","FXAA selector saves its runtime mode");
   var fpsSelector=Field<ComboBox>(form,"frameRate");
   Check(fpsSelector.Items.Count==6&&fpsSelector.Items[0].ToString()=="VSync","frame-rate selector includes VSync mode");
   foreach(int fps in new[]{30,60,75,120,144}){
    fpsSelector.SelectedItem=fps+" FPS";
    Check(GameFiles.LoadSettings().FrameRateLimit==fps,"frame-rate selection saves "+fps+" FPS");
   }
   fpsSelector.SelectedIndex=0;
   Check(GameFiles.LoadSettings().FrameRateLimit==0,"VSync selection saves monitor mode");
   Check(Field<Label>(form,"renderScaleValue").Text=="300%","internal resolution slider displays a percentage");
   typeof(LauncherForm).GetMethod("ApplyGraphicsPreset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{0});
   var lowPreset=GameFiles.LoadSettings();
   Check(lowPreset.Width==960&&lowPreset.Height==540&&lowPreset.ResolutionScale==1&&lowPreset.AnisotropicOverride==0&&lowPreset.VSync&&lowPreset.FrameRateLimit==0,"Low preset applies performance-focused settings without changing frame rate");
   typeof(LauncherForm).GetMethod("ApplyGraphicsPreset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{1});
   var balancedPreset=GameFiles.LoadSettings();
   Check(balancedPreset.Width==1280&&balancedPreset.ResolutionScale==1&&balancedPreset.AnisotropicOverride==3&&balancedPreset.VSync,"Medium preset applies and saves defaults");
   typeof(LauncherForm).GetMethod("ApplyGraphicsPreset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{2});
   var highPreset=GameFiles.LoadSettings();
   Check(highPreset.Width==1920&&highPreset.ResolutionScale==2&&highPreset.AnisotropicOverride==5&&highPreset.AntiAliasing=="fxaa"&&highPreset.VSync,"High preset applies and saves image-quality settings");
   typeof(LauncherForm).GetMethod("PersistUiSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{"Test"});
   var directPlaySettings=GameFiles.LoadSettings();string directPlayArgs=GameFiles.Arguments(directPlaySettings,"C:/Latest game","test.log");
   Check(directPlaySettings.XexPath=="C:/Latest game/default.xex"&&directPlayArgs.Contains("--window_width=1920")&&directPlayArgs.Contains("--window_height=1080")&&directPlayArgs.Contains("--fullscreen=true"),"direct play reads latest saved game and display settings");
   Check(!directPlayArgs.Contains("--audio_channels=")&&directPlayArgs.Contains("\"--user_display_name=Latest Player\"")&&directPlayArgs.Contains("--d3d12_nonblocking_pipeline_creation=true"),"direct play leaves audio layout to Windows and applies latest name and performance settings");
  }
  using(var form=new LauncherForm(null,true)){
   foreach(var scale in new[]{1f,1.25f,1.5f,2f})foreach(var size in new[]{new Size(640,480),new Size(1000,740),new Size(1920,1080)}){
    form.PreviewLayout(size,scale);form.Show();Application.DoEvents();form.RefreshLayout();
    var toggle=Field<CheckBox>(form,"performanceMode");var hint=Field<Label>(form,"performanceHint");var graphics=Field<Label>(form,"graphicsTitle");var renderSlider=Field<GlassSlider>(form,"renderScale");
    Check(toggle.Width>0&&toggle.Height>=26*scale&&!toggle.Bounds.IntersectsWith(hint.Bounds),"performance row has space at "+size+" scale "+scale);
    Check(graphics.Bottom<=toggle.Top&&hint.Bottom<=renderSlider.Top&&renderSlider.Bottom<=((ScrollableControl)renderSlider.Parent).AutoScrollMinSize.Height,"graphics section has distinct visible rows at "+size+" scale "+scale);
    var pane=(ScrollableControl)toggle.Parent;pane.ScrollControlIntoView(toggle);Application.DoEvents();
    Check(toggle.Top>=0&&toggle.Bottom<=pane.ClientSize.Height,"performance mode is reachable by scrolling at "+size+" scale "+scale);
    pane.ScrollControlIntoView(renderSlider);Application.DoEvents();
    Check(renderSlider.Top>=0&&renderSlider.Bottom<=pane.ClientSize.Height,"graphics slider is reachable by scrolling at "+size+" scale "+scale);
    pane.AutoScrollPosition=Point.Empty;form.Hide();
   }
  }
  Console.WriteLine(checks+" performance mode checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
