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
        //private FileSystemWatcher watcher;
        //private IgnoreRules ignoreRules;

        public GitFileStatusTracker()
        {

        }

        public void Open(string workingFolder)
        {
            Close();

            if (!string.IsNullOrEmpty(workingFolder))
            {
                try
                {
                    var repo = new Repository(workingFolder);        
                    this.repositoryStatus = repo.Status;
                    this.workingFolderUri = new Uri(repo.WorkingDirectory+"\\");
                    //this.ignoreRules = new IgnoreRules(Path.Combine(repo.WorkingDirectory, GitSharp.Core.Constants.GITIGNORE_FILENAME));

                    //this.watcher = new FileSystemWatcher(workingFolder + "\\" + GitSharp.Core.Constants.DOT_GIT); //?
                    ////this.watcher = new FileSystemWatcher(workingFolder); //?

                    //this.watcher.NotifyFilter = NotifyFilters.LastWrite;
                    //this.watcher.EnableRaisingEvents = true;
                    //this.watcher.Changed += new FileSystemEventHandler(watcher_Changed);
                }
                catch
                {
                }
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
                if (delta < 500) return;

                //if (IsIgnoredFile(e.FullPath)) return;

                this.repositoryStatus.Update();
                if (OnGitRepoChanged != null) OnGitRepoChanged(this, EventArgs.Empty);

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
            this.repositoryStatus = null;
            this.workingFolderUri = null;
            //this.ignoreRules = null;

            //if (this.watcher != null)
            //{
            //    this.watcher.Changed -= new FileSystemEventHandler(watcher_Changed);
            //    this.watcher.Dispose();
            //}

        }

        //private bool IsIgnoredFile(string fileName)
        //{
        //    try
        //    {
        //        if (Directory.Exists(fileName))
        //        {
        //            return this.ignoreRules.IgnoreDir(this.repositoryStatus.Repository.WorkingDirectory, fileName);
        //        }
        //        else if (File.Exists(fileName))
        //        {
        //            return this.ignoreRules.IgnoreDir(this.repositoryStatus.Repository.WorkingDirectory,
        //                                              Path.GetDirectoryName(fileName)) ||
        //                   this.ignoreRules.IgnoreFile(this.repositoryStatus.Repository.WorkingDirectory, fileName);
        //        }
        //        else
        //            return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        public GitFileStatus GetFileStatus(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
                return GitFileStatus.NotControlled;

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
