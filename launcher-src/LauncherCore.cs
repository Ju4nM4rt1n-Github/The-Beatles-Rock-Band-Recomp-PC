using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace BeatlesLauncher {
 [DataContract] public class Settings {
  [DataMember] public string XexPath = "";
  [DataMember] public int Width = 1280;
  [DataMember] public int Height = 720;
  [DataMember] public bool Fullscreen = false;
  [DataMember] public bool PerformanceMode = true;
  [DataMember] public int ResolutionScale = 1;
  [DataMember] public int AnisotropicOverride = 3;
  [DataMember] public string AntiAliasing = "none";
  [DataMember] public bool VSync = true;
  [DataMember] public int FrameRateLimit = 0;
  [DataMember] public bool DolbyDigital = false;
  [DataMember] public int Volume = 100;
  [DataMember] public Dictionary<string,string> Keys = Controls.Defaults();
  [DataMember] public string InputDevice = "keyboard";
  [DataMember] public string Instrument = "guitar";
  [DataMember] public string Microphone = "default";
  [DataMember] public string UserName = "User";
  [DataMember] public int MicGain = 100;
  [DataMember] public int MicLatency = 5;
  [DataMember] public int AudioBuffer = 8;
    [DataMember] public string Player2Device = "";
    [DataMember] public string Player2Instrument = "bass";
    [DataMember] public string Player3Device = "";
    [DataMember] public string Player3Instrument = "drums";
    [DataMember] public string Player4Device = "";
    [DataMember] public string Player4Instrument = "guitar";
  [DataMember] public Dictionary<string,string> DrumKeys = Controls.Defaults("drums");
  [DataMember] public Dictionary<string,Dictionary<string,string>> DrumDeviceBindings = new Dictionary<string,Dictionary<string,string>>();
  [DataMember] public Dictionary<string,Dictionary<string,string>> DeviceBindings = new Dictionary<string,Dictionary<string,string>>();
    [OnDeserializing] void DefaultsBeforeRead(StreamingContext context){Width=1280;Height=720;Volume=100;PerformanceMode=true;ResolutionScale=1;AnisotropicOverride=3;AntiAliasing="none";VSync=true;FrameRateLimit=-1;DolbyDigital=false;InputDevice="keyboard";Instrument="guitar";Player2Device="";Player2Instrument="bass";Player3Device="";Player3Instrument="drums";Player4Device="";Player4Instrument="guitar";Microphone="default";UserName="User";MicGain=100;MicLatency=5;AudioBuffer=8;}
 }
 [DataContract] public class GameFile {
  [DataMember] public string Path;
  [DataMember] public long Size;
 }
 [DataContract] public class GameManifest {
  [DataMember] public string XexSha256;
  [DataMember] public List<GameFile> Files;
 }
 public class CheckResult {
  public bool Ready;
  public string Message;
  public string Root;
  public string Hash;
 }
 public static class GameFiles {
  public static readonly string AppRoot = AppDomain.CurrentDomain.BaseDirectory;
  public static readonly string SettingsPath = Path.Combine(AppRoot, "launcher-settings.json");
  public static T ReadJson<T>(string path) {
   using (var input = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
  }
  public static void WriteJson<T>(string path, T value) {
   string temp = path + ".tmp";
   using (var output = File.Create(temp)) new DataContractJsonSerializer(typeof(T)).WriteObject(output, value);
   if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
  }
    static string NormalizeInstrument(string value,string fallback){return value=="guitar"||value=="bass"||value=="drums"||value=="vocals"?value:fallback;}
    // The virtual microphone is a single device and only belongs to player 1.
    static bool MicrophoneActive(Settings settings){return settings.Microphone!="off"&&NormalizeInstrument(settings.Instrument,"guitar")=="vocals";}
    public static string CleanUserName(string value){var text=new string((value??"").Where(ch=>ch>=' '&&ch<='~').ToArray()).Trim();if(text.Length>15)text=text.Substring(0,15).Trim();return text.Length==0?"User":text;}
    public static Settings LoadSettings() {
    try { var value = ReadJson<Settings>(SettingsPath); if(value != null) {value.Keys=Controls.Normalize(value.Keys);value.DrumKeys=Controls.Normalize(value.DrumKeys,"drums");value.Instrument=NormalizeInstrument(value.Instrument,"guitar");value.Player2Instrument=NormalizeInstrument(value.Player2Instrument,"bass");value.Player3Instrument=NormalizeInstrument(value.Player3Instrument,"drums");value.Player4Instrument=NormalizeInstrument(value.Player4Instrument,"guitar");if(value.Player2Instrument=="vocals")value.Player2Instrument="guitar";if(value.Player3Instrument=="vocals")value.Player3Instrument="guitar";if(value.Player4Instrument=="vocals")value.Player4Instrument="guitar";if(value.Player2Device==null||value.Player2Device=="keyboard")value.Player2Device="";if(value.Player3Device==null||value.Player3Device=="keyboard")value.Player3Device="";if(value.Player4Device==null||value.Player4Device=="keyboard")value.Player4Device="";if(value.DrumDeviceBindings==null)value.DrumDeviceBindings=new Dictionary<string,Dictionary<string,string>>();if(String.IsNullOrEmpty(value.InputDevice))value.InputDevice="keyboard";if(value.DeviceBindings==null)value.DeviceBindings=new Dictionary<string,Dictionary<string,string>>();if(String.IsNullOrEmpty(value.Microphone))value.Microphone="default";value.UserName=CleanUserName(value.UserName);value.MicGain=Math.Max(0,Math.Min(400,value.MicGain));value.MicLatency=Math.Max(5,Math.Min(100,value.MicLatency));value.AudioBuffer=Math.Max(4,Math.Min(16,value.AudioBuffer));value.ResolutionScale=Math.Max(1,Math.Min(3,value.ResolutionScale));value.AnisotropicOverride=Math.Max(0,Math.Min(5,value.AnisotropicOverride));if(value.AntiAliasing!="fxaa")value.AntiAliasing="none";if(value.FrameRateLimit<0)value.FrameRateLimit=value.VSync?0:60;if(value.FrameRateLimit!=30&&value.FrameRateLimit!=60&&value.FrameRateLimit!=75&&value.FrameRateLimit!=120&&value.FrameRateLimit!=144)value.FrameRateLimit=0;value.VSync=true;return value;} } catch { }
   return new Settings();
  }
  public static CheckResult Check(string xex) {
   try {
    if (String.IsNullOrWhiteSpace(xex) || !File.Exists(xex)) return Bad("Choose default.xex from your extracted copy of the game.");
    if (!String.Equals(Path.GetFileName(xex), "default.xex", StringComparison.OrdinalIgnoreCase)) return Bad("The executable must be named default.xex inside the game folder.");
    var manifest = ReadJson<GameManifest>(Path.Combine(AppRoot, "game-files.json"));
    if (manifest == null || manifest.Files == null || String.IsNullOrWhiteSpace(manifest.XexSha256)) return Bad("The launcher verification file is missing. Extract the complete package again.");
    string hash;
    using (var sha = SHA256.Create()) using (var file = File.OpenRead(xex)) hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
    if (!String.Equals(hash, manifest.XexSha256, StringComparison.OrdinalIgnoreCase)) return new CheckResult { Message="This XEX version is not supported by this port yet.", Hash=hash };
    string root = Path.GetDirectoryName(Path.GetFullPath(xex));
    var missing = new List<string>(); var incomplete = new List<string>();
    foreach (var item in manifest.Files) {
     var file = new FileInfo(Path.Combine(root, item.Path.Replace('/', Path.DirectorySeparatorChar)));
     if (!file.Exists) missing.Add(item.Path); else if (file.Length != item.Size) incomplete.Add(item.Path);
    }
    if (missing.Count != 0) return Bad("Missing disc files: " + String.Join(", ", missing.Take(3)) + (missing.Count > 3 ? " and others." : ".") + " Keep the XEX beside the complete gen folder.");
    if (incomplete.Count != 0) return Bad("Some files are incomplete or from another version: " + String.Join(", ", incomplete.Take(3)) + ". Check your disc extraction.");
    if (!File.Exists(RuntimeExe)) return Bad("The port executable is missing. Extract the complete package before playing.");
    return new CheckResult { Ready=true, Root=root, Hash=hash, Message="Game files found. Ready to play." };
   } catch (UnauthorizedAccessException) { return Bad("Could not read that folder. Choose a location you can access."); }
   catch (Exception error) { return Bad("Could not verify the game: " + error.Message); }
  }
  private static CheckResult Bad(string message) { return new CheckResult { Message=message }; }
  public static string RuntimeExe { get { return Path.Combine(AppRoot, "runtime", "beatlesrockband-guitar.exe"); } }
  // Windows command-line quoting, including embedded quotes and trailing slashes.
  public static string Quote(string argument) {
   var text = new StringBuilder("\""); int slashes = 0;
   foreach (char ch in argument) {
    if (ch == '\\') { slashes++; continue; }
    if (ch == '"') { text.Append('\\', slashes * 2 + 1); text.Append('"'); slashes=0; continue; }
    text.Append('\\', slashes); slashes=0; text.Append(ch);
   }
   text.Append('\\', slashes * 2); text.Append('"'); return text.ToString();
  }
  public static string Arguments(Settings settings, string root, string log) {
    string instrument=NormalizeInstrument(settings.Instrument,"guitar");
   int audioBuffer=settings.DolbyDigital?16:Math.Max(4,Math.Min(16,settings.AudioBuffer));
   var args = new List<string> {
    "--game_data_root=" + root.Replace('\\','/'),
    "--user_data_root=" + Path.Combine(AppRoot,"userdata").Replace('\\','/'),
    "--cache_root=" + Path.Combine(AppRoot,"cache").Replace('\\','/'),
    // The bundled SDK opens narrow log paths; keep this relative to runtime.
    "--log_file=../logs/" + Path.GetFileName(log),
    "--gpu_plugin=xenos", "--windows_audio", "--audio_render_pacing",
    "--mnk_mode", "--keyboard_guitar", "--input_instrument="+instrument,
    "--window_width=" + settings.Width, "--window_height=" + settings.Height,
    "--fullscreen=" + settings.Fullscreen.ToString().ToLowerInvariant(),
    "--audio_dolby_digital=" + settings.DolbyDigital.ToString().ToLowerInvariant(),
    "--d3d12_nonblocking_pipeline_creation=" + settings.PerformanceMode.ToString().ToLowerInvariant(),
    "--resolution_scale=" + Math.Max(1,Math.Min(3,settings.ResolutionScale)),
    "--anisotropic_override=" + Math.Max(0,Math.Min(5,settings.AnisotropicOverride)),
    "--swap_post_effect=" + (settings.AntiAliasing=="fxaa"?"fxaa":"none"),
    "--vsync=true",
    "--game_volume=" + Math.Max(0, Math.Min(100, settings.Volume)),
    "--audio_maxqframes=" + audioBuffer,
   };
   if(settings.FrameRateLimit==30||settings.FrameRateLimit==60||settings.FrameRateLimit==75||settings.FrameRateLimit==120||settings.FrameRateLimit==144)
    args.Add("--video_mode_refresh_rate="+settings.FrameRateLimit);
   args.AddRange(Controls.Arguments(instrument=="drums"?settings.DrumKeys:settings.Keys,instrument));
   if(!String.IsNullOrEmpty(settings.InputDevice)&&settings.InputDevice!="keyboard") {
    Dictionary<string,string> bindings;
    var profiles=instrument=="drums"?settings.DrumDeviceBindings:settings.DeviceBindings;
    if(profiles==null||!profiles.TryGetValue(settings.InputDevice,out bindings))bindings=ControllerDevices.Defaults(instrument);
    args.Add("--controller_device="+settings.InputDevice);
    args.Add("--controller_bindings="+ControllerDevices.Serialize(ControllerDevices.Normalize(bindings,instrument),instrument));
   } else args.Add("--controller_device=keyboard");
    args.Add("--isolate_input_devices=true");
    args.Add("--user_display_name="+CleanUserName(settings.UserName));
    if(!MicrophoneActive(settings))args.Add("--virtual_microphone=false");
    else {
     args.Add("--virtual_microphone=true");args.Add("--virtual_microphone_port=0");
     args.Add("--virtual_microphone_gain="+settings.MicGain);args.Add("--virtual_microphone_latency_ms="+Math.Max(5,Math.Min(100,settings.MicLatency)));
     if(settings.Microphone!="default")args.Add("--virtual_microphone_device="+settings.Microphone);
    }
    AddPlayerControllerArgs(args,1,settings.Player2Device,settings.Player2Instrument,settings);
    AddPlayerControllerArgs(args,2,settings.Player3Device,settings.Player3Instrument,settings);
    AddPlayerControllerArgs(args,3,settings.Player4Device,settings.Player4Instrument,settings);
   return String.Join(" ", args.Select(Quote));
  }
  static void AddPlayerControllerArgs(List<string> args,int player,string device,string instrument,Settings settings) {
    if(String.IsNullOrEmpty(device)||device=="keyboard")return;
    instrument=NormalizeInstrument(instrument,player==1?"bass":"drums");if(instrument=="vocals")instrument="guitar";
    var profiles=instrument=="drums"?settings.DrumDeviceBindings:settings.DeviceBindings;
    Dictionary<string,string> bindings;
    if(profiles==null||!profiles.TryGetValue(device,out bindings))bindings=ControllerDevices.Defaults(instrument);
    args.Add("--controller_device_"+player+"="+device);
    args.Add("--controller_instrument_"+player+"="+instrument);
    args.Add("--controller_bindings_"+player+"="+ControllerDevices.Serialize(ControllerDevices.Normalize(bindings,instrument),instrument));
  }
  public static Process Start(Settings settings, string root) {
   Directory.CreateDirectory(Path.Combine(AppRoot,"userdata"));
   Directory.CreateDirectory(Path.Combine(AppRoot,"cache"));
   Directory.CreateDirectory(Path.Combine(AppRoot,"logs"));
   string log = Path.Combine(AppRoot,"logs","game-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");
   WriteJson(SettingsPath, settings);
   var start = new ProcessStartInfo(RuntimeExe, Arguments(settings, root, log));
   start.WorkingDirectory = Path.GetDirectoryName(RuntimeExe); start.UseShellExecute=false;
   return Process.Start(start);
  }
 }
}
