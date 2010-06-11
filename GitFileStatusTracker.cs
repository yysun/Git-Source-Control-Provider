using System;
using System.Collections.Generic;
using System.IO;
using GitSharp.Core;
using System.Diagnostics;

namespace GitScc
{
    public class GitFileStatusTracker
    {
        private string solutionFolder;

        private Repository repository;
        private Tree commitTree;
        private GitIndex index;
        private Dictionary<string, GitFileStatus> cache;

        public GitFileStatusTracker()
        {
            cache = new Dictionary<string, GitFileStatus>();
        }

        public void Open(string solutionFolder)
        {
            Close();

            this.solutionFolder = solutionFolder;

            if (!string.IsNullOrEmpty(solutionFolder))
            {
                try
                {
                    this.repository = Repository.Open(solutionFolder);
                    var id = repository.Resolve("HEAD");
                    var commit = repository.MapCommit(id);
                    commitTree = (commit != null ? commit.TreeEntry : new Tree(repository));
                    index = repository.Index;
                    index.RereadIfNecessary();
                }
                catch
                {
                }
            }
        }

        public string GitWorkingDirectory
        {
            get
            {
                return this.repository != null ?
                this.repository.WorkingDirectory.FullName : null;
            }
        }
        public bool HasGitRepository
        {
            get { return this.repository != null; }
        }

        public void Close()
        {
            cache.Clear();

            if (this.repository != null) this.repository.Close();
            this.repository = null;
        }

        public GitFileStatus GetFileStatus(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return GitFileStatus.NotControlled;

            if (!cache.ContainsKey(fileName))
            {
                var status = GetFileStatusNoCache(fileName);
                cache.Add(fileName, status);

                Debug.WriteLine(string.Format("GetFileStatus {0} - {1}", fileName, status));
            }
            return cache[fileName];
        }

        private GitFileStatus GetFileStatusNoCache(string fileName)
        {
           

            var fileNameRel = GetRelativeFileName(fileName);

            TreeEntry treeEntry = commitTree.FindBlobMember(fileNameRel);
            GitIndex.Entry indexEntry = index.GetEntry(fileNameRel);
            
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
                    return GitFileStatus.UnTrackered;
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

        public void Update()
        {
            if (!string.IsNullOrEmpty(solutionFolder)) Open(this.solutionFolder);
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

        public IEnumerable<GitFile> ChangedFiles
        {
            get
            {
                if (!HasGitRepository) yield break;

                //foreach (var fa in this.repositoryStatus.Added)
                //{
                //    yield return new GitFile { Status = GitFileStatus.Added, FileName = fa, IsStaged = true };
                //}
                //foreach (var fe in this.repositoryStatus.Modified)
                //{
                //    yield return new GitFile { Status = GitFileStatus.Modified, FileName = fe };
                //}
                //foreach (var fm in this.repositoryStatus.Missing)
                //{
                //    yield return new GitFile { Status = GitFileStatus.Deleted, FileName = fm };
                //}
                //foreach (var fd in this.repositoryStatus.Removed)
                //{
                //    yield return new GitFile { Status = GitFileStatus.Deleted, FileName = fd, IsStaged = true };
                //}
                //foreach (var fs in this.repositoryStatus.Staged)
                //{
                //    yield return new GitFile { Status = GitFileStatus.Staged, FileName = fs, IsStaged = true };
                //}
                //foreach (var fu in this.repositoryStatus.Untracked)
                //{
                //    yield return new GitFile { Status = GitFileStatus.UnTrackered, FileName = fu };
                //}
            }
        }

        internal void Init()
        {
            if (!this.HasGitRepository)
            {
                this.repository = new Repository(new DirectoryInfo(this.solutionFolder));
                this.repository.Create(false);
            }
        }

        internal void StageFile(string fileName)
        {
            //if (!this.HasGitRepository) return;
            //if (this.repositoryStatus.Missing.Has(fileName))
            //{
            //    this.repositoryStatus.Repository.Index.Remove(fileName);
            //}
            //else
            //{
            //    this.repositoryStatus.Repository.Index.Stage(fileName);
            //}
        }

        internal void UnStageFile(string fileName)
        {
            if (!this.HasGitRepository) return;

            //if (this.repositoryStatus.Removed.Has(fileName))
            //{
            //    var content = ((Leaf)this.repositoryStatus.Repository.Head.CurrentCommit.Tree[fileName]).RawData;
            //    var filepath = Encoding.UTF8.GetBytes(fileName);
            //    this.repositoryStatus.Repository.Index.AddContent(filepath, content);
            //}
            //else
            //{
            //    this.repositoryStatus.Repository.Index.Unstage(fileName);
            //}
        }

        internal void Commit(string message)
        {
            if (!this.HasGitRepository) return;
            //this.repositoryStatus.Repository.Commit(message);
        }

        internal string DiffFile(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return null;

            //GitSharp has not implemented diff yet

            //if (this.repositoryStatus.Removed.Has(fileName) ||
            //    this.repositoryStatus.Added.Has(fileName) ||
            //    this.repositoryStatus.Staged.Has(fileName))
            //{
            //    return RunCommand(string.Format("diff --cached --unified=3 -- \"{0}\"", fileName));
            //}
            //else if (this.repositoryStatus.Missing.Has(fileName) ||
            //     this.repositoryStatus.Modified.Has(fileName))
            //{
            //    return RunCommand(string.Format("diff --unified=3 -- \"{0}\"", fileName));
            //}
            //else
            //{
            //    byte[] bytes = File.ReadAllBytes(Path.Combine(this.workingFolder, fileName));

            //    return Diff.IsBinary(bytes) == true ? "[Binary Content]" :
            //        Encoding.UTF8.GetString(bytes); //TODO: assuming UTF8?
            //}

            return "";
        }


        private string RunCommand(string args)
        {
            var cmd = Path.Combine(Path.GetDirectoryName(GitSccOptions.Current.GitBashPath),
                                   "git.exe");

            var pinfo = new ProcessStartInfo(cmd)
            {
                Arguments = args,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = this.repository.WorkingDirectory.FullName
            };

            using (var process = Process.Start(pinfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                    throw new Exception(error);

                return output;
            }
        }
    }
}
