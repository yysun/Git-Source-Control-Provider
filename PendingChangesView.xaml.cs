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
using System.Text.RegularExpressions;

namespace GitScc
{
    /// <summary>
    /// Interaction logic for PendingChangesView.xaml
    /// </summary>
    public partial class PendingChangesView : UserControl
    {
        private GitFileStatusTracker tracker;

        public PendingChangesView()
        {
            InitializeComponent();
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

        private void checkBoxSelected_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            ((GitFile)this.dataGrid1.SelectedItem).IsSelected = checkBox.IsChecked == true;
        }

        private void checkBoxAllStaged_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            foreach (var item in this.dataGrid1.Items.Cast<GitFile>())
            {
                ((GitFile) item).IsSelected = checkBox.IsChecked == true;
            }
        }

        private void dataGrid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var fileName = GetSelectedFileName();
            if (fileName == null)
            {
                this.textBoxDiff.Document.Blocks.Clear();
                return;
            }

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action act = () =>
            {
                this.textBoxDiff.BeginInit();

                this.textBoxDiff.Document.Blocks.Clear();
                this.textBoxDiff.Document.PageWidth = 1000;

                var content = tracker.DiffFile(fileName);

                // TODO: paging all text into the richtextbox
                foreach (var line in content.Split('\n').Take(256)) // take max 256 lines for now
                {

                    TextRange range = new TextRange(this.textBoxDiff.Document.ContentEnd, this.textBoxDiff.Document.ContentEnd);
                    range.Text = line.Replace("\r", "") + "\r";

                    if (line.StartsWith("+"))
                    {
                        range.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromArgb(128, 166, 255, 166)));
                    }
                    else if (line.StartsWith("-"))
                    {
                        range.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromArgb(128, 255, 166, 166)));
                    }
                    else
                    {
                        range.ApplyPropertyValue(TextElement.BackgroundProperty, null);
                    }

                }

                this.textBoxDiff.EndInit();
            };

            dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
        }

        private void dataGrid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            GetSelectedFileFullName((fileName) =>
            {
                fileName = System.IO.Path.Combine(this.tracker.GitWorkingDirectory, fileName);
                if (!File.Exists(fileName)) return;

                var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
                dte.ItemOperations.OpenFile(fileName);
            });

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

        private void GetSelectedFileName(Action<string> action)
        {
            var fileName = GetSelectedFileName();
            if (fileName == null) return;
            try
            {
                action(fileName);
            }
            catch { }
        }

        private void GetSelectedFileFullName(Action<string> action, bool fileMustExists = true)
        {
            var fileName = GetSelectedFileName();
            if (fileName == null) return;
            fileName = System.IO.Path.Combine(this.tracker.GitWorkingDirectory, fileName);

            if (fileMustExists && !File.Exists(fileName)) return;
            try
            {
                action(fileName);
            }
            catch { }
        }
        #endregion

        #region Git functions

        DateTime lastTimeRefresh = DateTime.Now.AddDays(-1);
        internal void Refresh(GitFileStatusTracker tracker)
        {
            this.tracker = tracker;
            if (tracker == null)
            {
                this.dataGrid1.ItemsSource = null;
                return;
            }

            double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
            //if (delta < 1000) return; //no refresh within 1 second

            //Debug.WriteLine("==== Pending Changes Refresh {0}", delta);

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action act = () =>
            {
                var selectedFile = GetSelectedFileName();
                this.dataGrid1.BeginInit();

                this.dataGrid1.ItemsSource = tracker.ChangedFiles;
                ICollectionView view = CollectionViewSource.GetDefaultView(this.dataGrid1.ItemsSource);
                if (view != null)
                {
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(sortMemberPath, sortDirection));
                    view.Refresh();
                }

                this.dataGrid1.EndInit();

                this.dataGrid1.SelectedValue = selectedFile;
            };

            dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);

            lastTimeRefresh = DateTime.Now;
        }

        internal void Commit()
        {
            TextRange textRange = new TextRange(
                this.textBoxComments.Document.ContentStart,
                this.textBoxComments.Document.ContentEnd);

            var comments = textRange.Text;

            if (string.IsNullOrWhiteSpace(comments))
            {
                MessageBox.Show("Please enter comments for the commit.", "Commit",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (StagedFiles())
            {
                var id = tracker.Commit(comments);
                this.textBoxComments.Document.Blocks.Clear();
                this.textBoxDiff.Document.Blocks.Clear();
                ShowStatusMessage("Commit successfully. Commit Hash: " + id);
                this.dataGrid1.FindVisualChild<CheckBox>("checkBoxAllStaged").IsChecked = false;
            }
        }

        internal void AmendCommit()
        {
            TextRange textRange = new TextRange(
                this.textBoxComments.Document.ContentStart,
                this.textBoxComments.Document.ContentEnd);

            var comments = textRange.Text;

            if (string.IsNullOrWhiteSpace(comments))
            {
                textRange.Text = tracker.LastCommitMessage;
                return;
            }
            else
            {
                if (StagedFiles())
                {
                    var id = tracker.AmendCommit(comments);
                    this.textBoxComments.Document.Blocks.Clear();
                    this.textBoxDiff.Document.Blocks.Clear();
                    ShowStatusMessage("Amend last commit successfully. Commit Hash: " + id);
                    this.dataGrid1.FindVisualChild<CheckBox>("checkBoxAllStaged").IsChecked = false;
                }
            }
        }

        private bool StagedFiles()
        {
            foreach (var item in this.dataGrid1.Items.Cast<GitFile>())
            {
                //var item = (GitFile) _item;
                if (item.IsSelected && !item.IsStaged)
                {
                    tracker.StageFile(System.IO.Path.Combine(this.tracker.GitWorkingDirectory, item.FileName));
                }
            }

            bool hasStaged = tracker == null ? false :
                             tracker.ChangedFiles.Any(f => f.IsStaged);

            if (!hasStaged)
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

        #region Diff view events
        private void textBoxDiff_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            GetSelectedFileFullName((fileName) =>
            {
                var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
                dte.ItemOperations.OpenFile(fileName);

                var pointer = textBoxDiff.GetPositionFromPoint(e.GetPosition(textBoxDiff), true);
                var text = pointer.GetTextInRun(LogicalDirection.Backward);
                if (text.IndexOf('\r') > 0) text = text.Substring(text.LastIndexOf('\r'));
                int currentColumnNumber = text.Length;

                var start = 1;
                while (true)
                {
                    var match = Regex.Match(text, "@@(.+)@@\r");
                    if (match.Success)
                    {
                        var s = match.Groups[1].Value;
                        s = s.Substring(s.IndexOf('+') + 1);
                        s = s.Substring(0, s.IndexOf(','));
                        start = Convert.ToInt32(s);

                        if (!text.StartsWith("@@")) start -= 3;
                        break;
                    }

                    pointer = pointer.GetNextContextPosition(LogicalDirection.Backward);
                    if (pointer.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.Text)
                    {
                        text = pointer.GetTextInRun(LogicalDirection.Backward) + text;
                    }
                }

                start += text.Split('\r').Where(s => !s.StartsWith("-")).Count() - 2;

                var selection = dte.ActiveDocument.Selection as EnvDTE.TextSelection;
                selection.MoveToLineAndOffset(start, currentColumnNumber);

            });
        }
        #endregion

        #region Menu Events

        private void dataGrid1_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (this.dataGrid1.SelectedCells.Count == 0) return;
            var selectedItem = this.dataGrid1.SelectedCells[0].Item as GitFile;
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

        #endregion

    }

    public static class ExtHelper
    {
        public static TChild FindVisualChild<TChild>(this DependencyObject obj, string name = null) where TChild : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is TChild && (name ==null || ((Control) child).Name == name))
                {
                    return (TChild) child;
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
