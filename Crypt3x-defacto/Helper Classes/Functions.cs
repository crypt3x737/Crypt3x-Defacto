using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Windows.Media;

namespace Helper {
	struct Functions {
		// runs a cmd command and returns the output as list of strings
		public static IEnumerable<string> runCMD(string command, string arguments) {
			var p = new Process();

			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			p.StartInfo.UseShellExecute = false;

			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.RedirectStandardError = true;

			p.StartInfo.FileName = command;
			p.StartInfo.Arguments = arguments;

			var output = new List<string>();
			p.OutputDataReceived += (sender, args) => { if (args.Data != null) output.Add(args.Data); };

			try {
				p.Start();
				p.BeginOutputReadLine();
				p.WaitForExit();
			} catch (Win32Exception e) {
				if (e.NativeErrorCode == 2)
					System.Windows.Forms.MessageBox.Show("The '" + command + "' executable/command was not found.");
				else
					System.Windows.Forms.MessageBox.Show(e.Message);
			}

			return output;
		}

        // gets all Active Directory computer objects in the current forest
        public static IEnumerable<ComputerPrincipal> getADComputers() {
            using (var forest = Forest.GetCurrentForest()) {
                foreach (Domain domain in forest.Domains) {
                    using (domain) {
                        // create the domain context
                        var ctx = new PrincipalContext(ContextType.Domain, domain.Name);
                        // create a principal searcher to search for ComputerPrincipal objects
                        using (var comp = new ComputerPrincipal(ctx)) {
                            using (var srch = new PrincipalSearcher(comp)) {
                                foreach (ComputerPrincipal computer in srch.FindAll()) {
                                    yield return computer;
                                }
                            }
                        }
                    }
                }
            }
        }

#pragma warning disable IDE1006 // Naming Styles
        public static SolidColorBrush SolidColorBrushFromHex(string hex) => (SolidColorBrush)(new BrushConverter()).ConvertFromString(hex);
#pragma warning restore IDE1006 // Naming Styles
    }
}
