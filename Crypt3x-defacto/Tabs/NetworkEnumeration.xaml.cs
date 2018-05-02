using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Crypt3x_defacto
{
    public partial class NetworkEnumeration : Page
    {
        // This is supposedly defined in System.Linq.Parallel.Scheduling,
        // but Visual Studio says that doesn't exist, and I can't find
        // any official documentation of it.
        private static int MAX_SUPPORTED_DOP = 512;

        public NetworkEnumeration()
        {
            InitializeComponent();
        }

        public class NetworkDrive
        {
            public string Location { get; }
            public Uri Path { get; }
            public NetworkDrive(string address, string path)
            {
                Location = address;
                if (!string.IsNullOrEmpty(path))
                    Path = new Uri(path);
            }
        }

        async private void button_Click(object _, RoutedEventArgs __)
        {
            // disable the start button so it can't be clicked again until this function ends
            SystemInfo.page.disableSignIn();
            start_button.IsEnabled = false;

            status.Text = "Finding devices on the network...";

            // Get the ip's of every device on the network.
            var ipv4s = await Task.Run(() => getNetworkIPV4s());
            var ipv6s = await Task.Run(() => getNetworkIPV6s());

            // Remove duplicate ip's and ip's that don't have a host entry.
            var ips = await Task.Run(() => getUniqueDevices(ipv4s, ipv6s));

            // Try to get AD entries.
            string[] ADComputerNames;
            try
            {
                var computers = Helper.Functions.getADComputers();
                ADComputerNames = await Task.Run(() => computers.Select(c => c.Name).ToArray());
            }
            catch (ActiveDirectoryOperationException)
            {
                ADComputerNames = new string[0];
            }

            status.Text += "\n" + (ips.Length + ADComputerNames.Count()) + " unique devices found.\nFinding accessible drives...";

            // get drives
            var drives = await Task.Run(() => Environment.GetLogicalDrives().Select(d => new NetworkDrive(null, d)).Concat(getComputerDrives(ips.Concat(ADComputerNames).ToArray())));

            status.Text += "\n" + drives.Where(d => d.Path != null).Count() + " drives found.";

            // set/update the list
            drive_list.ItemsSource = drives;

            // re-enable the start button
            SystemInfo.page.enableSignIn();
            start_button.IsEnabled = true;
        }

        // does everything in button_Click(), but without gui interaction
        public static IEnumerable<NetworkDrive> getDrives()
        {
            // Get the ip's of every device on the network.
            var ipv4s = getNetworkIPV4s();
            var ipv6s = getNetworkIPV6s();

            // Remove duplicate ip's and ip's that don't have a host entry.
            var ips = getUniqueDevices(ipv4s, ipv6s);

            // Try to get AD entries.
            string[] ADComputerNames;
            try
            {
                var computers = Helper.Functions.getADComputers();
                ADComputerNames = computers.Select(c => c.Name).ToArray();
            }
            catch (ActiveDirectoryOperationException)
            {
                ADComputerNames = new string[0];
            }

            // get drives
            return Environment.GetLogicalDrives().Select(d => new NetworkDrive(null, d)).Concat(getComputerDrives(ips.Concat(ADComputerNames).ToArray()));
        }

        // gets the ipv4's of all systems on the current network
        static string[] getNetworkIPV4s()
        {
            // trim each line, select the ones that contain periods, select the first thing on that line, and again check that the first thing has a period
            return Helper.Functions.runCMD("arp", "-a").Select(x => x.Trim()).Where(x => x.Contains('.')).Select(x => x.Split()[0]).Where(x => x.Contains('.')).ToArray();
        }

        // gets the ipv6's of all systems on the current network
        static string[] getNetworkIPV6s()
        {
            // select lines that don't start with "ff0" or "Interface" and do contain a colon, and get the first thing in the line
            return Helper.Functions.runCMD("netsh", "interface ipv6 show neighbors").Select(x => x.Trim()).Where(x => !x.StartsWith("ff0") && !x.StartsWith("Interface") && x.Contains(':')).Select(x => x.Split()[0]).ToArray();
        }

        // checks ipv4's and ipv6's against each other to remove duplicates
        static string[] getUniqueDevices(string[] ipv4s, string[] ipv6s)
        {
            // Get the host entry for each ip to check its list of alternate
            // ip's (if there are any) in order to remove duplicates.
            // Also removes ip's that don't have a host entry
            // and changes ipv6 addresses to the Windows literal format.
            var ips = new List<string>();
            var all_ips = new HashSet<string>();

            if (ipv4s.Length != 0)
            {
                var parallelism = Math.Min(ipv4s.Length, MAX_SUPPORTED_DOP);
                var ipv4_hosts = ipv4s.AsParallel().WithDegreeOfParallelism(parallelism).Select(ip =>
                {
                    try
                    {
                        return new Tuple<string, IPHostEntry>(ip, Dns.GetHostEntry(ip));
                    }
                    catch { return null; }
                }).Where(t => t != null);
                foreach (var t in ipv4_hosts)
                {
                    var keep = true;
                    foreach (var addr in t.Item2.AddressList)
                        keep = all_ips.Add(addr.ToString()) && keep;
                    if (keep) ips.Add(t.Item1);
                    all_ips.Add(t.Item1);
                }
            }

            if (ipv6s.Length != 0)
            {
                var parallelism = Math.Min(ipv6s.Length, MAX_SUPPORTED_DOP);
                var ipv6_hosts = ipv6s.AsParallel().WithDegreeOfParallelism(parallelism).Select(ip =>
                {
                    try
                    {
                        return new Tuple<string, IPHostEntry>(ip, Dns.GetHostEntry(ip));
                    }
                    catch { return null; }
                }).Where(t => t != null);
                foreach (var t in ipv6_hosts)
                {
                    var keep = true;
                    foreach (var addr in t.Item2.AddressList)
                        keep = all_ips.Add(addr.ToString()) && keep;
                    if (keep) ips.Add(t.Item1.Replace(':', '-') + ".ipv6-literal.net");
                    all_ips.Add(t.Item1);
                }
            }

            return ips.ToArray();
        }

        // gets all the visible drives from a computer on the network
        static NetworkDrive[] getComputerDrives(string[] names)
        {
            var parallelism = Math.Min(names.Length, MAX_SUPPORTED_DOP);
            return names.Length != 0 ? names.AsParallel().WithDegreeOfParallelism(parallelism).SelectMany(
                name =>
                    Helper.Functions.runCMD("net", "view \\\\" + name + "\\ /ALL").Where(
                        line => line.Contains("Disk")
                    ).Select(
                        line => new NetworkDrive(name, "\\\\" + name + '\\' + line.Trim().Split()[0])
                    ).DefaultIfEmpty(new NetworkDrive(name, null))
            ).ToArray() : new NetworkDrive[0];
        }

        private void handleURIClick(object sender, RoutedEventArgs e)
        {
            // This throws an exception and opens the link anyways, so I don't know what's up with that.
            try
            {
                Process.Start((e.OriginalSource as Hyperlink).NavigateUri.AbsoluteUri);
                e.Handled = true;
            }
            catch (WebException)
            {
                // Ignore this one.
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }
    }
}
