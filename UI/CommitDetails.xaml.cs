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

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for CommitDetails.xaml
    /// </summary>
    public partial class CommitDetails : UserControl
    {
        GitFileStatusTracker tracker;

        public CommitDetails()
        {
            InitializeComponent();
        }


        internal void Show(GitFileStatusTracker tracker, string commitId)
        {
            this.tracker = tracker;
            var repositoryGraph = tracker.RepositoryGraph;
            var commit = repositoryGraph.GetCommit(commitId);    
            this.lblCommit.Content = "Hash: " + commit.Id;
            this.lblMessage.Content = "Message: " + commit.Message;
            this.lblAuthor.Content = commit.CommitterName + " " + commit.CommitDateRelative;
            this.detailsGrid.ColumnDefinitions[0].Width = new GridLength(250);
            this.fileTree.ItemsSource = repositoryGraph.GetTree(commitId).Children;
            this.patchList.ItemsSource = repositoryGraph.GetChanges(commitId);
        }

        internal void Show(GitFileStatusTracker tracker, string commitId1, string commitId2)
        {
            this.tracker = tracker;
            this.lblCommit.Content = commitId1;
            this.lblMessage.Content = "";
            this.lblAuthor.Content = commitId2;
            var repositoryGraph = tracker.RepositoryGraph;
            this.detailsGrid.ColumnDefinitions[0].Width = new GridLength(0);
            this.patchList.ItemsSource = repositoryGraph.GetChanges(commitId1, commitId2);
        }
    }
}
