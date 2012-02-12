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
using GitScc;
using GitScc.DataServices;

namespace GitUI.UI
{
    /// <summary>
    /// Interaction logic for MainToolBar.xaml
    /// </summary>
    public partial class MainToolBar : UserControl
    {
        private GitFileStatusTracker tracker;

        private GitViewModel gitViewModel;
        internal GitViewModel GitViewModel
        {
            set
            {
                gitViewModel = value;
                tracker = gitViewModel.Tracker;

                if (tracker.HasGitRepository)
                {
                    this.branchList.ItemsSource = tracker.RepositoryGraph.Refs
                         .Where(r => (r.Type == RefTypes.Branch || r.Type == RefTypes.HEAD) && isLoaded(r))
                         .Select(r => r.Name);

                    this.tagList.ItemsSource = tracker.RepositoryGraph.Refs
                        .Where(r => r.Type == RefTypes.Tag && isLoaded(r))
                        .Select(r => r.Name);
                }

                btnGitBash.IsEnabled = GitBash.Exists;
            }
        }

        private bool isLoaded(Ref r)
        {
            return tracker.RepositoryGraph.Commits.Any(c => c.Id == r.Id);
        }

        public MainToolBar()
        {
            InitializeComponent();
            txtCommit1.Text = txtCommit2.Text = "";
            lblSelectedCommits.Visibility = btnCompare.Visibility =
            lstSearch.Visibility = Visibility.Collapsed;
        }

        private void checkBox1_Click(object sender, RoutedEventArgs e)
        {
            if (tracker.HasGitRepository)
            {
                bool isSimplied = tracker.RepositoryGraph.IsSimplified;
                tracker.RepositoryGraph.IsSimplified = !isSimplied;
                this.lableView.Content = !isSimplied ? "Simplified view: ON" : "Simplified view: OFF";
                gitViewModel.Refresh(false);
            }
        }

        private void branchList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var name = branchList.SelectedValue as string;
            var id = tracker.RepositoryGraph.Refs
                            .Where(r => (r.Type == RefTypes.Branch || r.Type == RefTypes.HEAD) && r.Name == name)
                            .Select(r => r.Id)
                            .FirstOrDefault();
            if (id != null)
            {
                SelectCommit(id, name);
                HistoryViewCommands.ScrollToCommit.Execute(id, this);
            }
        }

        private void tagList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var name = tagList.SelectedValue as string;
            var id = tracker.RepositoryGraph.Refs
                            .Where(r => r.Type == RefTypes.Tag && r.Name == name)
                            .Select(r => r.Id)
                            .FirstOrDefault();
            if (id != null)
            {
                SelectCommit(id, name);
                HistoryViewCommands.ScrollToCommit.Execute(id, this);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            HistoryViewCommands.RefreshGraph.Execute(null, this);
        }

        #region Search commits
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = txtSearch.Text.ToLower();
            if (string.IsNullOrWhiteSpace(text))
            {
                lstSearch.ItemsSource = tracker.RepositoryGraph.Commits;
            }
            else
            {
                lstSearch.ItemsSource = tracker.RepositoryGraph.Commits
                    .Where(c => c.Message.ToLower().Contains(text) ||
                           c.Id.StartsWith(text) ||
                           c.CommitterName.ToLower().StartsWith(text) ||
                           c.CommitterEmail.ToLower().StartsWith(text) ||
                           c.CommitDateRelative.StartsWith(text));
            }
            lstSearch.Visibility = Visibility.Visible;
        }

        private void ShowSearchList()
        {
            lstSearch.Visibility = Visibility.Visible;
            lstSearch.Focus();
        }

        private void HideSearchList()
        {
            lstSearch.Visibility = Visibility.Collapsed;
            txtSearch.Focus();
        }

        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideSearchList();
            }
        }

        private void txtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                ShowSearchList();
            }
        }

        private void txtSearch_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HideSearchList();
            if (txtSearch.SelectedText == "")
            {
                txtSearch.SelectAll();
                e.Handled = true;
            }
        }

        private void lstSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                lstSearch.Visibility = Visibility.Collapsed;
                txtSearch.Focus();
                PickCommit();
            }
        }

        private void lstSearch_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PickCommit();
        }

        private void PickCommit()
        {
            var commit = lstSearch.SelectedItem as Commit;
            if (commit != null)
            {
                txtSearch.TextChanged -= new TextChangedEventHandler(txtSearch_TextChanged);
                txtSearch.Text = commit.Message;
                txtSearch.TextChanged += new TextChangedEventHandler(txtSearch_TextChanged);
                HistoryViewCommands.ScrollToCommit.Execute(commit.Id, this);
                SelectCommit(commit.ShortId, null);
            }
        }

        #endregion

        #region compare
        string id1, id2;

        internal void SelectCommit(string id, string name)
        {
            HideSearchList();
            lblSelectedCommits.Visibility = Visibility.Visible;
            if (id1 == null)
            {
                id1 = id;
                txtCommit1.Text = name ?? id;
                btnCompare.Visibility = Visibility.Collapsed;
            }
            else if (id2 == null)
            {
                id2 = id;
                txtCommit2.Text = name ?? id;
                btnCompare.Visibility = Visibility.Visible;
            }
            else
            {
                id1 = id2;
                txtCommit1.Text = txtCommit2.Text;
                id2 = id;
                txtCommit2.Text = name ?? id;
                btnCompare.Visibility = Visibility.Visible;
            }
        }

        private void btnCompare_Click(object sender, RoutedEventArgs e)
        {
            HideSearchList();
            HistoryViewCommands.CompareCommits.Execute(new string[] { id1, id2 }, this);
        }

        #endregion

        private void btnGitBash_Click(object sender, RoutedEventArgs e)
        {
            GitViewModel.OpenGitBash();
        }

    }
}
