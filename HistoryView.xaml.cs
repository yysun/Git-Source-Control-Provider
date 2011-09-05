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
        }

        public void InsertNewEditor(object editor)
        {
            diffEditorHost.Content = editor;
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
            //if (delta < 1000) return; //no refresh within 1 second

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action act = () =>
            {
                //var rw = new RevWalk(tracker.Repository);
                //rw.Sort(RevSort.COMMIT_TIME_DESC);
                //var head = rw.ParseCommit(tracker.Repository.Resolve("HEAD"));
                //rw.MarkStart(head);

                //CommitListBox.ItemsSource = rw.Select(r=>new {
                //    Message = r.GetShortMessage().Replace("\r", ""),
                //    Id = r.Id.Name.Substring(0, 5),
                //    Author = r.GetAuthorIdent().GetName(),
                //    //CommitTime = r.
                //}).ToArray();


                PlotWalk pw = new PlotWalk(tracker.Repository);
                pw.MarkStart(pw.LookupCommit(tracker.Repository.Resolve("HEAD")));
                PlotCommitList<PlotLane> pcl = new PlotCommitList<PlotLane>();
                pcl.Source(pw);
                pcl.FillTo(int.MaxValue);

                var commits = pcl.ToArray();

                CommitListBox.ItemsSource = commits.Select(r => new
                {
                    Message = r.GetShortMessage().Replace("\r", ""),
                    Id = r.GetLane().GetPosition(),
                    Author = r.GetChildCount(),
                    //Author = r.GetAuthorIdent().GetName(),
                    //CommitTime = r.
                }).ToArray();
            };

            dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);

            lastTimeRefresh = DateTime.Now;
        }

        private void OpenFile(string fileName)
        {
            this.toolWindow.SetDisplayedFile(fileName);
        }        
    }
}
