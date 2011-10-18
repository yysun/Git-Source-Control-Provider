using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GitScc.DataServices;
using NGit.Diff;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for CommitDetails.xaml
    /// </summary>
    public partial class CommitDetails : UserControl
    {
        internal HistoryToolWindow toolWindow;
        GitFileStatusTracker tracker;
        string commitId1, commitId2;

        public CommitDetails()
        {
            InitializeComponent();
        }

        #region show file in editor
        private void ShowFile(string tmpFileName)
        {
            var tuple = this.toolWindow.SetDisplayedFile(tmpFileName);
            if (tuple != null)
            {
                this.editor.Content = tuple.Item1;
            }
        } 
        #endregion

        #region datagrid sorting
        private string sortMemberPath = "Name";
        private ListSortDirection sortDirection = ListSortDirection.Ascending;

        private void dataGrid1_Sorting(object sender, DataGridSortingEventArgs e)
        {
            sortMemberPath = e.Column.SortMemberPath;
            sortDirection = e.Column.SortDirection != ListSortDirection.Ascending ?
                ListSortDirection.Ascending : ListSortDirection.Descending;

        }
        #endregion

        internal void Show(GitFileStatusTracker tracker, string commitId)
        {
            this.tracker = tracker;
            var repositoryGraph = tracker.RepositoryGraph;
            var commit = repositoryGraph.GetCommit(commitId);
            if (commit == null)
            {
                this.lblCommit.Content = "Cannot find commit: " + commit.Id;
            }
            else
            {
                this.lblCommit.Content = "Hash: " + commit.Id;
                this.lblMessage.Content = "Message: " + commit.Message;
                this.lblAuthor.Content = commit.CommitterName + " " + commit.CommitDateRelative;
                this.fileTree.ItemsSource = repositoryGraph.GetTree(commitId).Children;
                this.patchList.ItemsSource = repositoryGraph.GetChanges(commitId);
                //this.radioShowFileTree.IsChecked = true;
                this.radioShowFileTree.IsEnabled = true;
                this.toolWindow.ClearEditor();
                this.commitId1 = commit.ParentIds.Count > 0 ? commit.ParentIds[0] : null;
                this.commitId2 = commit.Id;
                this.btnSwitch.Visibility = Visibility.Collapsed;
                this.txtFileName.Text = "";

                this.radioShowChanges.IsChecked = true;
                this.fileTree.Visibility = Visibility.Collapsed;
                this.patchList.Visibility = Visibility.Visible;

            }
        }

        internal void Show(GitFileStatusTracker tracker, string commitId1, string commitId2)
        {
            this.tracker = tracker;
            var repositoryGraph = tracker.RepositoryGraph;

            var msg1 = repositoryGraph.Commits
                .Where(r => r.Id.StartsWith(commitId1))
                .Select(r => string.Format("{0} ({1}, {2})", r.Message, r.CommitDateRelative, r.CommitterName))
                .First().Replace("\r", "");

            var msg2 = repositoryGraph.Commits
                .Where(r => r.Id.StartsWith(commitId2))
                .Select(r => string.Format("{0} ({1}, {2})", r.Message, r.CommitDateRelative, r.CommitterName))
                .First().Replace("\r", "");

            var names1 = repositoryGraph.Refs
                .Where(r => r.Id.StartsWith(commitId1))
                .Select(r => r.Name);

            var names2 = repositoryGraph.Refs
                .Where(r => r.Id.StartsWith(commitId2))
                .Select(r => r.Name);

            var name1 = names1.Count() == 0 ? commitId1 : string.Join(", ", names1.ToArray());
            var name2 = names2.Count() == 0 ? commitId2 : string.Join(", ", names2.ToArray());

            this.lblCommit.Content = string.Format ("[{1}] {0}", msg1, name1);
            this.lblMessage.Content = string.Format("[{1}] {0}", msg2, name2);
            this.lblAuthor.Content = "";

            this.patchList.ItemsSource = repositoryGraph.GetChanges(commitId1, commitId2);
            this.radioShowChanges.IsChecked = true;
            this.radioShowFileTree.IsEnabled = false;
            this.toolWindow.ClearEditor();
            this.commitId1 = commitId1;
            this.commitId2 = commitId2;
            this.btnSwitch.Visibility = Visibility.Visible;
            this.txtFileName.Text = "";
        }

        private void radioShowFileTree_Checked(object sender, RoutedEventArgs e)
        {
            this.fileTree.Visibility = Visibility.Visible;
            this.patchList.Visibility = Visibility.Collapsed;
            this.toolWindow.ClearEditor();
        }

        private void radioShowChanges_Checked(object sender, RoutedEventArgs e)
        {
            this.fileTree.Visibility = Visibility.Collapsed;
            this.patchList.Visibility = Visibility.Visible;
            this.toolWindow.ClearEditor();
        }

        private void fileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selection = this.fileTree.SelectedValue as GitTreeObject;
            if (selection != null)
            {
                txtFileName.Text = "Content: " + selection.Name;
                var dispatcher = Dispatcher.CurrentDispatcher;
                Action act = () =>
                {
                    var content = selection.Content;
                    if (content != null)
                    {
                        var tmpFileName = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(selection.Name));
                        File.WriteAllBytes(tmpFileName, content);
                        ShowFile(tmpFileName);
                    }
                };
                dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
            }
        }

        private void patchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selection = (this.patchList.SelectedItem) as Change;
            if (selection != null)
            {
                txtFileName.Text = "Diff: " + selection.Name;
                var dispatcher = Dispatcher.CurrentDispatcher;
                Action act = () =>
                {
                    HistogramDiff hd = new HistogramDiff();
                    hd.SetFallbackAlgorithm(null);

                    RawText a = string.IsNullOrWhiteSpace(commitId1) ? new RawText(new byte[0]) :
                        new RawText(tracker.RepositoryGraph.GetFileContent(commitId1, selection.Name) ?? new byte[0]);
                    RawText b = string.IsNullOrWhiteSpace(commitId2) ? new RawText(new byte[0]) :
                        new RawText(tracker.RepositoryGraph.GetFileContent(commitId2, selection.Name) ?? new byte[0]);

                    var list = hd.Diff(RawTextComparator.DEFAULT, a, b);

                    var tmpFileName = Path.ChangeExtension(Path.GetTempFileName(), ".diff");

                    //using (Stream stream = new FileStream(tmpFileName, FileMode.CreateNew))
                    //{
                    //    DiffFormatter df = new DiffFormatter(stream);
                    //    df.Format(list, a, b);
                    //    df.Flush();
                    //}

                    using (Stream mstream = new MemoryStream(),
                          stream = new BufferedStream(mstream))
                    {
                        DiffFormatter df = new DiffFormatter(stream);
                        df.Format(list, a, b);
                        df.Flush();
                        stream.Seek(0, SeekOrigin.Begin);
                        var ret = new StreamReader(stream).ReadToEnd();
                        ret = ret.Replace("\r", "").Replace("\n", "\r\n");
                        File.WriteAllText(tmpFileName, ret);
                    }

                    ShowFile(tmpFileName);
                };

                dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
            }
        }

        private void btnSwitch_Click(object sender, RoutedEventArgs e)
        {
            this.Show(this.tracker, this.commitId2, this.commitId1);
        }

        private void menuSaveFile_Click(object sender, RoutedEventArgs e)
        {
            var selection = this.fileTree.SelectedValue as GitTreeObject;
            if (selection != null)
            {
                var fileName = selection.Name;
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = Path.GetFileName(fileName);
                dlg.DefaultExt = Path.GetExtension(fileName);
                dlg.Filter = "All files (*.*)|*.*";
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllBytes(dlg.FileName, selection.Content);
                }
            }
        }
    }
}
