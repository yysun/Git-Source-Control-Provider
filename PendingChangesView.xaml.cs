using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GitScc.UI;
using CancellationToken = System.Threading.CancellationToken;

namespace GitScc
{
    /// <summary>
    /// Interaction logic for PendingChangesView.xaml
    /// </summary>
    public partial class PendingChangesView : UserControl
    {
        private SccProviderService service;
        private GitFileStatusTracker tracker;

        private string[] diffLines;

        private GridViewColumnHeader _currentSortedColumn;
        private ListSortDirection _lastSortDirection;

        public PendingChangesView()
        {
            InitializeComponent();
            this.service = BasicSccProvider.GetServiceEx<SccProviderService>();
        }

        #region Events
        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private void listView1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space)
                return;

            var selectedItem = this.listView1.SelectedItem as GitFile;
            if (selectedItem == null) return;
            var selected = !selectedItem.IsSelected;
            foreach (var item in this.listView1.SelectedItems)
            {
                ((GitFile)item).IsSelected = selected;
            }

            e.Handled = true;
        }

        private void checkBoxSelected_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var listViewItem = FindAncestorOfType<ListViewItem>(checkBox);
            if (listViewItem != null && !listViewItem.IsSelected)
            {
                listView1.SelectedItem = listViewItem.Content;
                return;
            }

            foreach (var item in this.listView1.SelectedItems)
            {
                ((GitFile)item).IsSelected = checkBox.IsChecked == true;
            }

            e.Handled = true;
        }

        private void checkBoxAllStaged_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            foreach (var item in this.listView1.Items.Cast<GitFile>())
            {
                ((GitFile)item).IsSelected = checkBox.IsChecked == true;
            }
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                using (service.DisableRefresh())
                {
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
                                Action action = () => this.DiffEditor.Text = "File is too big to display: " + fileName;
                                Dispatcher.Invoke(action);
                            }
                            else
                            {
                                diffLines = File.ReadAllLines(tmpFileName);
                                Action action = () => this.ShowFile(tmpFileName);
                                Dispatcher.Invoke(action);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string message = ex.Message;
                        Action action = () => ShowStatusMessage(message);
                        Dispatcher.Invoke(action);
                    }
                }
            };

            Task.Factory.StartNew(act, CancellationToken.None, TaskCreationOptions.LongRunning, SccProviderService.TaskScheduler)
                .HandleNonCriticalExceptions();
        }

        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // only enable double-click to open when exactly one item is selected
            if (listView1.SelectedItems.Count != 1)
                return;

            // disable double-click to open for the checkbox
            var checkBox = FindAncestorOfType<CheckBox>(e.OriginalSource as DependencyObject);
            if (checkBox != null)
                return;

            GetSelectedFileFullName((fileName) =>
            {
                OpenFile(fileName);
            });
        }

        private T FindAncestorOfType<T>(DependencyObject dependencyObject)
            where T : DependencyObject
        {
            for (var current = dependencyObject; current != null; current = VisualTreeHelper.GetParent(current))
            {
                T typed = current as T;
                if (typed != null)
                    return typed;
            }

            return null;
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
            if (this.listView1.SelectedItems.Count == 0)
                return null;
            var selectedItem = this.listView1.SelectedItems[0] as GitFile;
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
                var files = this.listView1.SelectedItems.Cast<GitFile>()
                    .Select(item => System.IO.Path.Combine(this.tracker.GitWorkingDirectory, item.FileName))
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

        private void VerifyGit()
        {
            var isValid = false;
            if (GitBash.Exists)
            {
                var name  = GitBash.Run("config --global user.name", "").Output;
                var email = GitBash.Run("config --global user.email", "").Output;
                isValid = !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email);
            }

            if(!isValid)
                Settings.Show();
            else
                Settings.Hide();
        }

        internal void Refresh(GitFileStatusTracker tracker)
        {
            //VerifyGit();

            if (!GitBash.Exists)
            {
                Settings.Show();
                return;
            }
            else
                Settings.Hide();

            this.label3.Content = "Changed files";
            this.tracker = tracker;
            if (tracker == null)
            {
                using (service.DisableRefresh())
                {
                    ClearUI();
                }

                return;
            }

            Func<IEnumerable<GitFile>> getChangedFiles = () =>
            {
                Action action = () => ShowStatusMessage("Getting changed files ...");
                Dispatcher.Invoke(action);
                return tracker.ChangedFiles;
            };

            Action<IEnumerable<GitFile>> refreshAction = changedFiles =>
            {
                using (service.DisableRefresh())
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    var selectedFile = GetSelectedFileName();
                    var selectedFiles = this.listView1.Items.Cast<GitFile>()
                        .Where(i => i.IsSelected)
                        .Select(i => i.FileName).ToList();

                    this.listView1.BeginInit();

                    try
                    {
                        this.listView1.ItemsSource = changedFiles;

                        SortCurrentColumn();

                        this.listView1.SelectedValue = selectedFile;
                        selectedFiles.ForEach(fn =>
                        {
                            var item = this.listView1.Items.Cast<GitFile>()
                                .Where(i => i.FileName == fn)
                                .FirstOrDefault();
                            if (item != null)
                                item.IsSelected = true;
                        });

                        ShowStatusMessage("");

                        var changed = changedFiles;
                        this.label3.Content = string.Format("Changed files (+{0} ~{1} -{2} !{3})",
                            changed.Where(f => f.Status == GitFileStatus.New || f.Status == GitFileStatus.Added).Count(),
                            changed.Where(f => f.Status == GitFileStatus.Modified || f.Status == GitFileStatus.Staged).Count(),
                            changed.Where(f => f.Status == GitFileStatus.Deleted || f.Status == GitFileStatus.Removed).Count(),
                            changed.Where(f => f.Status == GitFileStatus.Conflict).Count());
                    }
                    catch (Exception ex)
                    {
                        ShowStatusMessage(ex.Message);
                    }
                    this.listView1.EndInit();

                    stopwatch.Stop();
                    Debug.WriteLine("**** PendingChangesView Refresh: " + stopwatch.ElapsedMilliseconds);

                    if (!GitSccOptions.Current.DisableAutoRefresh && stopwatch.ElapsedMilliseconds > 1000)
                        this.label4.Visibility = Visibility.Visible;
                    else
                        this.label4.Visibility = Visibility.Collapsed;
                }
            };

            Action<Task<IEnumerable<GitFile>>> continuationAction = task =>
            {
                Dispatcher.Invoke(refreshAction, task.Result);
            };

            Task.Factory.StartNew(getChangedFiles, CancellationToken.None, TaskCreationOptions.LongRunning, SccProviderService.TaskScheduler)
                .HandleNonCriticalExceptions()
                .ContinueWith(continuationAction, TaskContinuationOptions.ExecuteSynchronously)
                .HandleNonCriticalExceptions();
        }

        internal void ClearUI()
        {
            this.listView1.ItemsSource = null;
            this.textBoxComments.Document.Blocks.Clear();
            this.ClearEditor();
            var chk = this.listView1.FindVisualChild<CheckBox>("checkBoxAllStaged");
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

        internal void Commit()
        {
            using (service.DisableRefresh())
            {
                if (HasComments() && StageSelectedFiles(true))
                {
                    string errorMessage = null;
                    try
                    {
                        ShowStatusMessage("Committing ...");
                        var result = tracker.Commit(Comments, false, chkSignOff.IsChecked == true);
                        if (result.IsSha1)
                        {
                            ShowStatusMessage("Commit successfully. Commit Hash: " + result.Message);
                            ClearUI();
                        }
                        else
                        {
                            errorMessage = result.Message;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessage = ex.Message;
                    }

                    if (!String.IsNullOrEmpty(errorMessage))
                        MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }

            service.MarkDirty(false);
        }

        internal void AmendCommit()
        {
            if (string.IsNullOrWhiteSpace(Comments))
            {
                Comments = tracker.LastCommitMessage;
                return;
            }
            else
            {
                var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
                if (dte.ItemOperations.PromptToSave == EnvDTE.vsPromptResult.vsPromptResultCancelled) return;

                using (service.DisableRefresh())
                {
                    StageSelectedFiles(false);

                    try
                    {
                        ShowStatusMessage("Amending last Commit ...");
                        var id = tracker.Commit(Comments, true, chkSignOff.IsChecked == true);
                        ShowStatusMessage("Amend last commit successfully. Commit Hash: " + id);
                        ClearUI();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        ShowStatusMessage(ex.Message);
                    }
                }

                service.MarkDirty(false);
            }
        }

        private bool StageSelectedFiles(bool showWarning)
        {
            var unstaged = this.listView1.Items.Cast<GitFile>()
                               .Where(item => item.IsSelected && !item.IsStaged)
                               .ToArray();
            var count = unstaged.Length;
            int i = 0;
            foreach (var item in unstaged)
            {
                tracker.StageFile(System.IO.Path.Combine(this.tracker.GitWorkingDirectory, item.FileName));
                ShowStatusMessage(string.Format("Staged ({0}/{1}): {2}", i++, count, item.FileName));
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
            var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
            dte.StatusBar.Text = msg;
        }
        #endregion

        #region Menu Events

        private void listView1_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (this.listView1.SelectedItems.Count == 0)
                return;

            if (this.listView1.SelectedItems.Count == 1)
            {
                var selectedItem = this.listView1.SelectedItems[0] as GitFile;
                if (selectedItem == null) return;

                switch (selectedItem.Status)
                {
                    case GitFileStatus.Added:
                    case GitFileStatus.New:
                        menuCompare.IsEnabled = menuUndo.IsEnabled = false;
                        break;

                    case GitFileStatus.Modified:
                    case GitFileStatus.Staged:
                        menuCompare.IsEnabled = menuUndo.IsEnabled = true;
                        break;

                    case GitFileStatus.Removed:
                    case GitFileStatus.Deleted:
                        menuCompare.IsEnabled = false;
                        menuUndo.IsEnabled = true;
                        break;
                }

                menuStage.Visibility = selectedItem.IsStaged ? Visibility.Collapsed : Visibility.Visible;
                menuUnstage.Visibility = !selectedItem.IsStaged ? Visibility.Collapsed : Visibility.Visible;
                menuDeleteFile.Visibility = (selectedItem.Status == GitFileStatus.New || selectedItem.Status == GitFileStatus.Modified) ?
                    Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                menuStage.Visibility =
                menuUnstage.Visibility =
                menuDeleteFile.Visibility = Visibility.Visible;
                menuUndo.IsEnabled = true;
                menuIgnore.IsEnabled = false;
                menuCompare.IsEnabled = false;
            }
        }

        private void menuCompare_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileFullName(fileName =>
            {
                var service = BasicSccProvider.GetServiceEx<SccProviderService>();
                service.CompareFile(fileName);
            });
        }

        private void menuUndo_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedFileFullName(fileName =>
            {
                var service = BasicSccProvider.GetServiceEx<SccProviderService>();
                service.UndoFileChanges(fileName);
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
            const string deleteMsg = @"

Note: if the file is included project, you need to delete the file from project in solution explorer.";

            var filesToDelete = new List<string>();

            GetSelectedFileFullName(fileName => filesToDelete.Add(fileName));

            string title = (filesToDelete.Count == 1) ? "Delete File" : "Delete Files";
            string message = (filesToDelete.Count == 1) ?
                "Are you sure you want to delete file: " + Path.GetFileName(filesToDelete.First()) + deleteMsg :
                String.Format("Are you sure you want to delete {0} selected files", filesToDelete.Count) + deleteMsg;

            if (MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var fileName in filesToDelete)
                    File.Delete(fileName);
            }
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
        #endregion

        private void DiffEditor_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            int start = 1, column = 1;
            try
            {
                if (diffLines != null && diffLines.Length > 0)
                {
                    int line = this.DiffEditor.TextArea.Caret.Line;
                    column = this.DiffEditor.TextArea.Caret.Column;

                    string text = diffLines[line];
                    while (line >= 0)
                    {
                        var match = Regex.Match(text, "^@@(.+)@@");
                        if (match.Success)
                        {
                            var s = match.Groups[1].Value;
                            s = s.Substring(s.IndexOf('+') + 1);
                            s = s.Substring(0, s.IndexOf(','));
                            start += Convert.ToInt32(s) - 2;
                            break;
                        }
                        else if (text.StartsWith("-"))
                        {
                            start--;
                        }

                        start++;
                        --line;
                        text = line >= 0 ? diffLines[line] : "";
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatusMessage(ex.Message);
                Log.WriteLine("Pending Changes View - DiffEditor_MouseDoubleClick: {0}", ex.ToString());
            }
            GetSelectedFileFullName((fileName) =>
            {
                OpenFile(fileName);
                var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
                var selection = dte.ActiveDocument.Selection as EnvDTE.TextSelection;
                selection.MoveToLineAndOffset(start - 1, column);
            });
        }

        private void OpenFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            fileName = fileName.Replace("/", "\\");
            var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
            bool opened = false;
            Array projects = (Array)dte.ActiveSolutionProjects;
            foreach (dynamic project in projects)
            {
                foreach (dynamic item in project.ProjectItems)
                {
                    if (string.Compare(item.FileNames[0], fileName, true) == 0)
                    {
                        dynamic wnd = item.Open(EnvDTE.Constants.vsViewKindPrimary);
                        wnd.Activate();
                        opened = true;
                        break;
                    }
                }
                if (opened) break;
            }

            if (!opened) dte.ItemOperations.OpenFile(fileName);
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                this.Commit();
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            GridViewColumnCollection columns = ((GridView)listView1.View).Columns;
            _currentSortedColumn = (GridViewColumnHeader)columns[columns.Count - 1].Header;
            _lastSortDirection = ListSortDirection.Ascending;
            UpdateColumnHeaderTemplate(_currentSortedColumn, _lastSortDirection);
        }

        private void listView1_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader header = e.OriginalSource as GridViewColumnHeader;
            if (header == null || header.Role == GridViewColumnHeaderRole.Padding)
                return;

            ListSortDirection direction = ListSortDirection.Ascending;
            if (header == _currentSortedColumn && _lastSortDirection == ListSortDirection.Ascending)
                direction = ListSortDirection.Descending;

            Sort(header, direction);
            UpdateColumnHeaderTemplate(header, direction);
            _currentSortedColumn = header;
            _lastSortDirection = direction;
        }

        private void SortCurrentColumn()
        {
            if (_currentSortedColumn != null)
                Sort(_currentSortedColumn, _lastSortDirection);
        }

        private void Sort(GridViewColumnHeader header, ListSortDirection direction)
        {
            if (listView1.ItemsSource != null)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(listView1.ItemsSource);
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(header.Tag as string, direction));
                view.Refresh();
            }
        }

        private void UpdateColumnHeaderTemplate(GridViewColumnHeader header, ListSortDirection direction)
        {
            // don't change the template if we're sorting by the check state
            GridViewColumn checkStateColumn = ((GridView)listView1.View).Columns[0];
            if (header.Column != checkStateColumn)
            {
                if (direction == ListSortDirection.Ascending)
                    header.Column.HeaderTemplate = Resources["HeaderTemplateArrowUp"] as DataTemplate;
                else
                    header.Column.HeaderTemplate = Resources["HeaderTemplateArrowDown"] as DataTemplate;
            }

            if (_currentSortedColumn != null && _currentSortedColumn != header && _currentSortedColumn.Column != checkStateColumn)
                _currentSortedColumn.Column.HeaderTemplate = null;
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
