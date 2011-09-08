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
            this.branchList.ItemsSource = new string[] { "master", "develop", "test"};
        }

        public void InsertNewEditor(object editor)
        {
            //diffEditorHost.Content = editor;
        }

        DateTime lastTimeRefresh = DateTime.Now.AddDays(-1);

        internal void Refresh(GitFileStatusTracker tracker)
        {
            this.tracker = tracker;
            if (tracker == null)
            {
                //clear all UI
                return;
            }
            
            double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
            if (delta < 1000) return; //no refresh within 1 second

            this.HistoryGraph.Show(tracker.Repository);

            lastTimeRefresh = DateTime.Now;
        }

        private void OpenFile(string fileName)
        {
            this.toolWindow.SetDisplayedFile(fileName);
        }

    }
}
