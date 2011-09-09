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
using System.Windows.Threading;
using NGit.Revwalk;
using NGit.Revplot;
using GitScc.DataServices;

namespace GitScc
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml
    /// </summary>
    public partial class HistoryView : UserControl
    {
        HistoryToolWindow toolWindow;
        private GitFileStatusTracker tracker;

        public HistoryView(HistoryToolWindow toolWindow)
        {
            InitializeComponent();
            this.toolWindow = toolWindow;
        }

        public void InsertNewEditor(object editor)
        {
            //diffEditorHost.Content = editor;
        }

        DateTime lastTimeRefresh = DateTime.Now.AddDays(-1);

        internal void Refresh(GitFileStatusTracker tracker)
        {
            this.HistoryGraph.Show(tracker);

            this.tracker = tracker;
            if (tracker == null) return;

            double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
            if (delta < 1000) return; //no refresh within 1 second

            this.branchList.ItemsSource = tracker.RepositoryGraph.Refs
                .Where(r => r.Type == RefTypes.Branch)
                .Select(r => r.Name);

            this.tagList.ItemsSource = tracker.RepositoryGraph.Refs
                .Where(r => r.Type == RefTypes.Tag)
                .Select(r => r.Name);

            lastTimeRefresh = DateTime.Now;
        }

        private void OpenFile(string fileName)
        {
            this.toolWindow.SetDisplayedFile(fileName);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".xps";
            dlg.Filter = "XPS documents (.xps)|*.xps";
            if (dlg.ShowDialog() == true)
            {
                this.HistoryGraph.SaveToFile(dlg.FileName);
            }
        }

        private void checkBox1_Click(object sender, RoutedEventArgs e)
        {
            this.HistoryGraph.SetSimplified(checkBox1.IsChecked==true);
        }

    }
}
