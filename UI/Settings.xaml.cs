using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : UserControl
    {
        public Settings()
        {
            InitializeComponent();
        }
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "sh.exe";
            dlg.DefaultExt = ".exe";
            dlg.Filter = "EXE (.exe)|*.exe";

            if (dlg.ShowDialog() == true)
            {
                txtGitExePath.Text = dlg.FileName;

                CheckGitBash();
            }
        }

        private void CheckGitBash()
        {
            GitBash.GitExePath = txtGitExePath.Text;
            txtGitExePath.Text = GitBash.GitExePath;
            try
            {
                txtMessage.Content = GitBash.Run("version", "");
            }
            catch (Exception ex)
            {
                txtMessage.Content = ex.Message;
            }

            if (GitBash.Exists && txtMessage.Content.ToString().StartsWith("git version"))
            {
                btnGo.Visibility = Visibility.Visible;
            }
        }


        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            GitSccOptions.Current.GitBashPath = GitBash.GitExePath;
            GitSccOptions.Current.SaveConfig();
            var sccService = BasicSccProvider.GetServiceEx<SccProviderService>();
            sccService.NoRefresh = false;
            sccService.Refresh();
        }

        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            btnGo.Visibility = Visibility.Collapsed;
            txtMessage.Content = "";
            if (e.Key == Key.Enter)
            {
                CheckGitBash();
            }
        }

        internal void Show()
        {
            this.Visibility = Visibility.Visible;
            txtGitExePath.Text = GitBash.GitExePath;
            btnGo.Visibility = Visibility.Collapsed;
            txtGitExePath.Text = GitSccOptions.Current.GitBashPath;
            txtMessage.Content = "";
        }

        internal void Hide()
        {
            this.Visibility = Visibility.Hidden;
        }

    }
}
