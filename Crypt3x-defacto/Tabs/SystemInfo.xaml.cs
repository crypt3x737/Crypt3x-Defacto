using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Crypt3x_defacto {
    /// <summary>
    /// Interaction logic for Info.xaml
    /// </summary>
    public partial class SystemInfo : Page {
        // A reference to the non-static page instance.
        public static SystemInfo page;

        // A set of ID's for functions that have called disableSignIn()
        // (and have not yet called enableSignIn()). This is kept so that
        // multiple functions can call those functions asyncronously, and
        // sign-in will only be enabled when the set is empty.
        private HashSet<int> SignInKeys = new HashSet<int>();

        // Wrapper for Helper.UserImpersonator so it can be used as a node in a tree.
        private class iNode : Helper.UserImpersonator {
            public iNode parent;
            public List<iNode> children;
            public int depth;
            public iNode(string n, string d, SecureString p, int de) : base(n, d, p) { depth = de; }
        }
        private static iNode impersonationTreeRoot, currentImpersonation;

        public static string UserName { get; private set; }
        public static string UserDomainName { get; private set; }

        // only for use in updateImpersonationTreeGUI
        private static Thickness borderRight = new Thickness(0, 0, 1, 0);
        private static Thickness borderLeft = new Thickness(1, 0, 0, 0);
        private static Thickness borderLeftRight = new Thickness(1, 0, 1, 0);
        private static Thickness nodeMargin = new Thickness(4, 0, 4, 0);
        private static int verticalLineHeight = 8;
        private static int nodeWidth = 200;


        public SystemInfo() {
            InitializeComponent();
            page = this;
            UserName = Environment.UserName;
            UserDomainName = Environment.UserDomainName;
            impersonationTreeRoot = new iNode(UserName, UserDomainName, null, 0);
            currentImpersonation = impersonationTreeRoot;
            updateUserInfo();
            updateImpersonationTreeGUI();
        }

        public bool ConnectedToAD() {
            if (currentImpersonation.depth == 0) {
                // Apparently this equals "NTLM" when you're not connected to anything.
                // It can be other things as well, but I don't know what they are.
                return WindowsIdentity.GetCurrent().AuthenticationType == "Kerberos";
            } else {
                // I don't think an impersonation would ever succeed if you weren't connected to Active Directory.
                return true;
            }
        }

        private void updateUserInfo() {
            username.Text = UserName;
            connected_to_ad.Text = ConnectedToAD() ? "Yes" : "No";
            ad_domain.Text = UserDomainName;
            signInDom.Text = UserDomainName;
        }

        private void updateImpersonationTreeGUI() {
            impersonationTree.Content = updateImpersonationTreeGUI(impersonationTreeRoot);
        }
        private FrameworkElement updateImpersonationTreeGUI(iNode node) {
            // add user info box
            var b = new Border {
                Background = Brushes.Transparent,
                Margin = nodeMargin,
                Width = nodeWidth
            };

            if (node == currentImpersonation)
                b.SetResourceReference(Border.BackgroundProperty, "CurrentUserImpersonationNodeBackground");

            // add click event handlers
            b.MouseLeftButtonDown += iNodeClick;
            b.StylusDown += iNodeClick;
            b.TouchDown += iNodeClick;

            // add hover event handlers
            b.MouseEnter += iNodeHover;
            b.StylusEnter += iNodeHover;
            b.TouchEnter += iNodeHover;
            b.MouseLeave += iNodeUnhover;
            b.StylusLeave += iNodeUnhover;
            b.TouchLeave += iNodeUnhover;

            // add this node to the element so we can get it easily later
            b.Tag = node;

            // add the user data
            var dataContainer = new StackPanel{Orientation = Orientation.Vertical};
            dataContainer.Children.Add(new TextBlock{Text = node.name});
            dataContainer.Children.Add(new TextBlock{Text = node.domain});
            b.Child = dataContainer;

            // if there are no children, return this
            if (node.children == null) return b;

            /* WPF Layout
             * Grid with 5 rows and 2 * node.children columns
             *   - only the first row contents if there are no children
             *   first row
             *     Border
             *       StackPanel Vertical
             *         TextBlock UserName
             *         TextBlock Domain
             *   second row
             *     Rectangle 2px wide spanning all columns
             *   third row
             *     Border Right 2px
             *     Rectangle spanning 2 * children - 2 columns
             *     Border Left 2px
             *   fourth row
             *     Border Right
             *     Border LeftRight
             *     - repeat for children / 2
             *     Border Left
             *   fifth row
             *     - this is a child node
             */

            var ccount = node.children.Count;

            // create grid to hold lines and children
            var grid = new Grid();

            // add rows
            grid.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});
            grid.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});
            grid.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});
            grid.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});
            grid.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});

            // add columns
            for (var i = 0; i < ccount; ++i) {
                grid.ColumnDefinitions.Add(new ColumnDefinition{Width = GridLength.Auto});
                grid.ColumnDefinitions.Add(new ColumnDefinition{Width = GridLength.Auto});
            }

            // add parent
            Grid.SetRow(b, 0);
            Grid.SetColumn(b, 0);
            Grid.SetColumnSpan(b, ccount * 2);
            grid.Children.Add(b);

            // add vertical line from parent
            var fromParent = new Rectangle{Width = 2, Height = verticalLineHeight};
            Grid.SetRow(fromParent, 1);
            Grid.SetColumn(fromParent, 0);
            Grid.SetColumnSpan(fromParent, ccount * 2);
            grid.Children.Add(fromParent);

            // add horizontal line
            var leftBorder = new Border{BorderThickness = borderRight, Height = 2};
            Grid.SetRow(leftBorder, 2);
            Grid.SetColumn(leftBorder, 0);
            grid.Children.Add(leftBorder);
            if (ccount > 1) {
                var column = new Rectangle();
                Grid.SetRow(column, 2);
                Grid.SetColumn(column, 1);
                Grid.SetColumnSpan(column, 2 * ccount - 2);
                grid.Children.Add(column);
            }
            var rightBorder = new Border{BorderThickness = borderLeft, Height = 2};
            Grid.SetRow(rightBorder, 2);
            Grid.SetColumn(rightBorder, 2 * ccount - 1);
            grid.Children.Add(rightBorder);

            // add vertical lines to children
            leftBorder = new Border{BorderThickness = borderRight, Height = verticalLineHeight};
            Grid.SetRow(leftBorder, 3);
            Grid.SetColumn(leftBorder, 0);
            grid.Children.Add(leftBorder);
            for (var i = 1; ccount > 1 && i <= ccount; i += 2) {
                var leftRightBorder = new Border{BorderThickness = borderLeftRight, Height = verticalLineHeight};
                Grid.SetRow(leftRightBorder, 3);
                Grid.SetColumn(leftRightBorder, i);
                Grid.SetColumnSpan(leftRightBorder, 2);
                grid.Children.Add(leftRightBorder);
            }
            rightBorder = new Border{BorderThickness = borderLeft, Height = verticalLineHeight};
            Grid.SetRow(rightBorder, 3);
            Grid.SetColumn(rightBorder, 2 * ccount - 1);
            grid.Children.Add(rightBorder);

            // recursively add children
            for (var i = 0; i < ccount; ++i) {
                var child = updateImpersonationTreeGUI(node.children[i]);
                Grid.SetRow(child, 4);
                Grid.SetColumn(child, i * 2);
                Grid.SetColumnSpan(child, 2);
                grid.Children.Add(child);
            }

            return grid;
        }


        // Disables user sign-in. Uses the calling method's MetadataToken, obtained from the stack trace, as an identifier.
        public void disableSignIn() {
            SignInKeys.Add(new StackFrame(1).GetMethod().MetadataToken);
            signIn.IsEnabled = false;
        }

        // Enables user sign-in. Uses the calling method's MetadataToken, obtained from the stack trace, as an identifier.
        public void enableSignIn() {
            SignInKeys.Remove(new StackFrame(1).GetMethod().MetadataToken);
            if (SignInKeys.Count == 0)
                signIn.IsEnabled = true;
        }


        public void addChildToImpersonationTree(string name, string domain, string password) {
            // Convert string password to SecureString securePass.
            var securePass = new System.Net.NetworkCredential("", password).SecurePassword;

            // Create new node.
            var i = new iNode(name, domain, securePass, currentImpersonation.depth + 1);
            i.parent = currentImpersonation;

            // Add it as a child of the current node.
            if (currentImpersonation.children == null)
                currentImpersonation.children = new List<iNode>();
            currentImpersonation.children.Add(i);

            // Update the GUI.
            updateImpersonationTreeGUI();
        }

        private async void signIn_Click(object sender, RoutedEventArgs e) {
            signIn.IsEnabled = false;

            var i = new iNode(signInUser.Text, signInDom.Text.ToUpper(), signInPass.SecurePassword, currentImpersonation.depth + 1);

            if (await Task.Run((Func<bool>)i.start)) {
                signInUser.Clear();
                signInPass.Clear();

                UserName = i.name;
                UserDomainName = i.domain;

                i.parent = currentImpersonation;
                if (currentImpersonation.children == null)
                    currentImpersonation.children = new List<iNode>();
                currentImpersonation.children.Add(i);
                currentImpersonation = i;

                updateUserInfo();
                updateImpersonationTreeGUI();
            }

            signIn.IsEnabled = true;
        }

        // Select the input when it is clicked or focused.
        private void signInUserDom_ClickFocus(object sender, RoutedEventArgs e) => (sender as TextBox).SelectAll();
        private void signInPass_ClickFocus(object sender, RoutedEventArgs e) => (sender as PasswordBox).SelectAll();


        // when a node in the impersonation tree is clicked
        private void iNodeClick(object sender, RoutedEventArgs e) {
            var targetNode = (iNode)((Border)sender).Tag;

            // if the node that was clicked isn't the current impersonation,
            // make it the current impersonation
            if (targetNode != currentImpersonation) {
                // an alias
                var currNode = currentImpersonation;

                // path to get to the target node
                var path = new Stack<iNode>();

                // move the target up the tree
                // keeping track of the path to the true target
                while (targetNode.depth > currNode.depth) {
                    path.Push(targetNode);
                    targetNode = targetNode.parent;
                }

                // move the current node up the tree
                while (currNode.depth > targetNode.depth) {
                    currNode.stop();
                    currNode = currNode.parent;
                    currentImpersonation = currNode;
                }

                // move both up the tree until they meet
                while (targetNode != currNode) {
                    path.Push(targetNode);
                    targetNode = targetNode.parent;
                    currNode.stop();
                    currNode = currNode.parent;
                    currentImpersonation = currNode;
                }

                // move down the tree to the target
                while (path.Count > 0) {
                    var next = path.Pop();
                    if (next.start())
                        currentImpersonation = next;
                    else break;
                }

                UserName = currentImpersonation.name;
                UserDomainName = currentImpersonation.domain;
                updateUserInfo();
                updateImpersonationTreeGUI();
            }

            e.Handled = true;
        }

        // when a node in the impersonation tree is hovered over
        private void iNodeHover(object sender, RoutedEventArgs e) => ((Border)sender).SetResourceReference(Border.BackgroundProperty, "CurrentUserImpersonationNodeBackgroundHover");
        private void iNodeUnhover(object sender, RoutedEventArgs e) {
            var b = (Border)sender;
            var node = (iNode)b.Tag;
            if (node == currentImpersonation)
                b.SetResourceReference(Border.BackgroundProperty, "CurrentUserImpersonationNodeBackground");
            else b.Background = Brushes.Transparent;
        }


        // Sign-in when the Enter key is pressed.
        private void signIn_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter)
                signIn_Click(sender, e);
        }
    }
}
