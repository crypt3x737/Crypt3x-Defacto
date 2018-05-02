using DiscUtils.Iso9660;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;

namespace Crypt3x_defacto
{
    /// <summary>
    /// Interaction logic for FileFinder.xaml
    /// </summary>
    public partial class FileFinder : Page
    {
        /*
         * findText - textbox where the user enters the expressions to be searched 
         * fileText - textbox that displays the location where the search will take place
         * def_string - string of default search terms provided in the findText textbox
         * search_terms - array of expressions that the user provides to search for
         * file list - a list of File_Info objects
         * File_Info - class with two members - file and directory
         * File - string variable containing the name of the file
         * Directory - string variable containing location of the corresponding file
         */


        public class File_Info
        {
            public Uri Directory { get; }
            public Uri File { get; }
            public string DirectoryText { get { return Directory.ToString(); } }
            public string FileText { get; }
            public File_Info(string Directory, string File = null)
            {
                this.Directory = new Uri(Directory);
                if (File != null)
                {
                    this.File = new Uri(Path.Combine(Directory, File));
                    FileText = File;
                }
            }
        }

        SolidColorBrush text_start_color = Brushes.DarkGray;
        SolidColorBrush text_selected_color = Brushes.Black;

        const string file_path_text = "If not selected, search will be conducted on every available drive";
        const string default_search_terms = "pass, cred, login, wim, config, setup, iso, vmdk";

        private string[] search_terms;
        private List<File_Info> file_list = new List<File_Info>();


        // Constructor that initializes the fileText and findText textboxes.
        public FileFinder()
        {
            InitializeComponent();
            fileText.Foreground = text_start_color;
            findText.Foreground = text_start_color;
            fileText.Text = file_path_text;
            findText.Text = default_search_terms;
        }

