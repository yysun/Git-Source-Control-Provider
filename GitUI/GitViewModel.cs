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
		public static readonly RoutedUICommand CompareCommits = new RoutedUICommand("CompareCommits", "CompareCommits", typeof(MainWindow));
		public static readonly RoutedUICommand ExportGraph = new RoutedUICommand("ExportGraph", "ExportGraph", typeof(MainWindow));
		public static readonly RoutedUICommand RefreshGraph = new RoutedUICommand("RefreshGraph", "RefreshGraph", typeof(MainWindow));
		public static readonly RoutedUICommand ScrollToCommit = new RoutedUICommand("ScrollToCommit", "ScrollToCommit", typeof(MainWindow));
		public static readonly RoutedUICommand GraphLoaded = new RoutedUICommand("GraphLoaded", "GraphLoaded", typeof(MainWindow));
		public static readonly RoutedUICommand PendingChanges = new RoutedUICommand("PendingChanges", "PendingChanges", typeof(MainWindow));
		public static readonly RoutedUICommand ShowMessage = new RoutedUICommand("ShowMessage", "ShowMessage", typeof(MainWindow));
	}

	public class GitViewModel
	{
		#region singleton
		private static GitViewModel current;
		public static GitViewModel Current
		{
			get
			{
				if (current == null)
				{
					var args = Environment.GetCommandLineArgs();
					current = new GitViewModel();

					var directory = args.Length > 1 ? args[1] :
						Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
					current.Open(directory);
				}

				return current;
			}
		}

		#endregion

		public event EventHandler GraphChanged = delegate { };
		private GitFileStatusTracker tracker;
		private string workingDirectory;
		private bool showSimplifiedView;

		public GitFileStatusTracker Tracker { get { return tracker; } }
		public string WorkingDirectory { get { return workingDirectory; } }
		public bool ShowSimplifiedView
		{
			get { return showSimplifiedView; }
			set
			{
				showSimplifiedView = value;
				Refresh(true);
			}
		}

		DispatcherTimer timer;
		FileSystemWatcher fileSystemWatcher;

		private GitViewModel()
		{
			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(100);
			timer.Tick += new EventHandler(timer_Tick);
			timer.Start();
		}

		internal void Open(string directory)
		{
			workingDirectory = directory;

			tracker = new GitFileStatusTracker(directory);
			if (tracker.HasGitRepository) directory = tracker.GitWorkingDirectory;

			if (Directory.Exists(directory))
			{
				fileSystemWatcher = new FileSystemWatcher(directory);
				fileSystemWatcher.IncludeSubdirectories = true;
				//fileSystemWatcher.Created += new FileSystemEventHandler(fileSystemWatcher_Changed);
				fileSystemWatcher.Deleted += new FileSystemEventHandler(fileSystemWatcher_Changed);
				//fileSystemWatcher.Renamed += new FileSystemEventHandler(fileSystemWatcher_Changed);
				fileSystemWatcher.Changed += new FileSystemEventHandler(fileSystemWatcher_Changed);
				fileSystemWatcher.EnableRaisingEvents = true;
			}
		}

		internal static void OpenGitBash()
		{
			GitBash.OpenGitBash(Current.WorkingDirectory);
		}

		#region Refresh

		internal DateTime lastTimeRefresh = DateTime.Now.AddDays(-1);
		internal DateTime nextTimeRefresh = DateTime.Now.AddDays(-1);

		private void fileSystemWatcher_Changed(object source, FileSystemEventArgs e)
		{
			if (!NoRefresh)
			{
				double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
				if (delta > 500)
				{
					NeedRefresh = true;
					lastTimeRefresh = DateTime.Now;
					nextTimeRefresh = DateTime.Now;
				}
			}
		}

		internal bool NoRefresh;
		private bool NeedRefresh;

		private void timer_Tick(Object sender, EventArgs args)
		{
			if (NeedRefresh && !NoRefresh)
			{
				double delta = DateTime.Now.Subtract(nextTimeRefresh).TotalMilliseconds;
				if (delta > 200)
				{
					System.Diagnostics.Debug.WriteLine("$$$$ Refresh");
					DisableAutoRefresh();
					Refresh(false);
					NoRefresh = false;
					NeedRefresh = false;
					nextTimeRefresh = DateTime.Now;
				}
			}
		}

		internal void Refresh(bool reload)
		{
			tracker.Refresh();
			if (tracker.HasGitRepository)
				tracker.RepositoryGraph.IsSimplified = showSimplifiedView;
			GraphChanged(this, reload ? new EventArgs() : null); // use non-null to force reload
		}

		internal void EnableAutoRefresh()
		{
			timer.Start();
			NoRefresh = false;
			NeedRefresh = false;
			lastTimeRefresh = DateTime.Now;
		}

		internal void DisableAutoRefresh()
		{
			timer.Stop();
			NoRefresh = true;
			NeedRefresh = false;
			lastTimeRefresh = DateTime.Now.AddSeconds(2);
		}

		#endregion

		#region Git commands

		internal GitUI.UI.GitConsole console = null;

		private string GitRun(string cmd)
		{
			if (!GitBash.Exists) throw new Exception("git.exe is not found.");
			if (this.Tracker == null) throw new Exception("Git repository is not found.");

			if (console != null)
			{
				console.Run("git " + cmd);
				return "";
			}
			else
			{
				var result = GitBash.Run(cmd, this.Tracker.GitWorkingDirectory);
				return result.Output;
			}
		}

		private void GitRunCmd(string cmd)
		{
			if (!GitBash.Exists) throw new Exception("git.exe is not found.");
			if (this.Tracker == null) throw new Exception("Git repository is not found.");
			if (console != null)
			{
				console.Run("git " + cmd);
			}
			else
			{
				GitBash.RunCmd(cmd, this.Tracker.GitWorkingDirectory);
			}
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

		internal string Archive(string id, string fileName)
		{
			return GitRun(string.Format("archive {0} --format=zip --output \"{1}\"", id, fileName));
		}

		internal void Patch(string id1, string fileName)
		{
			GitRunCmd(string.Format("format-patch {0} -1 --stdout > \"{1}\"", id1, fileName));
		}

		internal void Patch(string id1, string id2, string fileName)
		{
			GitRunCmd(string.Format("format-patch {0}..{1} -o \"{2}\"", id1, id2, fileName));
		}

		#endregion    
	
	}
}
