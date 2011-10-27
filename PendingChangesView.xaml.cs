﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TextManager.Interop;
using NGit.Api;
using GitScc.UI;

namespace GitScc
{
    /// <summary>
    /// Interaction logic for PendingChangesView.xaml
    /// </summary>
    public partial class PendingChangesView : UserControl
    {
        private GitFileStatusTracker tracker;
        private ToolWindowWithEditor toolWindow;
        private IVsTextView textView;
        private string[] diffLines;

        public PendingChangesView(ToolWindowWithEditor toolWindow)
        {
            InitializeComponent();
            this.toolWindow = toolWindow;
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
                ((GitFile)item).IsSelected = checkBox.IsChecked == true;
            }
        }

        private void dataGrid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var fileName = GetSelectedFileName();
            if (fileName == null)
            {
                this.toolWindow.ClearEditor();
                return;
            }

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action act = () =>
            {
                try
                {
                    var ret = tracker.DiffFile(fileName);
                    ret = ret.Replace("\r", "").Replace("\n", "\r\n");

                    var tmpFileName = Path.ChangeExtension(Path.GetTempFileName(), ".diff");
                    File.WriteAllText(tmpFileName, ret);

                    var tuple = this.toolWindow.SetDisplayedFile(tmpFileName);
                    if (tuple != null)
                    {
                        this.DiffEditor.Content = tuple.Item1;
                        this.textView = tuple.Item2;
                        diffLines = ret.Split('\n');
                    }
                }
                catch { }

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
                ClearUI();
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

        private void ClearUI()
        {
            this.textBoxComments.Document.Blocks.Clear();
            this.toolWindow.ClearEditor();
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

        internal void Commit()
        {
            if (HasComments() && StageSelectedFiles())
            {
                try
                {
                    ShowStatusMessage("Committing ...");
                    var id = tracker.Commit(Comments);
                    ShowStatusMessage("Commit successfully. Commit Hash: " + id);
                    ClearUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    ShowStatusMessage(ex.Message);
                }
            }
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
                if (StageSelectedFiles())
                {
                    try
                    {
                        ShowStatusMessage("Amending last Commit ...");
                        var id = tracker.AmendCommit(Comments);
                        ShowStatusMessage("Amend last commit successfully. Commit Hash: " + id);
                        ClearUI();

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        ShowStatusMessage(ex.Message);
                    }
                }
            }
        }

        private bool StageSelectedFiles()
        {
            foreach (var item in this.dataGrid1.Items.Cast<GitFile>())
            {
                //var item = (GitFile) _item;
                if (item.IsSelected && !item.IsStaged)
                {
                    tracker.StageFile(System.IO.Path.Combine(this.tracker.GitWorkingDirectory, item.FileName));
                }
            }

            bool hasStaged = tracker != null && tracker.ChangedFiles.Any(f => f.IsStaged);

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
            menuDeleteFile.Visibility = (selectedItem.Status == GitFileStatus.New || selectedItem.Status == GitFileStatus.Modified) ?
                Visibility.Visible : Visibility.Collapsed;
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

            GetSelectedFileFullName(fileName =>
            {
                if (MessageBox.Show("Are you sure you want to delete file: " + Path.GetFileName(fileName) + deleteMsg,
                                   "Delete File",
                                   MessageBoxButton.YesNo,
                                   MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    File.Delete(fileName);
                }
            });
        }

        #endregion

        private void DiffEditor_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int start = 1, column = 1;
            try
            {
                if (this.textView != null && diffLines != null && diffLines.Length > 0)
                {
                    int line;
                    textView.GetCaretPos(out line, out column);

                    string text = diffLines[line];
                    while (line >=0)
                    {
                        var match = Regex.Match(text, "^@@(.+)@@");
                        if (match.Success)
                        {
                            var s = match.Groups[1].Value;
                            s = s.Substring(s.IndexOf('+') + 1);
                            s = s.Substring(0, s.IndexOf(','));
                            start += Convert.ToInt32(s) - 2;
                            //if (!text.StartsWith("@@")) start -= 3;
                            break;
                        }
                        else if (text.StartsWith("-"))
                        {
                            start--;
                        }

                        start++;
                        --line;
                        text = line>=0 ? diffLines[line] : "";
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("Pending Changes View - DiffEditor_MouseDoubleClick: {0}", ex.ToString());
            } 
            GetSelectedFileFullName((fileName) =>
            {
                var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
                dte.ItemOperations.OpenFile(fileName);
                var selection = dte.ActiveDocument.Selection as EnvDTE.TextSelection;
                selection.MoveToLineAndOffset(start, column);
            });
        }

        public void PullRebase()
        {
            if (dataGrid1.Items.Count == 0)
            {
                ShowStatusMessage("Starting to pull rebase");
                string returnedMessage = tracker.PullRebase();
                if (returnedMessage.Contains("Current branch master is up to date."))
                    ShowStatusMessage("Current branch master is up to date.");
                else if (returnedMessage.Contains("When you have resolved this problem run \"git rebase --continue\"."))
                {
                    ShowDialog("Rebase fail.\n" + returnedMessage, MessageBoxImage.Error);
                    ShowStatusMessage("Rebase fail.");
                }
                else
                    ShowStatusMessage("Pull rebase success!");
                Clipboard.SetText(returnedMessage);
            }
            else
            {
                ShowDialog("Can not pull, you have unstaged changes", MessageBoxImage.Exclamation);
            }
        }
        private MessageBoxResult ShowDialog(string message, MessageBoxImage img = MessageBoxImage.None, MessageBoxButton btn = MessageBoxButton.OK)
        {
            return MessageBox.Show(message, "Git SS Provider", btn, img);
        }
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
