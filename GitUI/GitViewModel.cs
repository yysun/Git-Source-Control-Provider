using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using GitScc;

namespace GitUI
{
    public static class HistoryViewCommands
    {
        //public static readonly RoutedUICommand CloseCommitDetails = new RoutedUICommand("CloseCommitDetails", "CloseCommitDetails", typeof(MainWindow));
        //public static readonly RoutedUICommand OpenCommitDetails = new RoutedUICommand("OpenCommitDetails", "OpenCommitDetails", typeof(MainWindow));
        //public static readonly RoutedUICommand SelectCommit = new RoutedUICommand("SelectCommit", "SelectCommit", typeof(MainWindow));
        public static readonly RoutedUICommand SimplifiedView = new RoutedUICommand("SimplifiedView", "SimplifiedView", typeof(GitViewModel));
        public static readonly RoutedUICommand ExportGraph = new RoutedUICommand("ExportGraph", "ExportGraph", typeof(GitScc.UI.HistoryGraph));
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
        //private FileSystemWatcher fileSystemWatcher;
        private GitFileStatusTracker tracker;
       
        public GitFileStatusTracker Tacker { get { return tracker; } }

        private GitViewModel()
        {
            var args = Environment.GetCommandLineArgs();
            var workingDirectory = args.Length > 1 ? args[1] :
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
        }

        private DateTime lastTimeRefresh = DateTime.Now;

        internal void Refresh()
        {
            double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
            if (delta > 1000)
            {
                //System.Diagnostics.Debug.WriteLine("==== GitViewModel Refresh {0}", delta);
                tracker.Refresh();
                GraphChanged(this, null);
                lastTimeRefresh = DateTime.Now;
            }
        }

        internal void RefreshGraph()
        {
            GraphChanged(this, null);
        }
    }
}
