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
                var result = GitBash.Run("version", "");
                txtMessage.Content = result.Output;
                result = GitBash.Run("config --global user.name", "");
                txtUserName.Text = result.Output;
                result = GitBash.Run("config --global user.email", "");
                txtUserEmail.Text = result.Output;
            }
            catch (Exception ex)
            {
                txtMessage.Content = ex.Message;
            }

            if (GitBash.Exists && txtMessage.Content.ToString().StartsWith("git version"))
            {
                btnOK.Visibility = Visibility.Visible;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUserName.Text))
            {
                MessageBox.Show("Please enter user name", "Error", MessageBoxButton.OK);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtUserEmail.Text))
            {
                MessageBox.Show("Please enter user email", "Error", MessageBoxButton.OK);
                return;
            }

            try
            {
                GitBash.Run("config --global user.name \"" + txtUserName.Text + "\"", "");
                GitBash.Run("config --global user.email " + txtUserEmail.Text, "");

                GitSccOptions.Current.GitBashPath = GitBash.GitExePath;
                GitSccOptions.Current.SaveConfig();
                var sccService = BasicSccProvider.GetServiceEx<SccProviderService>();
                sccService.NoRefresh = false;
                sccService.Refresh();
            }
            catch (Exception ex)
            {
                txtMessage.Content = ex.Message;
            }
        }

        internal void Show()
        {
            this.Visibility = Visibility.Visible;
            txtGitExePath.Text = GitBash.GitExePath;
            btnOK.Visibility = Visibility.Collapsed;
            CheckGitBash();
            txtMessage.Content = "";
        }

        internal void Hide()
        {
            this.Visibility = Visibility.Hidden;
        }

        private void btnVerify_Click(object sender, RoutedEventArgs e)
        {
            btnOK.Visibility = Visibility.Collapsed;
            txtMessage.Content = "";
            CheckGitBash();
        }
    }
}
