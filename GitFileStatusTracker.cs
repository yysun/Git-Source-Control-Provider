using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using GitSharp.Core;
using GitSharp.Core.Util;
using GitSharp.Core.Util.JavaHelper;

namespace GitScc
{
    public class GitFileStatusTracker : IDisposable
    {
        private string initFolder;

        private Repository repository;
        private Tree commitTree;
        private GitIndex index;
        //private IgnoreHandler ignoreHandler;

        private Dictionary<string, GitFileStatus> cache;

        public GitFileStatusTracker(string workingFolder)
        {
            cache = new Dictionary<string, GitFileStatus>();
            this.initFolder = workingFolder;
            Refresh();
        }

        public void Refresh()
        {
            cache.Clear();
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
                        this.index.RereadIfNecessary();
                        //this.ignoreHandler = new IgnoreHandler(repository);
                        //this.watcher = new FileSystemWatcher(this.repository.WorkingDirectory.FullName);
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        public void Dispose()
        {
            if (this.repository != null) this.repository.Close();
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
                    //remove the ingore check for better performance
                    //if (this.ignoreHandler.IsIgnored(fileName))
                    //{
                    //    return GitFileStatus.Ignored;
                    //}

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
            if (entry != null)
            {
                var blob = repository.OpenBlob(entry.Id);
                if (blob != null) return blob.CachedBytes;
            }
            return null;
        }

        public string CurrentBranch
        {
            get
            {
                return this.HasGitRepository ? this.repository.getBranch() : "";
            }
        }

        internal static string GetRepositoryDirectory(string folder)
        {
            var repository = (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) ?
                Repository.Open(folder) : null;

            return repository == null ? null : repository.WorkingDirectory.FullName;

        }

        public override string ToString()
        {
            return repository == null ? "[no repo]" : repository.WorkingDirectory.FullName;
        }

        public System.Collections.IEnumerable ChangedFiles { get; set; }

        public void UnStageFile(string fileName)
        {
            if (!this.HasGitRepository) return;
            this.index.RereadIfNecessary();

            var content = GetFileContent(fileName);
            fileName = GetRelativeFileName(fileName);
            if (content == null)
            {
                this.index.remove(
                    new DirectoryInfo(this.repository.WorkingDirectory.FullName),
                    new FileInfo(fileName));
            }
            else
            {
                this.index.add(
                    new DirectoryInfo(this.repository.WorkingDirectory.FullName),
                    new FileInfo(fileName),
                    content);
            }

            this.index.write();
        }

        public void StageFile(string fileName)
        {
            if (!this.HasGitRepository) return;
            this.index.RereadIfNecessary();

            var content = File.ReadAllBytes(fileName);

            this.index.add(
                new DirectoryInfo(this.repository.WorkingDirectory.FullName),
                new FileInfo(fileName),
                content);

            this.index.write();
        }

        public void RemoveFile(string fileName)
        {
            if (!this.HasGitRepository) return;

            this.index.RereadIfNecessary();
            this.index.remove(
                    new DirectoryInfo(this.repository.WorkingDirectory.FullName),
                    new FileInfo(fileName));

            this.index.write();
        }

        public string DiffFile(string fileName)
        {
            if (!this.HasGitRepository) return null;
            throw new NotImplementedException();
        }

        public void Commit(string message)
        {
            if (!this.HasGitRepository) return;
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Commit message must not be null or empty!", "message");

            this.index.RereadIfNecessary();
            var tree_id = this.index.writeTree();

            var parent = repository.MapCommit(repository.Resolve("HEAD"));

            if ((parent == null && this.index.Members.Count == 0) || (parent != null && parent.TreeId == tree_id))
                throw new InvalidOperationException("There are no changes to commit");

            var corecommit = new Commit(this.repository);
            corecommit.ParentIds = parent == null ? new ObjectId[0] : new ObjectId[] { parent.TreeId };
            var time = DateTimeOffset.Now;
            corecommit.Author =
            corecommit.Committer = new PersonIdent(this["user.name"], this["user.email"], time.ToMillisecondsSinceEpoch(), (int)time.Offset.TotalMinutes);
            corecommit.Message = message;
            corecommit.TreeEntry = this.repository.MapTree(tree_id);
            corecommit.Encoding = Charset.forName(this["i18n.commitencoding"] ?? "utf-8");
            corecommit.Save();

			var updateRef = this.repository.UpdateRef("HEAD");
			updateRef.NewObjectId = corecommit.CommitId;
			updateRef.IsForceUpdate = true;
			updateRef.update();

            Refresh();

        }

        public static void Init(string folderName)
        {
            var gitFolder = Path.Combine(folderName, Constants.DOT_GIT);
            var repo = new Repository(new DirectoryInfo(gitFolder));
            repo.Create(false);
        }


        public string this[string key]
        {
            get
            {
                if (!this.HasGitRepository) return null;

                var config = this.repository.Config;
                var token = key.Split('.');

                if (token.Count() == 2)
                {
                    return config.getString(token[0], null, token[1]);
                }

                if (token.Count() == 3)
                {
                    return config.getString(token[0], token[1], token[2]);
                }

                return null;
            }
            set
            {
                if (!this.HasGitRepository) return;

                var config = this.repository.Config;
                var token = key.Split('.');
                if (token.Count() == 2)
                    config.setString(token[0], null, token[1], value);
                else if (token.Count() == 3)
                    config.setString(token[0], token[1], token[2], value);
            }
        }
    }
}
