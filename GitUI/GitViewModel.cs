using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using GitScc;
using System.Windows.Threading;

namespace GitUI
{
    public static class HistoryViewCommands
    {
        public static readonly RoutedUICommand CloseCommitDetails = new RoutedUICommand("CloseCommitDetails", "CloseCommitDetails", typeof(MainWindow));
        public static readonly RoutedUICommand OpenCommitDetails = new RoutedUICommand("OpenCommitDetails", "OpenCommitDetails", typeof(MainWindow));
        public static readonly RoutedUICommand SelectCommit = new RoutedUICommand("SelectCommit", "SelectCommit", typeof(MainWindow));
        public static readonly RoutedUICommand ExportGraph = new RoutedUICommand("ExportGraph", "ExportGraph", typeof(MainWindow));
        public static readonly RoutedUICommand RefreshGraph = new RoutedUICommand("RefreshGraph", "RefreshGraph", typeof(MainWindow));
        public static readonly RoutedUICommand ScrollToCommit = new RoutedUICommand("ScrollToCommit", "ScrollToCommit", typeof(MainWindow));
        public static readonly RoutedUICommand GraphLoaded = new RoutedUICommand("GraphLoaded", "GraphLoaded", typeof(MainWindow));
    }

    public class GitViewModel
    {
        #region singleton
        private static GitViewModel current;
        public static GitViewModel Current
        {
            get
            {
                if (current == null) current = new GitViewModel();
                return current;
            }
        } 
        #endregion

        public event EventHandler GraphChanged = delegate { };
        private GitFileStatusTracker tracker;
        private string workingDirectory;

        public GitFileStatusTracker Tacker { get { return tracker; } }
        public string WorkingDirectory { get { return workingDirectory; } }

        DispatcherTimer timer;

        private GitViewModel()
        {
            var args = Environment.GetCommandLineArgs();
            workingDirectory = args.Length > 1 ? args[1] :
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            tracker = new GitFileStatusTracker(workingDirectory);
            if (tracker.HasGitRepository) workingDirectory = tracker.GitWorkingDirectory;
            if (Directory.Exists(workingDirectory))
            {
                FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(workingDirectory);

                fileSystemWatcher.Created += (_, e) => Refresh();
                fileSystemWatcher.Changed += (_, e) => Refresh();
                fileSystemWatcher.Deleted += (_, e) => Refresh();
                fileSystemWatcher.Renamed += (_, e) => Refresh();
                fileSystemWatcher.EnableRaisingEvents = true;
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick +=new EventHandler(timer_Tick);
        }

        private bool NoRefresh;

        private void timer_Tick(Object sender, EventArgs args)
        {
            timer.Stop(); // one time deal

            if (!NoRefresh)
            {
                NoRefresh = true;
                tracker.Refresh();
                GraphChanged(this, null);
                NoRefresh = false;
            }
        }

        internal void Refresh()
        {
            timer.Start();

            //double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
            //if (delta > 100)
            //{
                //System.Diagnostics.Debug.WriteLine("==== GitViewModel Refresh {0}", delta);
                //tracker.Refresh();
            //}
            //lastTimeRefresh = DateTime.Now;
        }

        internal void RefreshGraph()
        {
            GraphChanged(this, null);
        }

        private string GitRun(string cmd)
        {
            if (!GitBash.Exists) throw new Exception("git.exe is not found.");
            if (this.Tacker == null) throw new Exception("Git repository is not found.");

            var ret = GitBash.Run(cmd, this.Tacker.GitWorkingDirectory);
            Refresh();

            return ret;
        }

        internal string AddTag(string name, string id)
        {
            return GitRun(string.Format("tag \"{0}\" {1}", name, id));
        }

        internal string GetTagId(string name)
        {
            return GitRun("show-ref refs/tags/" + name);
        }

        internal string DeleteTag(string name)
        {
            return GitRun("tag -d " + name);
        }

        internal string AddBranch(string name, string id)
        {
            return GitRun(string.Format("branch \"{0}\" {1}", name, id));
        }

        internal string GetBranchId(string name)
        {
            return GitRun("show-ref refs/heads/" + name);
        }

        internal string DeleteBranch(string name)
        {
            return GitRun("branch -d " + name);
        }

        internal string CheckoutBranch(string name)
        {
            return GitRun("checkout " + name);
        }

    }
}
