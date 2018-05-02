using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Crypt3x_defacto
{
    /// <summary>
    /// Interaction logic for GetUserInfo.xaml
    /// </summary>
    public partial class GetUserInfo : Page
    {
        // A reference to the non-static page instance.
        public static GetUserInfo page;

        public class User
        {
            public bool Administrator { get; }
            public string Domain { get; }
            public string Display_Name { get; }
            public string Full_Name { get; }
            public string Distinguished_Name { get; }

            public object _ { get; } // spacer 1

            public string Description { get; }
            // notes field?
            public string Account_Type { get; }
            public bool Local_Account { get; }
            public bool Disabled { get; }

            public object __ { get; } // spacer 2

            public DateTime? Last_Logon { get; }
            public DateTime? Password_Last_Set { get; }
            public DateTime? Last_Bad_Password_Attempt { get; }
            public int Bad_Logon_Count { get; }
            public bool Lockout { get; }
            public DateTime? Lockout_Time { get; }
            public bool Password_Changeable { get; }
            public bool Password_Expires { get; }
            public bool Password_Required { get; }
            public DateTime? Account_Expiration_Date { get; }
            public bool Smartcard_Required { get; }

            public object ___ { get; } // spacer 3

            public string SID { get; }
            public string GUID { get; }
            public string Group_Name { get; }
            public string Status { get; }


            // Win32_UserAccount
            // https://msdn.microsoft.com/en-us/library/aa394507(v=vs.85).aspx
            public User(ManagementObject user, string group = null)
            {
                /* Unused Attributes:
                 * PSComputerName = Domain (for local accounts)
                 * Caption = Domain\Name
                 * InstallDate
                 * SIDType = User
                 */

                Administrator = group?.Contains("Admin") ?? false;
                Domain = (string)user["Domain"];
                Full_Name = (string)user["FullName"];
                Display_Name = (string)user["Name"];
                // Distinguished_Name

                Description = (string)user["Description"];
                switch ((UInt32)user["AccountType"])
                {
                    case 256:
                        Account_Type = "Temporary Duplicate Account";
                        break;
                    case 512:
                        Account_Type = "Normal Account";
                        break;
                    case 2048:
                        Account_Type = "Interdomain Trust Account";
                        break;
                    case 4096:
                        Account_Type = "Workstation Trust Account";
                        break;
                    case 8192:
                        Account_Type = "Server Trust Account";
                        break;
                    default:
                        Account_Type = "";
                        break;
                }
                Local_Account = (bool)user["LocalAccount"];
                Disabled = (bool)user["Disabled"];

                // Last_Logon
                // Password_Last_Set
                // Last_Bad_Password_Attempt
                // Bad_Logon_Count
                Lockout = (bool)user["Lockout"];
                // Lockout_Time
                Password_Changeable = (bool)user["PasswordChangeable"];
                Password_Expires = (bool)user["PasswordExpires"];
                Password_Required = (bool)user["PasswordRequired"];
                // Account_Expiration_Date
                // Smartcard_Required

                SID = (string)user["SID"];
                // GUID
                Group_Name = group;
                Status = (string)user["Status"];
            }

            // Active Directory User
            public User(AuthenticablePrincipal user, string gName)
            {
                /* Unused Attributes:
                 * UserPrincipalName = SamAccountName@UserDomainName
                 * HomeDirectory
                 * HomeDrive
                 * ScriptPath
                 * PermittedWorkstations
                 * PermittedLogonTimes
                 * Certificates
                 * DelegationPermitted
                 */

                Administrator = gName.Contains("Admin");
                Domain = SystemInfo.UserDomainName;
                Display_Name = user.DisplayName;
                Full_Name = user.SamAccountName;
                Distinguished_Name = user.DistinguishedName;

                Description = user.Description;
                Account_Type = "Active Directory Account";
                Local_Account = false;
                Disabled = !user.Enabled ?? true; // true or false when null?

                Last_Logon = user.LastLogon;
                Password_Last_Set = user.LastPasswordSet;
                Last_Bad_Password_Attempt = user.LastBadPasswordAttempt;
                Bad_Logon_Count = user.BadLogonCount;
                Lockout = user.IsAccountLockedOut();
                Lockout_Time = user.AccountLockoutTime;
                Password_Changeable = !user.UserCannotChangePassword;
                Password_Expires = !user.PasswordNeverExpires;
                Password_Required = !user.PasswordNotRequired;
                Account_Expiration_Date = user.AccountExpirationDate;
                Smartcard_Required = user.SmartcardLogonRequired;

                SID = user.Sid.ToString();
                GUID = user.Guid.ToString();
                Group_Name = gName;
                // Status
            }
        }

        public static IEnumerable<User> users;

        public GetUserInfo()
        {
            InitializeComponent();
            page = this;
            //button.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        }

        async public static Task<User[]> get_users()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var ADUsers = page.getADUsers();
                    return page.getLocalUsers().Concat(ADUsers).ToArray();
                }
                catch (ActiveDirectoryOperationException)
                {
                    return page.getLocalUsers().ToArray();
                }
                catch (Exception exc)
                {
                    System.Windows.Forms.MessageBox.Show(exc.ToString());
                    return new User[0];
                }
            });
        }

        async public Task getAndDisplayAllUsers()
        {
            SystemInfo.page.disableSignIn();
            button.IsEnabled = false;
            datagrid.ItemsSource = null;
            users = await get_users();
            datagrid.ItemsSource = users;
            button.IsEnabled = true;
            SystemInfo.page.enableSignIn();
        }

        async private void button_Click(object sender, System.Windows.RoutedEventArgs e) => await getAndDisplayAllUsers();

        private IEnumerable<User> getLocalUsers()
        {
            // Get all of the local users.
            var userAccountQuery = new SelectQuery("Win32_UserAccount");
            var userAccountSearcher = new ManagementObjectSearcher(userAccountQuery);
            var users = userAccountSearcher.Get().Cast<ManagementObject>().Where(x => (bool)x["LocalAccount"]).ToArray();

            // Get the SID's of all of the local group users, and the name of the group they're in.
            var groupUserQuery = new SelectQuery("Win32_GroupUser");
            var groupUserSearcher = new ManagementObjectSearcher(groupUserQuery);
            var groups = new List<Tuple<string, string>>();
            foreach (ManagementObject group in groupUserSearcher.Get())
            {
                var pCom = new ManagementObject((string)group["PartComponent"]);
                var gCom = new ManagementObject((string)group["GroupComponent"]);

                // Check that this is a local group and the necessary properties exist before adding the user SID and group name.
                if ((bool)gCom["LocalAccount"] && pCom.GetType().GetProperty("SID") != null && gCom.GetType().GetProperty("Name") != null)
                    groups.Add(new Tuple<string, string>((string)pCom["SID"], (string)gCom["Name"]));
            }

            //System.Windows.Forms.MessageBox.Show("Users: " + users.Count().ToString() + "\nGroups: " + groups.Count().ToString());

            // Get the local users as User objects with their group name set.
            // There will be duplicates if a user is in more than one group.
            foreach (var u in users)
            {
                var uSID = (string)u["SID"];
                var userInGroup = false;
                foreach (var g in groups)
                {
                    if (uSID == g.Item1)
                    {
                        yield return new User(u, g.Item2);
                        userInGroup = true;
                    }
                }
                if (!userInGroup)
                    yield return new User(u);
            }
        }

        // gets all users in any active directory domain on the network
        // doesn't work if you're not in an active directory domain
        private IEnumerable<User> getADUsers()
        {
            using (var forest = Forest.GetCurrentForest())
            {
                foreach (Domain domain in forest.Domains)
                {
                    using (domain)
                    {
                        // create your domain context
                        var ctx = new PrincipalContext(ContextType.Domain, domain.Name);
                        // create a principal searcher to search for GroupPrincipal objects
                        using (var qbeGroup = new GroupPrincipal(ctx))
                        {
                            using (var srch = new PrincipalSearcher(qbeGroup))
                            {
                                foreach (GroupPrincipal group in srch.FindAll())
                                {
                                    foreach (var member in group.GetMembers(true))
                                    {
                                        if (member is AuthenticablePrincipal)
                                        {
                                            yield return new User((AuthenticablePrincipal)member, group.Name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // add spaces to the datagrid column titles
        private void datagrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var title = e.PropertyName.Replace('_', ' ').Trim();
            e.Column.Header = title;
            if (title == "")
                e.Column.CanUserResize = false;
        }
    }
}
