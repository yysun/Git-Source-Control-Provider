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
using System.Diagnostics;

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
                result = GitBash.Run("config --global credential.helper", "");
                var msg = string.IsNullOrWhiteSpace(result.Output) ? 
                    "Click here to install Windows Credential for Git":
                    "Git credential helper is installed";
                txtGitCredentialHelper.Inlines.Clear();
                txtGitCredentialHelper.Inlines.Add(msg);
                result = GitBash.Run("config --global merge.tool", "");
                msg = string.IsNullOrWhiteSpace(result.Output) ?
                   "Git merge tool is not configured." :
                   "Git merge tool is " +  result.Output;
                txtGitMergeTool.Inlines.Clear();
                txtGitMergeTool.Inlines.Add(msg);
            }
            catch (Exception ex)
            {
                txtMessage.Content = ex.Message;
            }

            btnOK.IsEnabled = GitBash.Exists && txtMessage.Content.ToString().StartsWith("git version");
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
                sccService.MarkDirty(false);
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
            btnOK.IsEnabled = false;
            txtGitExePath.Text = GitSccOptions.Current.GitBashPath;
            txtMessage.Content = "";
            CheckGitBash();
        }

        internal void Hide()
        {
            this.Visibility = Visibility.Hidden;
        }

        private void btnVerify_Click(object sender, RoutedEventArgs e)
        {
            btnOK.IsEnabled = false;
            txtMessage.Content = "";
            CheckGitBash();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void txtGitExePath_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CheckGitBash();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }


    }
}