        // Changes the findText/fileText textbox font color to black.
        private void text_GotFocus(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.TextBox)sender).Foreground = text_selected_color;
        }

        // If the textbox is empty, the default value is added and its color is reset.
        private void text_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = (System.Windows.Controls.TextBox)sender;
            if (box.Text == String.Empty)
            {
                box.Foreground = text_start_color;
                box.Text = box == fileText ? file_path_text : default_search_terms;
            }
        }

        /* 
         * Function gets called when the submit button is clicked. This is responsible for
         * calling the functions that search file and directories. It also updates the 
         * data grid once the search is completed.
         */
        async private void submitBtn_Click(object sender, RoutedEventArgs e)
        {
            disable(); // disable every textbox and button, so user can't modify while the code is running

            file_list.Clear();
            dataGrid.ItemsSource = null;

            var watch = new Stopwatch();
            watch.Start();

            // gets all the strings from the search text and puts it in the array search_term
            search_terms = getSearchTerms();

            var path = fileText.Text;
            if (path != file_path_text)
            {
                if (Directory.Exists(path))
                {
                    await Task.Run(() => get_files(path));
                    await Task.Run(() => get_directories(path));
                }
                else {
                    System.Windows.MessageBox.Show("Please make sure the file path provided is correct");
                }
            }
            else {
                foreach (var drive in Environment.GetLogicalDrives())
                    await Task.Run(() => get_directories(drive));
            }

            watch.Stop();

            status.Text = "Scan Completed in " + watch.Elapsed.ToString("g");
            dataGrid.ItemsSource = file_list;
            enable(); // enables every textbox and button once the process is completed

            if (!Directory.Exists(fileText.Text))
                fileText.Text = file_path_text;

            if (file_list.Count == 0 && Directory.Exists(fileText.Text))
                System.Windows.MessageBox.Show("No search results found");
        }

        // disable the UI Options
        public void disable()
        {
            browseBtn.IsEnabled = false;
            searchBtn.IsEnabled = false;
            fileText.IsEnabled = false;
            findText.IsEnabled = false;
            DirCheckBox.IsEnabled = false;
        }

        // enable the UI Options
        public void enable()
        {
            browseBtn.IsEnabled = true;
            searchBtn.IsEnabled = true;
            fileText.IsEnabled = true;
            findText.IsEnabled = true;
            DirCheckBox.IsEnabled = true;
        }

        // Function that gets the search terms provided by the user.
        private string[] getSearchTerms()
        {
            // make lowercase
            // split on commas (and the whitepsace around them) that are not inside quotes, not counting slash-escaped quotes
            // remove leading and trailing quotes, and unsescape escaped quotes
            // don't include empty strings
            return Regex.Split(findText.Text.ToLower(), "\\s*,\\s*(?=(?:(?:[^\"]| (?:\\\"))*\"(?:[^\"]|(?:\\\"))*\")*(?!(?:[^\"](?!\\\"))*\"))")
                .Select(s => Regex.Replace(s, "^\"(.*)\"$", "$1").Replace("\\\"", "\""))
                .Where(s => s != String.Empty)
                .ToArray();
        }

        // Function to get all the directories in a specific path.
        public void get_directories(string path)
        {
            bool isChecked = false;
            string folder_name;

            Dispatcher.Invoke(() =>
            {
                // this refer to form in WPF application
                isChecked = DirCheckBox.IsChecked.GetValueOrDefault(false);
                status.Text = "Scanning " + path;
            });

            try
            {
                foreach (var s in Directory.EnumerateDirectories(path))
                {
                    if (isChecked)
                    {
                        folder_name = s.Substring(path.Length + 1);
                        // check if the file location string has one of the search terms 
                        foreach (var item in search_terms)
                        {
                            if (folder_name.ToLower().Contains(item))
                            {
                                // if directory string has a search term then add it to File_Info.directory and add empty string to File_Info.file
                                file_list.Add(new File_Info(s));
                                break;
                            }
                        }
                    }
                    get_directories(s); // recursive function to get all directories
                    get_files(s); // reccursive function to get all files
                }
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException) { }
        }

        // Function to get all the files in a specific path.
        public void get_files(string path)
        {
            try
            {
                foreach (var s in Directory.EnumerateFiles(path))
                {
                    switch (Path.GetExtension(s))
                    {
                        case ".zip":
                            read_zip(s);
                            break;
                        case ".iso":
                            read_iso(s);
                            break;
                    }

                    var fileName = s.Substring(path.Length + 1);
                    foreach (var item in search_terms)
                    {
                        if (fileName.ToLower().Contains(item))
                        {
                            file_list.Add(new File_Info(path, fileName));
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        // Function that opens the folder Dialog to allow users to select a folder to search in. 
        private void browseBtn_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    fileText.Text = fbd.SelectedPath;
            }
        }

        // Function that reads compressed files like .zip without extracting them.
        public void read_zip(string path)
        {
            var folders = new HashSet<string>();
            string folder;
            bool add;

            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // extracts the folder path inside the archive
                        folder = entry.FullName.ToString().Substring(0, entry.FullName.Length - entry.Name.Length).ToLower();
                        foreach (var item in search_terms)
                        {
                            if (entry.Name.ToString().ToLower().Contains(item))
                            {
                                file_list.Add(new File_Info(path + folder, entry.Name));
                                break;
                            }

                            add = true;
                            if (folder.Contains(item))
                            {
                                foreach (var s in folders)
                                {
                                    if (folder.Contains(s))
                                    {
                                        add = false;
                                        break;
                                    }
                                }
                                if (add == true)
                                    folders.Add(folder);
                            }
                        }
                    }
                }
                foreach (var s in folders)
                    file_list.Add(new File_Info(path + '\\' + s));
            }
            catch { }
        }

        // Function that reads .iso files without extracting the file.
        public void read_iso(string iso)
        {
            using (var isoStream = File.Open(iso, FileMode.Open))
            {
                foreach (var item in search_terms)
                {
                    if (iso.ToLower().Contains(item))
                    {
                        var index = iso.LastIndexOf('\\');
                        var filename = iso.Substring(index + 1, iso.Length - index - 1);
                        file_list.Add(new File_Info(iso, filename));
                        return;
                    }
                }

                var cd = new CDReader(isoStream, true);
                var root = cd.Root.FullName.ToString();
                var dir = cd.GetDirectories(root);
                recurse_iso(dir, cd, iso);
            }
        }

        // Recursive function that reads all the directories in an .iso file.
        public void recurse_iso(string[] directories, CDReader cd, string iso)
        {
            foreach (var d in directories)
            {
                foreach (var f in cd.GetFiles(d))
                {
                    var index = f.LastIndexOf('\\');
                    var filename = f.Substring(index + 1, f.Length - index - 1);
                    foreach (var item in search_terms)
                    {
                        if (filename.ToLower().Contains(item))
                            file_list.Add(new File_Info(iso + f, filename));
                    }
                }

                var dir = cd.GetDirectories(d);
                recurse_iso(dir, cd, iso);
            }
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
                // Ignore this one (happens when opening directories).
            }
            catch (Win32Exception)
            {
                // And this one (happens when opening files).
            }
            catch (InvalidOperationException)
            {
                // This one too (happens when opening files).
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }
    }
}
