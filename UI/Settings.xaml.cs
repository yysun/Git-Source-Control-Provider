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

            try
            {
                txtMessage.Content = GitBash.Run("version", "");
            }
            catch (Exception ex)
            {
                txtMessage.Content = ex.Message;
            }

            if (GitBash.Exists)
            {
                GitSccOptions.Current.GitBashPath = GitBash.GitExePath;
                GitSccOptions.Current.SaveConfig();
                BasicSccProvider.GetServiceEx<SccProviderService>().Refresh();
            }
        }

        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CheckGitBash();
            }
        }

        internal void Show()
        {
            this.Visibility = Visibility.Visible;
            txtGitExePath.Text = GitBash.GitExePath;
            txtMessage.Content = "";
        }

        internal void Hide()
        {
            this.Visibility = Visibility.Hidden;
        }
    }
}
