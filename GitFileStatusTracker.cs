using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GitScc.DataServices;
using NGit;
using NGit.Api;
using NGit.Diff;
using NGit.Dircache;
using NGit.Ignore;
using NGit.Revwalk;
using NGit.Storage.File;
using NGit.Treewalk;
using NGit.Treewalk.Filter;
using System.Diagnostics;

namespace GitScc
{
    public class GitFileStatusTracker : IDisposable
    {
        private string initFolder;

        private Repository repository;
        //private Tree commitTree;
        //private GitIndex index;
        private Dictionary<string, GitFileStatus> cache;
        private IEnumerable<string> changedFiles;

        public GitFileStatusTracker(string workingFolder)
        {
            this.cache = new Dictionary<string, GitFileStatus>();
            this.initFolder = workingFolder;
            Refresh();
        }

        private Repository Open(DirectoryInfo directory)
        {
            var name = directory.FullName;
            if (name.EndsWith(Constants.DOT_GIT_EXT))
            {
                return Git.Open(name).GetRepository();
            }

            var subDirectories = directory.GetDirectories(Constants.DOT_GIT);
            if (subDirectories.Length > 0)
            {
                return Git.Open(subDirectories[0].FullName).GetRepository(); ;
            }

            if (directory.Parent == null) return null;

            return Open(directory.Parent);

        }

        public void Refresh()
        {
            this.cache.Clear();
            this.changedFiles = null;
            this.repositoryGraph = null;

            if (!string.IsNullOrEmpty(initFolder))
            {
                try
                {
                    this.repository = Open(new DirectoryInfo(initFolder));
                }
                catch (Exception ex)
                {
                    Log.WriteLine("Refresh: {0}\r\n{1}", this.initFolder, ex.ToString());
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
                return this.repository == null ? null : this.repository.WorkTree;
            }
        }

        public bool HasGitRepository
        {
            get { return this.repository != null; }
        }

        #region File Status
        public GitFileStatus GetFileStatus(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return GitFileStatus.NotControlled;

            var cacheKey = GetCacheKey(fileName);

            if (!this.cache.ContainsKey(cacheKey))
            {
                var status = GitFileStatus.NotControlled;
                try
                {
                    status = GetFileStatusNoCache(fileName);
                    this.cache[cacheKey] = status;
                    //Debug.WriteLine(string.Format("GetFileStatus {0} - {1}", fileName, status));
                }
                catch (Exception ex)
                {
                    Log.WriteLine("Get File Status: {0}\r\n{1}", fileName, ex.ToString());
                }
                return status;
            }
            else
            {
                return this.cache[cacheKey];
            }
        }

        private GitFileStatus GetFileStatusNoCache(string fileName)
        {
            var fileNameRel = GetRelativeFileNameForGit(fileName);
            var dirCache = repository.ReadDirCache();
            TreeWalk treeWalk = new TreeWalk(this.repository) { Recursive = true };
            var head = repository.Resolve(Constants.HEAD);
            RevTree revTree = head == null ? null : new RevWalk(repository).ParseTree(head);

            if (revTree != null)
            {
                treeWalk.AddTree(revTree);
            }
            else
            {
                treeWalk.AddTree(new EmptyTreeIterator());
            }
            treeWalk.AddTree(new DirCacheIterator(dirCache));
            treeWalk.AddTree(new FileTreeIterator(this.repository));

            var filters = new TreeFilter[] {
                PathFilter.Create(fileNameRel),
                new SkipWorkTreeFilter(INDEX),
                new IndexDiffFilter(INDEX, WORKDIR)
            };
            treeWalk.Filter = AndTreeFilter.Create(filters);
            if (treeWalk.Next())
            {
                AbstractTreeIterator treeIterator = treeWalk.GetTree<AbstractTreeIterator>(TREE);
                DirCacheIterator dirCacheIterator = treeWalk.GetTree<DirCacheIterator>(INDEX);
                WorkingTreeIterator workingTreeIterator = treeWalk.GetTree<WorkingTreeIterator>(WORKDIR);
                if (dirCacheIterator != null)
                {
                    DirCacheEntry dirCacheEntry = dirCacheIterator.GetDirCacheEntry();
                    if (dirCacheEntry != null && dirCacheEntry.Stage > 0)
                    {
                        return GitFileStatus.MergeConflict;
                    }

                    if (workingTreeIterator == null)
                    {
                        // in index, not in workdir => missing
                        return GitFileStatus.Deleted;
                    }
                    else
                    {
                        if (workingTreeIterator.IsModified(dirCacheIterator.GetDirCacheEntry(), true))
                        {
                            // in index, in workdir, content differs => modified
                            return GitFileStatus.Modified;
                        }
                    }
                }
                if (treeIterator != null)
                {
                    if (dirCacheIterator != null)
                    {
                        if (!treeIterator.IdEqual(dirCacheIterator) || treeIterator.EntryRawMode != dirCacheIterator.EntryRawMode)
                        {
                            // in repo, in index, content diff => changed
                            return GitFileStatus.Staged;
                        }
                    }
                    else
                    {
                        return GitFileStatus.Removed;
                    }
                }
                else
                {
                    if (dirCacheIterator != null)
                    {
                        // not in repo, in index => added
                        return GitFileStatus.Added;
                    }
                    else
                    {
                        // not in repo, not in index => untracked
                        if (workingTreeIterator != null)
                        {
                            return !workingTreeIterator.IsEntryIgnored() ? GitFileStatus.New : GitFileStatus.Ignored;
                        }
                    }
                }
            }

            var dirCacheEntry2 = dirCache.GetEntry(fileNameRel);
            if (dirCacheEntry2 != null)
            {
                var treeEntry2 = TreeWalk.ForPath(repository, fileNameRel, revTree);
                if (treeEntry2 != null && treeEntry2.GetObjectId(0).Equals(dirCacheEntry2.GetObjectId()))
                    return GitFileStatus.Tracked;
            }

            return GitFileStatus.NotControlled;
        } 
        #endregion

        #region Full/Relative File Name, Cache Key

        private string GetCacheKey(string fileName)
        {
            return GetRelativeFileName(fileName);
        }

        private string GetFullPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return this.GitWorkingDirectory;

            return Path.Combine(this.GitWorkingDirectory, fileName.Replace("/", "\\"));
        }

