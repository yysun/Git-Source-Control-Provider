using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GitSharp;
using System.Diagnostics;

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
        
        /// <summary>
        /// This function is for solution explorer. It does not take care of removed and missing files.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public GitFileStatus GetFileStatus(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
                return GitFileStatus.NotControlled;

            fileName = workingFolderUri.MakeRelativeUri(new Uri(fileName)).ToString();
            fileName = fileName.Replace("%20", " ");

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
            if (!string.IsNullOrEmpty(workingFolder)) Open(this.workingFolder);
            }
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

        public IEnumerable<GitFile> ChangedFiles
        {
            get
            {
                if (!HasGitRepository) yield break; 

                foreach (var fa in this.repositoryStatus.Added)
                {
                    yield return new GitFile { Status = GitFileStatus.Added, FileName = fa, IsStaged = true };
                }
                foreach (var fe in this.repositoryStatus.Modified)
                {
                    yield return new GitFile { Status = GitFileStatus.Modified, FileName = fe };
                }
                foreach (var fm in this.repositoryStatus.Missing)
                {
                    yield return new GitFile { Status = GitFileStatus.Deleted, FileName = fm };
                }
                foreach (var fd in this.repositoryStatus.Removed)
                {
                    yield return new GitFile { Status = GitFileStatus.Deleted, FileName = fd, IsStaged = true };
                }
                foreach (var fs in this.repositoryStatus.Staged)
                {
                    yield return new GitFile { Status = GitFileStatus.Staged, FileName = fs, IsStaged = true };
                }
                foreach (var fu in this.repositoryStatus.Untracked)
                {
                    yield return new GitFile { Status = GitFileStatus.UnTrackered, FileName = fu };
                }
            }
        }

        internal void Init()
        {
            if (!this.HasGitRepository)
            {
                Repository.Init(this.workingFolder);
            }
        }

        internal void StageFile(string fileName)
        {
            if (!this.HasGitRepository) return;
            if (this.repositoryStatus.Missing.Has(fileName))
            {
                this.repositoryStatus.Repository.Index.Remove(fileName);
            }
            else
            {
                this.repositoryStatus.Repository.Index.Stage(fileName);
            }
        }

        internal void UnStageFile(string fileName)
        {
            if (!this.HasGitRepository) return;

            if (this.repositoryStatus.Removed.Has(fileName))
            {
                var content = ((Leaf)this.repositoryStatus.Repository.Head.CurrentCommit.Tree[fileName]).RawData;
                var filepath = Encoding.UTF8.GetBytes(fileName);
                this.repositoryStatus.Repository.Index.AddContent(filepath, content);
            }
            else
            {
                this.repositoryStatus.Repository.Index.Unstage(fileName);
            }
        }

        internal void Commit(string message)
        {
            if (!this.HasGitRepository) return;
            this.repositoryStatus.Repository.Commit(message);
        }

        internal string DiffFile(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return null;

            //GitSharp has not implemented diff yet

            if (this.repositoryStatus.Removed.Has(fileName) ||
                this.repositoryStatus.Added.Has(fileName) ||
                this.repositoryStatus.Staged.Has(fileName))
            {
                return RunCommand(string.Format("diff --cached --unified=3 -- \"{0}\"", fileName));
            }
            else if (this.repositoryStatus.Missing.Has(fileName) ||
                 this.repositoryStatus.Modified.Has(fileName))
            {
                return RunCommand(string.Format("diff --unified=3 -- \"{0}\"", fileName));
            }
            else
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(this.workingFolder, fileName));

                return Diff.IsBinary(bytes) == true ? "[Binary Content]" :
                    Encoding.UTF8.GetString(bytes); //TODO: assuming UTF8?
            }
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
                WorkingDirectory = this.workingFolder
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

    public static class HashSetExt
    {
        public static bool Has(this HashSet<string> hashSet, string value)
        {
            return hashSet.Any(s => string.Compare(s, value, true) == 0);
        }
    }
}
