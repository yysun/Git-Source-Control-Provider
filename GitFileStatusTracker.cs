using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NGit;
using NGit.Api;
using NGit.Dircache;
using NGit.Revwalk;
using NGit.Storage.File;
using NGit.Treewalk;
using NGit.Treewalk.Filter;

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
                    this.repository = Git.Open(initFolder).GetRepository();

                    if (this.repository != null)
                    {
                        var id = repository.Resolve(Constants.HEAD);
                        //var commit = repository.MapCommit(id);
                        //this.commitTree = (commit != null ? commit.TreeEntry : new Tree(repository));
                        if (id == null)
                        {
                            this.commitTree = new Tree(repository);
                        }
                        else
                        {
                            var treeId = ObjectId.FromString(repository.Open(id).GetBytes(), 5);
                            this.commitTree = new Tree(repository, treeId, repository.Open(treeId).GetBytes());
                        }
                        this.index = repository.GetIndex();
                        //this.index.RereadIfNecessary();
                        //this.ignoreHandler = new IgnoreHandler(repository);
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
                    this.repository.WorkTree;
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
                if (treeEntry != null && !treeEntry.GetId().Equals(indexEntry.GetObjectId()))
                {
                    return GitFileStatus.Staged;
                }
                if (!File.Exists(fileName))
                {
                    return GitFileStatus.Missing;
                }
                if (File.Exists(fileName) && indexEntry.IsModified(repository.WorkTree, true))
                {
                    return GitFileStatus.Modified;
                }
                if (indexEntry.GetStage() != 0)
                {
                    return GitFileStatus.MergeConflict;
                }
                if (treeEntry != null && treeEntry.GetId().Equals(indexEntry.GetObjectId()))
                {
                    return GitFileStatus.Tracked;
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
            //Uri workingFolderUri = new Uri(repository.WorkingDirectory.FullName + "\\");
            //fileName = workingFolderUri.MakeRelativeUri(new Uri(fileName)).ToString();
            //fileName = fileName.Replace("%20", " ");
            //return fileName;

            return GetRelativePath(repository.WorkTree, fileName);
        }

        /// <summary>
        /// Computes relative path, where path is relative to reference_path
        /// </summary>
        /// <param name="reference_path"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetRelativePath(string reference_path, string path)
        {
            if (reference_path == null)
                throw new ArgumentNullException("reference_path");
            if (path == null)
                throw new ArgumentNullException("path");
            //reference_path = reference_path.Replace('/', '\\');
            //path = path.Replace('/', '\\');
            bool isRooted = Path.IsPathRooted(reference_path) && Path.IsPathRooted(path);
            if (isRooted)
            {
                bool isDifferentRoot = string.Compare(Path.GetPathRoot(reference_path), Path.GetPathRoot(path), true) != 0;
                if (isDifferentRoot)
                    return path;
            }
            var relativePath = new StringCollection();
            string[] fromDirectories = Regex.Split(reference_path, @"[/\\]+");
            string[] toDirectories = Regex.Split(path, @"[/\\]+");
            int length = Math.Min(fromDirectories.Length, toDirectories.Length);
            int lastCommonRoot = -1;
            // find common root
            for (int x = 0; x < length; x++)
            {
                if (string.Compare(fromDirectories[x],
                      toDirectories[x], true) != 0)
                    break;
                lastCommonRoot = x;
            }
            if (lastCommonRoot == -1)
                return string.Join(Path.DirectorySeparatorChar.ToString(), toDirectories);
            // add relative folders in from path
            for (int x = lastCommonRoot + 1; x < fromDirectories.Length; x++)
                if (fromDirectories[x].Length > 0)
                    relativePath.Add("..");
            // add to folders to path
            for (int x = lastCommonRoot + 1; x < toDirectories.Length; x++)
                relativePath.Add(toDirectories[x]);
            // create relative path
            string[] relativeParts = new string[relativePath.Count];
            relativePath.CopyTo(relativeParts, 0);
            string newPath = string.Join(Path.DirectorySeparatorChar.ToString(), relativeParts);
            return newPath;
        }

        public byte[] GetFileContent(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return null;

            fileName = GetRelativeFileName(fileName);

            var entry = commitTree.FindBlobMember(fileName);
            if (entry != null)
            {
                var blob = repository.Open(entry.GetId());
                if (blob != null) return blob.GetCachedBytes();
            }

            return null;
        }

        public string CurrentBranch
        {
            get
            {
                return this.HasGitRepository ? this.repository.GetBranch() : "";
            }
        }

        /// <summary>
        /// Search Git Repository in folder and its parent folders 
        /// </summary>
        /// <param name="folder">starting folder</param>
        /// <returns>folder that has .git subfolder</returns>
        public static string GetRepositoryDirectory(string folder)
        {
            if(string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;

            var directory = new DirectoryInfo(folder);

            if (directory.GetDirectories(Constants.DOT_GIT).Length > 0)
            {
                return folder;
            }

            return directory.Parent == null ? null :
                   GetRepositoryDirectory(directory.Parent.FullName);
        }

        public override string ToString()
        {
            return repository == null ? "[no repo]" : this.GitWorkingDirectory;
        }

        public void UnStageFile(string fileName)
        {
            fileName = Path.Combine(initFolder, fileName);

            if (!this.HasGitRepository) return;
            this.index.RereadIfNecessary();

            var content = GetFileContent(fileName);

            this.index.Remove(repository.WorkTree, fileName);

            if (content != null)
            {
                this.index.Add(repository.WorkTree, fileName, content);
            }

            this.index.Write();
            cache.Clear();
        }

        public void StageFile(string fileName)
        {
            fileName = Path.Combine(initFolder, fileName);

            if (!this.HasGitRepository) return;
            this.index.RereadIfNecessary();

            var content = File.ReadAllBytes(fileName);

            this.index.Add(repository.WorkTree, fileName, content);

            this.index.Write();
            cache.Clear();
        }

        public void RemoveFile(string fileName)
        {
            if (!this.HasGitRepository) return;

            this.index.RereadIfNecessary();
            this.index.Remove(repository.WorkTree, fileName);
            this.index.Write();
            cache.Clear();
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

            var git = new Git(this.repository);
            git.Commit().SetMessage(message).Call();
            Refresh();
        }

        public void AmendCommit(string message)
        {
            if (!HasGitRepository) return;

            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Commit message must not be null or empty!", "message");

            var git = new Git(this.repository);
            git.Commit().SetAmend(true).SetMessage(message).Call();
            Refresh();
        }

        public static void Init(string folderName)
        {
            var gitFolder = Path.Combine(folderName, Constants.DOT_GIT);
            var repo = new FileRepository(gitFolder);
            repo.Create();
        }

        public IEnumerable<GitFile> ChangedFiles
        {
            get
            {
                FillCache();
                return from f in cache
                       where f.Value != GitFileStatus.Tracked &&
                             f.Value != GitFileStatus.NotControlled
                       select new GitFile
                       {
                           FileName = GetRelativeFileName(f.Key),
                           Status = f.Value,
                           IsStaged = f.Value == GitFileStatus.Added ||
                                      f.Value == GitFileStatus.Staged ||
                                      f.Value == GitFileStatus.Removed
                       };
            }
        }

        private const int INDEX = 1;
        private const int WORKDIR = 2;

        public void FillCache()
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();

            var treeWalk = new TreeWalk(this.repository);
            treeWalk.Recursive = true;
            treeWalk.Filter = TreeFilter.ANY_DIFF;

            var id = repository.Resolve(Constants.HEAD);
            if (id != null)
            {
                treeWalk.AddTree(ObjectId.FromString(repository.Open(id).GetBytes(), 5)); //any better way?
            }
            else
            {
                treeWalk.AddTree(new EmptyTreeIterator());
            }

            treeWalk.AddTree(new DirCacheIterator(this.repository.ReadDirCache()));
            treeWalk.AddTree(new FileTreeIterator(this.repository));
            var filters = new TreeFilter[] { new SkipWorkTreeFilter(INDEX), new IndexDiffFilter(INDEX, WORKDIR) };
            treeWalk.Filter = AndTreeFilter.Create(filters);

            while (treeWalk.Next())
            {
                var fileName = GetFullPath(treeWalk.PathString);
                //    Debug.WriteLine(string.Format("==== Fill cache for {0}", fileName));
                var status = GetFileStatusNoCache(fileName);
                cache[fileName] = status;
            }

            //sw.Stop();
            //Debug.WriteLine("+++++++++++" + sw.ElapsedMilliseconds);

            //sw.Start();

            //var workingTreeIt = new FileTreeIterator(this.repository);
            //IndexDiff diff = new IndexDiff(this.repository, Constants.HEAD, workingTreeIt);
            //diff.Diff();
            //diff.GetChanged().ToList().ForEach(f => cache[GetFullPath(f)] = GitFileStatus.Staged);
            //diff.GetModified().ToList().ForEach(f => cache[GetFullPath(f)] = GitFileStatus.Modified);
            //diff.GetAdded().ToList().ForEach(f => cache[GetFullPath(f)] = GitFileStatus.Added);
            //diff.GetUntracked().ToList().ForEach(f => cache[GetFullPath(f)] = GitFileStatus.New);
            
            //sw.Stop();
            //Debug.WriteLine("+++++++++++" + sw.ElapsedMilliseconds);
        }

        private string GetFullPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return this.GitWorkingDirectory;

            return Path.Combine(this.GitWorkingDirectory, fileName.Replace("/", "\\"));
        }


        public string LastCommitMessage
        {
            get
            {
                if (!HasGitRepository) return null;

                ObjectId headId = this.repository.Resolve(Constants.HEAD);
                var revWalk = new RevWalk(this.repository);
                revWalk.MarkStart(revWalk.LookupCommit(headId));
                foreach (RevCommit c in revWalk)
                {
                    return c.GetFullMessage();
                }
                return "";

            }
        }
    }
}
