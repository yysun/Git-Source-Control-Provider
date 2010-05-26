using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GitSharp;

namespace GitScc
{
    public class GitFileStatusTracker
    {
        private RepositoryStatus repositoryStatus;
        private Uri workingFolderUri;
        FileSystemWatcher watcher;

        public GitFileStatusTracker()
        {

        }
   
        public void Open(string workingFolder)
        {
            Close();

            if (!string.IsNullOrEmpty(workingFolder))
            {
                var repo = new Repository(workingFolder);
                if (Repository.IsValid(repo.WorkingDirectory))
                {
                    this.repositoryStatus = repo.Status;
                    this.workingFolderUri = new Uri(repo.WorkingDirectory+"\\");

                    this.watcher = new FileSystemWatcher(repo.WorkingDirectory + "\\.git"); //?
                    this.watcher.NotifyFilter = NotifyFilters.LastWrite;
                    this.watcher.EnableRaisingEvents = true;
                    this.watcher.Changed += new FileSystemEventHandler(watcher_Changed);
                }
            }
            else
            {
                this.repositoryStatus = null;
                this.workingFolderUri = null;
                this.watcher.Changed -= new FileSystemEventHandler(watcher_Changed);
                this.watcher = null;
            }
        }

        private DateTime TimeFired; 
        public event EventHandler OnGitRepoChanged;
        private object locker = new object();

        void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            lock (locker)
            {
                double delta = DateTime.Now.Subtract(TimeFired).TotalMilliseconds;
                if (OnGitRepoChanged == null || delta < 500) return;
                
                this.repositoryStatus.Update();
                OnGitRepoChanged(this, EventArgs.Empty);

                TimeFired = DateTime.Now;
            }
        }

        public bool HasGitRepository
        {
            get { return this.repositoryStatus != null; }
        }

        public void Close()
        {
            if (this.repositoryStatus != null) this.repositoryStatus.Repository.Close();
            
            if (this.watcher != null)
            {
                this.watcher.Changed -= new FileSystemEventHandler(watcher_Changed);
                this.watcher.Dispose();
            }
        }

        public GitFileStatus GetFileStatus(string fileName)
        {
            if (!HasGitRepository || !File.Exists(fileName)) return GitFileStatus.NotControlled;
            
            fileName = workingFolderUri.MakeRelativeUri(new Uri(fileName)).ToString();

            if (this.repositoryStatus.Untracked.Has(fileName))
            {
                return GitFileStatus.UnTrackered;
            }
            else if (this.repositoryStatus.Modified.Has(fileName))
            {
                return GitFileStatus.Modified;
            }
            else if (this.repositoryStatus.Added.Has(fileName))
            {
                return GitFileStatus.Staged;
            }
            else if (this.repositoryStatus.Staged.Has(fileName))
            {
                return GitFileStatus.Staged;
            }
            else
            {
                return GitFileStatus.Trackered;
            }
        }
    }

    public static class HashSetExt
    {
        public static bool Has(this HashSet<string> hashSet, string value)
        {
            return hashSet.Any(s => string.Compare(s, value, true) == 0);
        }
    }
}
