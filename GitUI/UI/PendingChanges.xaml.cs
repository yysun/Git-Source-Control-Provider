using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GitScc;

namespace GitUI.UI
{
    /// <summary>
    /// Interaction logic for PendingChanges.xaml
    /// </summary>
    public partial class PendingChanges : UserControl
    {
        GitFileStatusTracker tracker;
        GitViewModel service;

        private string[] diffLines;

        public PendingChanges()
        {
            InitializeComponent();
            this.service = GitViewModel.Current;
        }

        #region Events
        private string sortMemberPath = "FileName";
        private ListSortDirection sortDirection = ListSortDirection.Ascending;

        private void dataGrid1_Sorting(object sender, DataGridSortingEventArgs e)
        {
            sortMemberPath = e.Column.SortMemberPath;
            sortDirection = e.Column.SortDirection != ListSortDirection.Ascending ?
                ListSortDirection.Ascending : ListSortDirection.Descending;
        }

        private void dataGrid1_KeyDown(object sender, KeyEventArgs e)
        {
            var selectedItem = this.dataGrid1.SelectedItem as GitFile;
            if (selectedItem == null || e.Key != Key.Space) return;
            var selected = !selectedItem.IsSelected;
            foreach (var item in this.dataGrid1.SelectedItems)
            {
                ((GitFile)item).IsSelected = selected;
            }
        }

        private void checkBoxSelected_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            foreach (var item in this.dataGrid1.SelectedItems)
            {
                ((GitFile)item).IsSelected = checkBox.IsChecked == true;
            }
        }

