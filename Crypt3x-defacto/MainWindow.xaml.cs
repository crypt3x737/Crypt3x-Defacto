using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Crypt3x_defacto.Tabs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public SystemInfo systemInfo { get; } = new SystemInfo();
        public FileFinder fileFinder { get; } = new FileFinder();
        public NetworkEnumeration networkEnumerator { get; } = new NetworkEnumeration();
        public PasswordSpraying passwordSprayer { get; } = new PasswordSpraying();
        public GetUserInfo userInfo { get; } = new GetUserInfo();
        public DumpLSASS dumpLSASS { get; } = new DumpLSASS();

        private Page currentPage;
        private Border currentBorder;

        public MainWindow()
        {
            InitializeComponent();
            Application.Current.Resources.Source = new Uri("/Themes/Default.xaml", UriKind.RelativeOrAbsolute);
            passwordSprayer.GetADUsers();

            // add event handlers to the BHIS icon
            bhis_icon.MouseLeftButtonDown += info_btn_Click;
            bhis_icon.StylusDown += info_btn_Click;
            bhis_icon.TouchDown += info_btn_Click;

            // add event handlers to tab switcher buttons
            foreach (Border tab in tab_panel.Children)
            {
                tab.MouseLeftButtonDown += tab_button_Click;
                tab.StylusDown += tab_button_Click;
                tab.TouchDown += tab_button_Click;
                tab.MouseEnter += tab_button_Hover;
                tab.StylusEnter += tab_button_Hover;
                tab.TouchEnter += tab_button_Hover;
                tab.MouseLeave += tab_button_Unhover;
                tab.StylusLeave += tab_button_Unhover;
                tab.TouchLeave += tab_button_Unhover;
            }

            main_frame.Navigate(systemInfo);
            currentPage = systemInfo;
            currentBorder = start_tab;
            currentBorder.SetResourceReference(BackgroundProperty, "TabButtonBackgroundSelected");
            currentBorder.SetResourceReference(ForegroundProperty, "TabButtonTextSelected");
        }


        private void tab_button_Click(object sender, RoutedEventArgs e)
        {
            var b = (Border)sender;
            if (b == currentBorder) return;
            var page = (Page)b.Tag;

            main_frame.Navigate(page);
            currentPage = page;

            currentBorder.SetResourceReference(BackgroundProperty, "TabButtonBackgroundUnselected");
            b.SetResourceReference(BackgroundProperty, "TabButtonBackgroundSelected");

            var currText = (Label)((StackPanel)currentBorder.Child).Children[1];
            currText.SetResourceReference(ForegroundProperty, "TabButtonTextUnselected");
            var newText = (Label)((StackPanel)b.Child).Children[1];
            newText.SetResourceReference(ForegroundProperty, "TabButtonTextSelected");

            currentBorder = b;
        }
        private void tab_button_Hover(object sender, RoutedEventArgs e)
        {
            var b = (Border)sender;
            var text = (Label)((StackPanel)b.Child).Children[1];
            b.SetResourceReference(BackgroundProperty, "TabButtonBackgroundHover");
            text.SetResourceReference(ForegroundProperty, "TabButtonTextHover");
        }
        private void tab_button_Unhover(object sender, RoutedEventArgs e)
        {
            var b = (Border)sender;
            var text = (Label)((StackPanel)b.Child).Children[1];
            b.SetResourceReference(Border.BackgroundProperty, b.Tag == currentPage ? "TabButtonBackgroundSelected" : "TabButtonBackgroundUnselected");
            text.SetResourceReference(Label.ForegroundProperty, b.Tag == currentPage ? "TabButtonTextSelected" : "TabButtonTextUnselected");
        }


        private void close_btn_Click(object sender, RoutedEventArgs e) => Close();
        private void minimize_btn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void info_btn_Click(object sender, RoutedEventArgs e) => System.Diagnostics.Process.Start("https://www.blackhillsinfosec.com");


        private void title_bar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
