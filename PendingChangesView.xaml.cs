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

        internal void Refresh(GitFileStatusTracker tracker)
        {
            this.tracker = tracker;

            this.dataGrid1.ItemsSource = tracker.ChangedFiles;
            ICollectionView view = CollectionViewSource.GetDefaultView(this.dataGrid1.ItemsSource);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortMemberPath, sortDirection));
            view.Refresh();
        }

        private void checkBoxStaged_Click(object sender, RoutedEventArgs e)
        {
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

    }
}
