using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace BeatlesLauncher {
 static class DirectPlay {
  [STAThread]static int Main() {
   const string title="The Beatles: Rock Band";
   try {
    if(Process.GetProcessesByName("beatlesrockband-guitar").Any()){MessageBox.Show("The game is already running.",title,MessageBoxButtons.OK,MessageBoxIcon.Information);return 1;}
    var settings=GameFiles.LoadSettings();
    var result=GameFiles.Check(settings.XexPath);
    if(!result.Ready){MessageBox.Show("Open BeatlesRockBandLauncher.exe first to choose your game and settings.\n\n"+result.Message,title,MessageBoxButtons.OK,MessageBoxIcon.Information);return 2;}
    var game=GameFiles.Start(settings,result.Root);
    if(game==null)throw new Exception("Windows could not start the game.");
    return 0;
   } catch(Exception error) {
    MessageBox.Show("Could not start the game.\n\n"+error.Message,title,MessageBoxButtons.OK,MessageBoxIcon.Error);return 3;
   }
  }
 }
}
