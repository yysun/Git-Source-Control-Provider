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
                         .Where(r => r.Type == RefTypes.Branch)
                         .Select(r => r.Name);

                    this.tagList.ItemsSource = tracker.RepositoryGraph.Refs
                        .Where(r => r.Type == RefTypes.Tag)
                        .Select(r => r.Name);
                }
            }
        }

        public MainToolBar()
        {
            InitializeComponent();
        }

        private void checkBox1_Click(object sender, RoutedEventArgs e)
        {
            bool isSimplied = tracker.RepositoryGraph.IsSimplified;
            tracker.RepositoryGraph.IsSimplified = !isSimplied;
            gitViewModel.RefreshGraph();
        }
    }
}
