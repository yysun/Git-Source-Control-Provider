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
using System.ComponentModel;
using System.Diagnostics;

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

        private string sortMemberPath = "FileName";
        private ListSortDirection sortDirection = ListSortDirection.Ascending;

        private void dataGrid1_Sorting(object sender, DataGridSortingEventArgs e)
        {
            sortMemberPath = e.Column.SortMemberPath;
            sortDirection = e.Column.SortDirection != ListSortDirection.Ascending ?
                ListSortDirection.Ascending : ListSortDirection.Descending;
            
        }

        private void checkBoxStaged_Click(object sender, RoutedEventArgs e)
        {
            if (this.dataGrid1.SelectedCells.Count == 0) return;
            var selectedItem = this.dataGrid1.SelectedCells[0].Item as GitFile;
            if (selectedItem == null) return;

            var fileName = selectedItem.FileName;

            var checkBox = sender as CheckBox;
            if (checkBox.IsChecked == false)
            {
                tracker.UnStageFile(fileName);
            }
            else
            {
                tracker.StageFile(fileName);
            }
        }

        private void checkBoxAllStaged_Click(object sender, RoutedEventArgs e)
        {

        }

        private void dataGrid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (this.dataGrid1.SelectedCells.Count == 0) return;
            //var selectedItem = this.dataGrid1.SelectedCells[0].Item as GitFile;
            //if (selectedItem == null) return;
            //var fileName = selectedItem.FileName;

            //this.textBoxDiff.Document.Blocks.Clear();

            //var content = tracker.DiffFile(fileName);
            //foreach (var line in content.Split('\n'))
            //{

            //    TextRange range = new TextRange(this.textBoxDiff.Document.ContentEnd, this.textBoxDiff.Document.ContentEnd); 
            //    range.Text = line.Replace("\r", "") + "\r";

            //    if (line.StartsWith("+"))
            //    {
            //        range.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromArgb(128, 166, 255, 166)));
            //    }
            //    else if (line.StartsWith("-"))
            //    {
            //        range.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromArgb(128, 255, 166, 166)));
            //    }
            //    else
            //    {
            //        range.ApplyPropertyValue(TextElement.BackgroundProperty, null);
            //    }

            //    //this.textBoxDiff.AppendText(line);
            //}
 
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

            tracker.Commit(comments);
            this.textBoxComments.Document.Blocks.Clear();
        }

        internal void Refresh(GitFileStatusTracker tracker)
        {
            Debug.WriteLine("==== Pending Changes Refresh {0}", tracker == null ? "" : tracker.GitWorkingDirectory);

            this.tracker = tracker;
            this.dataGrid1.ItemsSource = tracker == null ? null : tracker.ChangedFiles;
            ICollectionView view = CollectionViewSource.GetDefaultView(this.dataGrid1.ItemsSource);
            if (view != null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(sortMemberPath, sortDirection));
                view.Refresh();
            }
        }

        private void btnFindChanges_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
