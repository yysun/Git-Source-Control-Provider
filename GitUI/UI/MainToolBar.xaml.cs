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
                tracker = gitViewModel.Tacker;

                if (tracker.HasGitRepository)
                {
                    this.branchList.ItemsSource = tracker.RepositoryGraph.Refs
                         .Where(r => (r.Type == RefTypes.Branch || r.Type == RefTypes.HEAD) && isLoaded(r))
                         .Select(r => r.Name);

                    this.tagList.ItemsSource = tracker.RepositoryGraph.Refs
                        .Where(r => r.Type == RefTypes.Tag && isLoaded(r))
                        .Select(r => r.Name);
                }
            }
        }

        private bool isLoaded(Ref r)
        {
            return tracker.RepositoryGraph.Commits.Any(c => c.Id == r.Id);
        }

        public MainToolBar()
        {
            InitializeComponent();
        }

        private void checkBox1_Click(object sender, RoutedEventArgs e)
        {
            if (tracker.HasGitRepository)
            {
                bool isSimplied = tracker.RepositoryGraph.IsSimplified;
                tracker.RepositoryGraph.IsSimplified = !isSimplied;
                this.lableView.Content = !isSimplied ? "Simplified view: ON" : "Simplified view: OFF";
                gitViewModel.RefreshGraph();
            }
        }

        private void branchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var name = branchList.SelectedValue as string;
            var id = tracker.RepositoryGraph.Refs
                            .Where(r => (r.Type == RefTypes.Branch || r.Type == RefTypes.HEAD) && r.Name == name)
                            .Select(r => r.Id)
                            .FirstOrDefault();
            if (id != null) HistoryViewCommands.ScrollToCommit.Execute(id, this);
        }

        private void tagList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var name = tagList.SelectedValue as string;
            var id = tracker.RepositoryGraph.Refs
                            .Where(r => r.Type == RefTypes.Tag && r.Name == name)
                            .Select(r => r.Id)
                            .FirstOrDefault();
            if (id != null) HistoryViewCommands.ScrollToCommit.Execute(id, this);
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            HistoryViewCommands.RefreshGraph.Execute(null, this);
        }
    }
}
