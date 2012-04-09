using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GitScc.DataServices;
using GitUI;
using NGit.Diff;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for CommitDetails.xaml
    /// </summary>
    public partial class CommitDetails : UserControl
    {
        GitFileStatusTracker tracker;
        string commitId1, commitId2;

        public CommitDetails()
        {
            InitializeComponent();
            btnSwitch.Visibility = Visibility.Collapsed;
        }

        #region show file in editor

        private void ShowFile(string tmpFileName)
        {
            this.editor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinitionByExtension(
                Path.GetExtension(tmpFileName));
            this.editor.ShowLineNumbers = true;
            this.editor.Load(tmpFileName);
            File.Delete(tmpFileName);
        }

        private void ClearEditor()
        {
            this.editor.Text = "";
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
            try
            {
                //var stopWatch = new Stopwatch();
                //stopWatch.Start();

                this.fileTree.ItemsSource = null;
                this.patchList.ItemsSource = null;

                this.tracker = tracker;
                var repositoryGraph = tracker.RepositoryGraph;
                var commit = repositoryGraph.GetCommit(commitId);
                if (commit == null)
                {
                    this.lblCommit.Content = "Cannot find commit: " + commit.Id;
                    this.radioShowChanges.IsEnabled = false;
                    this.radioShowFileTree.IsEnabled = false;
                }
                else
                {
                    this.radioShowChanges.IsEnabled = true;
                    this.radioShowFileTree.IsEnabled = true;

                    //this.lblCommit.Content = commit.Id;
                    this.lblMessage.Content = commit.ToString();
                    this.lblAuthor.Content = commit.CommitterName + " " + commit.CommitDateRelative;
                    //this.fileTree.ItemsSource = repositoryGraph.GetTree(commitId).Children;
                    this.patchList.ItemsSource = repositoryGraph.GetChanges(commitId);
                    this.radioShowFileTree.IsEnabled = true;
                    ClearEditor();
                    this.commitId1 = commit.ParentIds.Count > 0 ? commit.ParentIds[0] : null;
                    this.commitId2 = commit.Id;
                    this.btnSwitch.Visibility = Visibility.Collapsed;
                    this.txtFileName.Text = "";

                    this.radioShowChanges.IsChecked = true;
                    this.fileTree.Visibility = Visibility.Collapsed;
                    this.patchList.Visibility = Visibility.Visible;

                    var names2 = repositoryGraph.Refs
                        .Where(r => r.Id.StartsWith(commitId2))
                        .Select(r => r.Name);

                    var name2 = names2.Count() == 0 ? commitId2 : string.Join(", ", names2.ToArray());
                    this.lblCommit.Content = name2;

                    if (this.patchList.Items.Count > 0) this.patchList.SelectedIndex = 0;
                }

                //stopWatch.Stop();
                //this.lblCommit.Content = stopWatch.ElapsedMilliseconds.ToString();
            }
            catch (Exception ex)
            {
                this.lblCommit.Content = ex.Message + " Please try again.";
            }
        }

        internal void Show(GitFileStatusTracker tracker, string commitId1, string commitId2)
        {
            try
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

                var name1 = names1.Count() == 0 ? commitId1 : string.Join(", ", names1.ToArray()) + " " + commitId1.Substring(0, 7);
                var name2 = names2.Count() == 0 ? commitId2 : string.Join(", ", names2.ToArray()) + " " + commitId2.Substring(0, 7);

                this.lblCommit.Content = string.Format("[{1}] {0}", msg2, name2);
                this.lblMessage.Content = string.Format("[{1}] {0}", msg1, name1);
                this.lblAuthor.Content = "";

                this.patchList.ItemsSource = repositoryGraph.GetChanges(commitId1, commitId2);
                this.radioShowChanges.IsChecked = true;
                this.radioShowFileTree.IsEnabled = false;
                ClearEditor();
                this.commitId1 = commitId1;
                this.commitId2 = commitId2;
                this.btnSwitch.Visibility = Visibility.Visible;
                this.txtFileName.Text = "";
            }
            catch (Exception ex)
            {
                this.lblCommit.Content = ex.Message + " Please try again.";
            }

        }

        private void radioShowFileTree_Checked(object sender, RoutedEventArgs e)
        {
            this.fileTree.Visibility = Visibility.Visible;
            this.patchList.Visibility = Visibility.Collapsed;
            ClearEditor();

            if (this.tracker != null && this.fileTree.ItemsSource == null)
            {
                var repositoryGraph = tracker.RepositoryGraph;
                if(repositoryGraph!=null)
                    this.fileTree.ItemsSource = repositoryGraph.GetTree(commitId2).Children;
            }
        }

        private void radioShowChanges_Checked(object sender, RoutedEventArgs e)
        {
            this.fileTree.Visibility = Visibility.Collapsed;
            this.patchList.Visibility = Visibility.Visible;
            ClearEditor();
        }

        private void fileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.fileTree.Visibility != Visibility.Visible) return;

            var selection = this.fileTree.SelectedValue as GitTreeObject;
            if (selection != null)
            {
                txtFileName.Text = "Content: " + selection.Name;
                var dispatcher = Dispatcher.CurrentDispatcher;
                Action act = () =>
                {
                    try
                    {
                        var content = selection.Content;
                        if (content != null)
                        {
                            var tmpFileName = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(selection.Name));
                            File.WriteAllBytes(tmpFileName, content);
                            ShowFile(tmpFileName);
                        }
                    }
                    catch { }
                };
                dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
            }
        }

        private void patchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.patchList.Visibility != Visibility.Visible) return;

            var selection = (this.patchList.SelectedItem) as Change;
            if (selection != null)
            {
                txtFileName.Text = "Diff: " + selection.Name;
                var dispatcher = Dispatcher.CurrentDispatcher;
                Action act = () =>
                {
                    try
                    {
                        var tmpFileName = this.tracker.DiffFile(selection.Name, commitId1, commitId2);
                        ShowFile(tmpFileName);
                    }
                    catch { }
                };

                dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
            }
        }

        private void btnSwitch_Click(object sender, RoutedEventArgs e)
        {
            var selected = patchList.SelectedValue;
            this.Show(this.tracker, this.commitId2, this.commitId1);
            if (selected != null)
                patchList.SelectedValue = selected;
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

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (btnSwitch.Visibility == Visibility.Collapsed)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.DefaultExt = ".patch";
                dlg.Filter = "Patch (.patch)|*.patch";

                var id = this.commitId2.Substring(0, 7);
                dlg.FileName = id + ".patch";
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        GitViewModel.Current.Patch( this.commitId2, dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                dlg.ShowNewFolderButton = true;
                
                var id1 = this.commitId1.Substring(0, 7);
                var id2 = this.commitId2.Substring(0, 7);

                dlg.Description = string.Format("Select a folder to save patches from {0} to {1}", id1, id2);

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        GitViewModel.Current.Patch(this.commitId1, this.commitId2, dlg.SelectedPath);
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