        private string GetRelativeFileNameForGit(string fileName)
        {
            return GetRelativePath(repository.WorkTree, fileName).Replace("\\", "/");
        }

        private string GetRelativeFileName(string fileName)
        {
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
        #endregion

        #region File Content
        public byte[] GetFileContent(string fileName)
        {
            if (!HasGitRepository || string.IsNullOrEmpty(fileName))
                return null;

            fileName = GetRelativeFileNameForGit(fileName);

            try
            {
                var head = repository.Resolve(Constants.HEAD);
                RevTree revTree = head == null ? null : new RevWalk(repository).ParseTree(head);

                var entry = TreeWalk.ForPath(repository, fileName, revTree);
                if (!entry.IsSubtree)
                {
                    var blob = repository.Open(entry.GetObjectId(0));
                    if (blob != null) return blob.GetCachedBytes();
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("Get File Content: {0}\r\n{1}", fileName, ex.ToString());
            }

            return null;
        } 
        #endregion

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
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;

            var directory = new DirectoryInfo(folder);

            if (directory.GetDirectories(Constants.DOT_GIT).Length > 0)
            {
                return folder;
            }

            return directory.Parent == null ? null :
                   GetRepositoryDirectory(directory.Parent.FullName);
        }

        #region Git operations: init, stage, unstage(rm --cache), commit/amend commit

        /// <summary>
        /// Requires absolute path
        /// </summary>
        /// <param name="fileName"></param>
        public void UnStageFile(string fileName)
        {
            if (!this.HasGitRepository) return;

            var fileNameRel = GetRelativeFileName(fileName);
            var head = repository.Resolve(Constants.HEAD);

            Tree commitTree = null;
            TreeEntry treeEntry = null;
            if (head == null)
            {
                commitTree = new Tree(repository);
            }
            else
            {
                var revTree = new RevWalk(repository).ParseTree(head);
                var treeId = revTree.Id;
                commitTree = new Tree(repository, treeId, repository.Open(treeId).GetBytes());
                if(commitTree!=null) treeEntry = commitTree.FindBlobMember(fileNameRel);
            }
            var index = repository.GetIndex();
            index.RereadIfNecessary();
            index.Remove(repository.WorkTree, fileName);

            if (treeEntry != null)
            {
                index.AddEntry(treeEntry);
            }

            index.Write();

            this.cache.Remove(GetCacheKey(fileName));
        }

        /// <summary>
        /// Requires absolute path
        /// </summary>
        /// <param name="fileName"></param>
        public void StageFile(string fileName)
        {
            if (!this.HasGitRepository) return;
            var index = repository.GetIndex();
            index.RereadIfNecessary();
            index.Remove(repository.WorkTree, fileName);

            if (File.Exists(fileName))
            {
                var content = File.ReadAllBytes(fileName);
                index.Add(repository.WorkTree, fileName, content);
            }
            else
            {
                //stage deleted
                index.Remove(repository.WorkTree, fileName);
            }
            index.Write();
            this.cache.Remove(GetCacheKey(fileName));
        }

        #region under research ...
        /// <summary>
        /// Requires absolute path
        /// </summary>
        /// <param name="fileName"></param>
        //public void UnStageFile(string fileName)
        //{
        //    if (!this.HasGitRepository) return;
        //    var fileNameRel = GetRelativeFileNameForGit(fileName);

        //    GitBash.Run(string.Format("rm --cache {0}", GetRelativeFileName(fileName)), this.GitWorkingDirectory);

        //    var git = new Git(this.repository);

        //    var head = repository.Resolve(Constants.HEAD);
        //    if (head != null)
        //    {
        //        RevWalk revWalk = new RevWalk(repository);
        //        RevTree headTree = revWalk.ParseTree(head);
        //        if (headTree != null)
        //        {
        //            try
        //            {
        //                var entry = TreeWalk.ForPath(repository, fileName, headTree);
        //                if (!entry.IsSubtree)
        //                {
        //                    DirCacheEntry dce = new DirCacheEntry(entry.PathString);
        //                    var dc = repository.LockDirCache();
        //                    DirCacheBuilder builder = dc.Builder();
        //                    builder.Add(dce);
        //                    builder.Commit();
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.WriteLine("Unstage File Content: {0}\r\n{1}", fileName, ex.ToString());
        //            }
        //        }
        //    }
        //    this.cache.Remove(GetCacheKey(fileName));
        //}

        /// <summary>
        /// Requires absolute path
        /// </summary>
        /// <param name="fileName"></param>
        //public void StageFile(string fileName)
        //{
        //    if (!this.HasGitRepository) return;
        //    var fileNameRel = GetRelativeFileNameForGit(fileName);
        //    var git = new Git(this.repository);
        //    if (File.Exists(fileName))
        //    {
        //        git.Add().AddFilepattern(fileNameRel).Call();
        //    }
        //    else
        //    {
        //        git.Rm().AddFilepattern(fileNameRel).Call();
        //    }

        //    this.cache.Remove(GetCacheKey(fileName));
        //} 
        #endregion

        public string Commit(string message)
        {
            if (!this.HasGitRepository) return null;

            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Commit message must not be null or empty!", "message");

            string msg = "";
            if (GitBash.Exists)
            {
                message = message.Replace("\"", "\\\"");
                msg = GitBash.Run(string.Format("commit -m \"{0}\"", message), this.GitWorkingDirectory);
                if (msg.IndexOf('\n') > 0) msg = msg.Split('\n')[0];
            }
            else
            {
                var git = new Git(this.repository);
                var rev = git.Commit().SetMessage(message).Call();
                msg = rev.Name;
            }
            Refresh();

            return msg;
        }

        public string AmendCommit(string message)
        {
            if (!HasGitRepository) return null;

            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Commit message must not be null or empty!", "message");

            string msg = "";
            if (GitBash.Exists)
            {
                message = message.Replace("\"", "\\\"");
                msg = GitBash.Run(string.Format("commit --amend -m \"{0}\"", message), this.GitWorkingDirectory);
                if (msg.IndexOf('\n') > 0) msg = msg.Split('\n')[0];
            }
            else
            {
                var git = new Git(this.repository);
                var rev = git.Commit().SetAmend(true).SetMessage(message).Call();
                msg = rev.Name;
            }
            Refresh();

            return msg;
        }

        public string LastCommitMessage
        {
            get
            {
                if (!HasGitRepository) return null;
                var headId = this.repository.Resolve(Constants.HEAD);
                if (headId != null)
                {
                    var revWalk = new RevWalk(this.repository);
                    var commit = revWalk.ParseCommit(headId);
                    return commit == null ? null : commit.GetFullMessage();
                }
                return null;
            }
        }

        public static void Init(string folderName)
        {
            if (GitBash.Exists)
            {
                GitBash.Run("init", folderName);
            }
            else
            {
                var gitFolder = Path.Combine(folderName, Constants.DOT_GIT);
                var repo = new FileRepository(gitFolder);
                repo.Create();
                var dir = Directory.CreateDirectory(gitFolder);
                dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }
        } 
        #endregion

        /// <summary>
        /// Diff working file with last commit
        /// </summary>
        /// <param name="fileName">Expect relative path</param>
        /// <returns></returns>
        public string DiffFile(string fileName)
        {
            try
            {
                if (!this.HasGitRepository) return "";

                HistogramDiff hd = new HistogramDiff();
                hd.SetFallbackAlgorithm(null);

                var fullName = GetFullPath(fileName);

                RawText b = new RawText(File.Exists(GetFullPath(fileName)) ?
                                        File.ReadAllBytes(fullName) : new byte[0]);
                RawText a = new RawText(GetFileContent(fileName) ?? new byte[0]);

                var list = hd.Diff(RawTextComparator.DEFAULT, a, b);

                using (Stream mstream = new MemoryStream(),
                              stream = new BufferedStream(mstream))
                {
                    DiffFormatter df = new DiffFormatter(stream);
                    df.Format(list, a, b);
                    df.Flush();
                    stream.Seek(0, SeekOrigin.Begin);
                    var ret = new StreamReader(stream).ReadToEnd();

                    return ret;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("Refresh: {0}\r\n{1}", this.initFolder, ex.ToString());

                return "";
            }
        }

        #region Changed Files
        public IEnumerable<GitFile> ChangedFiles
        {
            get
            {
                if (changedFiles == null) changedFiles = GetChangedFiles();

                foreach (string f in changedFiles)
                {
                    this.cache[this.GetCacheKey(f)] = GetFileStatusNoCache(f);
                }

                return from f in this.cache
                       where f.Value != GitFileStatus.Tracked &&
                             f.Value != GitFileStatus.NotControlled &&
                             f.Value != GitFileStatus.Ignored
                       select new GitFile
                       {
                           FileName = GetRelativeFileName(f.Key),
                           Status = f.Value
                       };
            }
        }

        private const int TREE = 0;
        private const int INDEX = 1;
        private const int WORKDIR = 2;

        public IList<string> GetChangedFiles()
        {
            var list = new List<string>();

            var treeWalk = new TreeWalk(this.repository);
            treeWalk.Recursive = true;
            treeWalk.Filter = TreeFilter.ANY_DIFF;

            var id = repository.Resolve(Constants.HEAD);
            if (id != null)
            {
                treeWalk.AddTree(new RevWalk(repository).ParseTree(id));
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
                if (Directory.Exists(fileName)) continue; // this excludes sub modules
                list.Add(fileName);
            }
            return list;
        } 
        #endregion

        public override string ToString()
        {
            return repository == null ? "[no repo]" : this.GitWorkingDirectory;
        }

        public Repository Repository
        {
            get { return repository; }
        }

        RepositoryGraph repositoryGraph;
        public RepositoryGraph RepositoryGraph
        {
            get
            {
                if (repositoryGraph == null)
                {
                    repositoryGraph = HasGitRepository ? new RepositoryGraph(this.repository) : null;
                }
                return repositoryGraph;
            }
        }
    }

    public abstract class Log
    {
        private static string logFileName = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "gitscc.log");

        public static void WriteLine(string format, params object[] objects)
        {
#if(DEBUG)
            var msg = string.Format(format, objects);
            msg = string.Format("{0} {1}\r\n\r\n", DateTime.UtcNow.ToString(), msg);
            File.AppendAllText(logFileName, msg);
#endif
        }
    }
}
