using System;
using System.Collections.Generic;
using System.IO;
using GitSharp.Core;
using System.Diagnostics;
using System.Timers;

namespace GitScc
{
    public class GitFileStatusTracker
    {
        private string initFolder;

        private Repository repository;
        private Tree commitTree;
        private GitIndex index;
        private IgnoreHandler ignoreHandler;

        private Dictionary<string, GitFileStatus> cache;

        public GitFileStatusTracker(string workingFolder)
        {
            this.initFolder = workingFolder;
            cache = new Dictionary<string, GitFileStatus>();

            Refresh();
        }

        public void Refresh()
        {

            Close();

            if (!string.IsNullOrEmpty(initFolder))
            {
                try
                {
                    this.repository = Repository.Open(initFolder);

                    if (this.repository != null)
                    {
                        var id = repository.Resolve("HEAD");
                        var commit = repository.MapCommit(id);
                        this.commitTree = (commit != null ? commit.TreeEntry : new Tree(repository));
                        this.index = repository.Index;
                        this.index.Read();
                        this.ignoreHandler = new IgnoreHandler(repository);
                        //this.watcher = new FileSystemWatcher(this.repository.WorkingDirectory.FullName);
                    }
                }
                catch
                {
                }
            }
        }

        public void Close()
        {
            cache.Clear();

            if (this.repository != null) this.repository.Close();
            this.repository = null;

        }

        public string GitWorkingDirectory
        {
            get
            {
                return this.repository == null ? null :
                    this.repository.WorkingDirectory.FullName;
            }
        }

        public bool HasGitRepository
        {
            get { return this.repository != null; }
        }


        public GitFileStatus GetFileStatus(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return GitFileStatus.NotControlled;

            if (!cache.ContainsKey(fileName))
            {
                var status = GetFileStatusNoCache(fileName);
                cache.Add(fileName, status);
                //Debug.WriteLine(string.Format("GetFileStatus {0} - {1}", fileName, status));
                return status;
            }
            else
            {
                return cache[fileName];
            }
        }

        private GitFileStatus GetFileStatusNoCache(string fileName)
        {
            var fileNameRel = GetRelativeFileName(fileName);

            TreeEntry treeEntry = this.commitTree.FindBlobMember(fileNameRel);
            GitIndex.Entry indexEntry = this.index.GetEntry(fileNameRel);
            
            //the order of 'if' below is important
            if (indexEntry != null)
            {
                if (treeEntry == null)
                {
                    return GitFileStatus.Added;
                }
                if (treeEntry != null && !treeEntry.Id.Equals(indexEntry.ObjectId))
                {
                    return GitFileStatus.Staged;
                }            
                if (!File.Exists(fileName))
                {
                    return GitFileStatus.Missing;
                }
                if (File.Exists(fileName) && indexEntry.IsModified(repository.WorkingDirectory, true))
                {
                    return GitFileStatus.Modified;
                }
                if (indexEntry.Stage != 0)
                {
                    return GitFileStatus.MergeConflict;
                }
                if (treeEntry != null && treeEntry.Id.Equals(indexEntry.ObjectId))
                {
                    return GitFileStatus.Trackered;
                }
            }
            else // <-- index entry == null
            {
                if (treeEntry != null && !(treeEntry is Tree))
                {
                    return GitFileStatus.Removed;
                }
                if (File.Exists(fileName))
                {
                    if (this.ignoreHandler.IsIgnored(fileName))
                    {
                        return GitFileStatus.Ignored;
                    }

                    return GitFileStatus.New;
                }
            }
            
            return GitFileStatus.NotControlled;
        }

        private string GetRelativeFileName(string fileName)
        {
            Uri workingFolderUri = new Uri(repository.WorkingDirectory.FullName + "\\");
            fileName = workingFolderUri.MakeRelativeUri(new Uri(fileName)).ToString();
            fileName = fileName.Replace("%20", " ");
            return fileName;
        }

        public byte[] GetFileContent(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return null;

            fileName = GetRelativeFileName(fileName);

            var entry = commitTree.FindBlobMember(fileName);
            if(entry!=null)
            {
                var blob = repository.OpenBlob(entry.Id);
                if(blob!=null) return blob.CachedBytes;
            }        
            return null ;
        }

        public string CurrentBranch
        {
            get
            {
                return this.HasGitRepository ? this.repository.getBranch() : "";
            }
        }
    }
}
