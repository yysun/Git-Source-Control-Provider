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
        private string workingFolder;

        public GitFileStatusTracker()
        {

        }

        public void Open(string workingFolder)
        {
            Close();

            this.workingFolder = workingFolder;
            if (!string.IsNullOrEmpty(workingFolder) && Repository.IsValid(workingFolder))
            {
                try
                {
                    var repo = new Repository(workingFolder);        
                    this.repositoryStatus = repo.Status;
                    this.workingFolderUri = new Uri(repo.WorkingDirectory+"\\");
                }
                catch
                {
                }
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
        }

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

        public void Update()
        {
            if (!string.IsNullOrEmpty(workingFolder))
            {
                Open(this.workingFolder);
            }

            if (this.repositoryStatus!=null) 
                this.repositoryStatus.Update();
        }

        public byte[] GetFileContent(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return null;

            fileName = workingFolderUri.MakeRelativeUri(new Uri(fileName)).ToString();

            Leaf leaf = null;

            if (this.repositoryStatus != null &&
                this.repositoryStatus.Repository != null &&
                this.repositoryStatus.Repository.Head != null &&
                this.repositoryStatus.Repository.Head.CurrentCommit != null &&
                this.repositoryStatus.Repository.Head.CurrentCommit.Tree != null)
            {
                leaf = this.repositoryStatus.Repository.Head.CurrentCommit.Tree[fileName] as Leaf;
            }

            return leaf == null ? null : leaf.RawData;
        }

        public string CurrentBranch
        {
            get
            {
                return this.HasGitRepository ? this.repositoryStatus.Repository.CurrentBranch.Name : "";
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