        private void checkBoxAllStaged_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            foreach (var item in this.dataGrid1.Items.Cast<GitFile>())
            {
                ((GitFile)item).IsSelected = checkBox.IsChecked == true;
            }
        }

        private void dataGrid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var fileName = GetSelectedFileName();
            if (fileName == null)
            {
                this.ClearEditor();
                diffLines = new string[0];
                return;
            }

            Action act = () =>
            {
                service.NoRefresh = true;
                try
                {
                    //var ret = tracker.DiffFile(fileName);
                    //ret = ret.Replace("\r", "").Replace("\n", "\r\n");

                    //var tmpFileName = Path.ChangeExtension(Path.GetTempFileName(), ".diff");
                    //File.WriteAllText(tmpFileName, ret);

                    var tmpFileName = tracker.DiffFile(fileName);
                    if (!string.IsNullOrWhiteSpace(tmpFileName) && File.Exists(tmpFileName))
                    {
                        if (new FileInfo(tmpFileName).Length > 2 * 1024 * 1024)
                        {
                            this.DiffEditor.Text = "File is too big to display: " + fileName;
                        }
                        else
                        {
                            diffLines = File.ReadAllLines(tmpFileName);
                            this.ShowFile(tmpFileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    //ShowStatusMessage(ex.Message);
                    this.DiffEditor.Text = ex.Message;
                }
                service.NoRefresh = false;

            };

            this.Dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
        }

        private void ClearEditor()
        {
            this.DiffEditor.Text = "";
        }

        private void ShowFile(string fileName)
        {
            try
            {
                this.DiffEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinitionByExtension(
                    Path.GetExtension(fileName));
                this.DiffEditor.ShowLineNumbers = true;
                this.DiffEditor.Load(fileName);
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        #endregion

        #region Select File
        private string GetSelectedFileName()
        {
            if (this.dataGrid1.SelectedCells.Count == 0) return null;
            var selectedItem = this.dataGrid1.SelectedCells[0].Item as GitFile;
            if (selectedItem == null) return null;
            return selectedItem.FileName;
        }

        private void GetSelectedFileName(Action<string> action, bool changeToGitPathSeparator = false)
        {
            var fileName = GetSelectedFileName();
            if (fileName == null) return;
            try
            {
                if (changeToGitPathSeparator) fileName.Replace("\\", "/");
                action(fileName);
            }
            catch (Exception ex)
            {
                ShowStatusMessage(ex.Message);
            }
        }

        private void GetSelectedFileFullName(Action<string> action, bool fileMustExists = true)
        {
            try
            {
                var files = this.dataGrid1.SelectedItems.Cast<GitFile>()
                    .Select(item=>System.IO.Path.Combine(this.tracker.GitWorkingDirectory, item.FileName))
                    .ToList();

                foreach (var fileName in files)
                {
                    if (fileMustExists && !File.Exists(fileName)) return;
                    action(fileName);
                }
            }
            catch (Exception ex)
            {
                ShowStatusMessage(ex.Message);
            }
        }
        #endregion

        #region Git functions

        DateTime lastTimeRefresh = DateTime.Now.AddDays(-1);
        internal void Refresh(GitFileStatusTracker tracker)
        {
            this.label3.Content = "Changed files";

            this.tracker = tracker;
            this.chkAmend.IsChecked = false;
            this.chkSignOff.IsChecked = false;
            this.chkNewBranch.IsChecked = false;

            if (tracker == null)
            {
                //service.NoRefresh = true;
                ClearUI();
                service.NoRefresh = false;
                return;
            }

            Action act = () =>
            {
                lblMessage.Content = "Commit to: " + tracker.CurrentBranch;
                service.NoRefresh = true;
                ShowStatusMessage("Getting changed files ...");

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                var selectedFile = GetSelectedFileName();
                var selectedFiles = this.dataGrid1.Items.Cast<GitFile>()
                    .Where(i => i.IsSelected)
                    .Select(i => i.FileName).ToList();

                this.dataGrid1.BeginInit();

                try
                {

                    this.dataGrid1.ItemsSource = tracker.ChangedFiles;

                    ICollectionView view = CollectionViewSource.GetDefaultView(this.dataGrid1.ItemsSource);
                    if (view != null)
                    {
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new SortDescription(sortMemberPath, sortDirection));
                        view.Refresh();
                    }

                    this.dataGrid1.SelectedValue = selectedFile;
                    selectedFiles.ForEach(fn =>
                    {
                        var item = this.dataGrid1.Items.Cast<GitFile>()
                            .Where(i => i.FileName == fn)
                            .FirstOrDefault();
                        if (item != null) item.IsSelected = true;
                    });

                    ShowStatusMessage("");

                    var changed = tracker.ChangedFiles;
                    this.label3.Content = string.Format("Changed files: ({0}) +{1} ~{2} -{3} !{4}", tracker.CurrentBranch,
                        changed.Where(f => f.Status == GitFileStatus.New || f.Status == GitFileStatus.Added).Count(),
                        changed.Where(f => f.Status == GitFileStatus.Modified || f.Status == GitFileStatus.Staged).Count(),
                        changed.Where(f => f.Status == GitFileStatus.Deleted || f.Status == GitFileStatus.Removed).Count(),
                        changed.Where(f => f.Status == GitFileStatus.Conflict).Count());
                }
                catch (Exception ex)
                {
                    ShowStatusMessage(ex.Message);
                }
                this.dataGrid1.EndInit();

                stopwatch.Stop();
                Debug.WriteLine("**** PendingChangesView Refresh: " + stopwatch.ElapsedMilliseconds);

                //if (!GitSccOptions.Current.DisableAutoRefresh && stopwatch.ElapsedMilliseconds > 1000)
                //    this.label4.Visibility = Visibility.Visible;
                //else
                //    this.label4.Visibility = Visibility.Collapsed;

                service.NoRefresh = false;
                service.lastTimeRefresh = DateTime.Now; //important!!

            };

            this.Dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
        }

        internal void ClearUI()
        {
            this.dataGrid1.ItemsSource = null;
            this.textBoxComments.Document.Blocks.Clear();
            this.ClearEditor();
            var chk = this.dataGrid1.FindVisualChild<CheckBox>("checkBoxAllStaged");
            if (chk != null) chk.IsChecked = false;
        }

        private string Comments
        {
            get
            {
                TextRange textRange = new TextRange(
                    this.textBoxComments.Document.ContentStart,
                    this.textBoxComments.Document.ContentEnd);
                return textRange.Text;
            }
            set
            {
                TextRange textRange = new TextRange(
                    this.textBoxComments.Document.ContentStart,
                    this.textBoxComments.Document.ContentEnd);
                textRange.Text = value;
            }
        }

        private bool HasComments()
        {
            if (string.IsNullOrWhiteSpace(Comments))
            {
                MessageBox.Show("Please enter comments for the commit.", "Commit",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            else
                return true;
        }

        private bool StageSelectedFiles(bool showWarning)
        {
            var unstaged = this.dataGrid1.Items.Cast<GitFile>()
                               .Where(item => item.IsSelected && !item.IsStaged)
                               .ToArray();
            var count = unstaged.Length;
            int i = 0;
            foreach (var item in unstaged)
            {
                tracker.StageFile(System.IO.Path.Combine(this.tracker.GitWorkingDirectory, item.FileName));
                ShowStatusMessage(string.Format("Staged ({0}/{1}): {2}", i++, count, item.FileName));
                service.lastTimeRefresh = DateTime.Now;
            }

            bool hasStaged = tracker == null ? false :
                             tracker.ChangedFiles.Any(f => f.IsStaged);

            if (!hasStaged && showWarning)
            {
                MessageBox.Show("No file has been staged for commit.", "Commit",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            return hasStaged;
        }

        private void ShowStatusMessage(string msg)
        {

        }
        #endregion

        #region Menu Events

        private void dataGrid1_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (this.dataGrid1.SelectedItems.Count == 0) return;

            if (this.dataGrid1.SelectedItems.Count == 1)
            {
                var selectedItem = this.dataGrid1.SelectedItem as GitFile;
                if (selectedItem == null) return;

                switch (selectedItem.Status)
                {
                    case GitFileStatus.Added:
                    case GitFileStatus.New:
                        //menuCompare.IsEnabled = 
                        menuUndo.IsEnabled = false;
                        break;

                    case GitFileStatus.Modified:
                    case GitFileStatus.Staged:
                        //menuCompare.IsEnabled = 
                        menuUndo.IsEnabled = true;
                        break;

                    case GitFileStatus.Removed:
                    case GitFileStatus.Deleted:
                        //menuCompare.IsEnabled = false;
                        menuUndo.IsEnabled = true;
                        break;
                }

                menuStage.Visibility = selectedItem.IsStaged ? Visibility.Collapsed : Visibility.Visible;
                menuUnstage.Visibility = !selectedItem.IsStaged ? Visibility.Collapsed : Visibility.Visible;
                menuDeleteFile.Visibility = (selectedItem.Status == GitFileStatus.New || selectedItem.Status == GitFileStatus.Modified) ?
                    Visibility.Visible : Visibility.Collapsed;
                menuIgnore.IsEnabled = true;
            }
            else
            {
                menuStage.Visibility = 
                menuUnstage.Visibility =
                menuDeleteFile.Visibility = Visibility.Visible;
                menuUndo.IsEnabled = true;
                menuIgnore.IsEnabled = false;
                //menuCompare.IsEnabled = false;
            }
        }

        private void menuCompare_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileFullName(fileName =>
            {
                //service.CompareFile(fileName);
            });
        }

        private void menuUndo_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileFullName(fileName =>
            {
                GitFileStatus status = tracker.GetFileStatus(fileName);
                if (status == GitFileStatus.Modified || status == GitFileStatus.Staged ||
                    status == GitFileStatus.Deleted || status == GitFileStatus.Removed)
                {
                    if (MessageBox.Show("Are you sure you want to undo changes for " + Path.GetFileName(fileName) +
                        " and restore a version from the last commit? ",
                        "Undo Changes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        tracker.CheckOutFile(fileName);
                    }
                }
            }, false); // file must exists check flag is false
        }

        private void menuStage_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileFullName(fileName =>
            {
                tracker.StageFile(fileName);
                ShowStatusMessage("Staged file: " + fileName);
            }, false);
        }

        private void menuUnstage_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileFullName(fileName =>
            {
                tracker.UnStageFile(fileName);
                ShowStatusMessage("Un-staged file: " + fileName);
            }, false);
        }

        private void menuDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileFullName(fileName =>
            {
                if (MessageBox.Show("Are you sure you want to delete file: " + Path.GetFileName(fileName),
                                   "Delete File",
                                   MessageBoxButton.YesNo,
                                   MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    File.Delete(fileName);
                }
            });
        }

        #endregion

        #region Ignore files
        private void menuIgnore_Click(object sender, RoutedEventArgs e)
        {

        }

        private void menuIgnoreFile_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileName((fileName) =>
            {
                tracker.AddIgnoreItem(fileName);
            }, true);
        }

        private void menuIgnoreFilePath_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileName((fileName) =>
            {
                tracker.AddIgnoreItem(Path.GetDirectoryName(fileName) + "*/");
            }, true);
        }

        private void menuIgnoreFileExt_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileName((fileName) =>
            {
                tracker.AddIgnoreItem("*" + Path.GetExtension(fileName));
            }, true);
        }

        private void menuRefresh_Click(object sender, RoutedEventArgs e)
        {
            HistoryViewCommands.RefreshGraph.Execute(null, this);
        } 

        #endregion

        private void chkNewBranch_Checked(object sender, RoutedEventArgs e)
        {
            txtNewBranch.Focus();
        }

        private void txtNewBranch_TextChanged(object sender, TextChangedEventArgs e)
        {
            chkNewBranch.IsChecked = txtNewBranch.Text.Length > 0;
        }

        private void chkAmend_Checked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Comments))
            {
                Comments = tracker.LastCommitMessage;
            }
        }

        private void btnPendingChanges_Click(object sender, RoutedEventArgs e)
        {
            if (tracker == null) return;

            try
            {
                service.NoRefresh = true;

                if (chkNewBranch.IsChecked == true)
                {
                    if (string.IsNullOrWhiteSpace(txtNewBranch.Text))
                    {
                        MessageBox.Show("Please enter new branch name.", "Commit",
                            MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        txtNewBranch.Focus();
                        return;
                    }
                    tracker.CheckOutBranch(txtNewBranch.Text, true);
                }

                var isAmend = chkAmend.IsChecked == true;
                if (HasComments() && StageSelectedFiles(!isAmend))
                {
                    ShowStatusMessage("Committing ...");
                    var id = tracker.Commit(Comments, isAmend, chkSignOff.IsChecked == true);
                    ShowStatusMessage("Commit successfully. Commit Hash: " + id);
                    ClearUI();

                    HistoryViewCommands.CloseCommitDetails.Execute(this, null);
                    HistoryViewCommands.RefreshGraph.Execute(this, null);
                }
                service.NoRefresh = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                ShowStatusMessage(ex.Message);
            }
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnPendingChanges_Click(this, null);
            }
        }
    }

    public static class ExtHelper
    {
        public static TChild FindVisualChild<TChild>(this DependencyObject obj, string name = null) where TChild : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is TChild && (name == null || ((Control)child).Name == name))
                {
                    return (TChild)child;
                }
                else
                {
                    TChild childOfChild = FindVisualChild<TChild>(child, name);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
    }
}
