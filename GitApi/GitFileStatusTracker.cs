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
        private Tree commitTree;
        private GitIndex index;
        private DirCache dirCache;
        private ObjectId head;
        private Dictionary<string, GitFileStatus> cache;
        private IEnumerable<GitFile> changedFiles;
        private IList<IgnoreRule> ignoreRules;
        private IEnumerable<string> remotes;
        private IDictionary<string, string> configs;

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
            this.index = null;
            this.commitTree = null;

            this.cache.Clear();
            this.changedFiles = null;
            this.repositoryGraph = null;
            this.dirCache = null;
            this.head = null;
            this.ignoreRules = null;
            this.remotes = null;
            this.configs = null;
            if (!string.IsNullOrEmpty(initFolder))
            {
                try
                {
                    this.repository = Open(new DirectoryInfo(initFolder));
                    dirCache = repository.ReadDirCache();
                    head = repository.Resolve(Constants.HEAD);

                    if (this.repository != null)
                    {
                        if (head == null)
                        {
                            this.commitTree = new Tree(repository);
                        }
                        else
                        {
                            var treeId = ObjectId.FromString(repository.Open(head).GetBytes(), 5);
                            this.commitTree = new Tree(repository, treeId, repository.Open(treeId).GetBytes());
                        }
                        this.index = repository.GetIndex();
                        this.index.RereadIfNecessary();

                        try
                        {
                            //load local .gitignore file
                            var ignoreFile = Path.Combine(this.initFolder, 
                                Constants.GITIGNORE_FILENAME);
                            if (File.Exists(ignoreFile))
                            {
                                ignoreRules = File.ReadAllLines(ignoreFile)
                                                  .Where(line => !line.StartsWith("#") && line.Trim().Length > 0)
                                                  .Select(line => new IgnoreRule(line)).ToList();
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    this.repository = null;
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
                    status = GetFileStatusNoCacheOld(fileName);
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

        public GitFileStatus GetFileStatusNoCache(string fileName)
        {
            if (Directory.Exists(fileName)) return GitFileStatus.Ignored;

            var fileNameRel = GetRelativeFileNameForGit(fileName);
            TreeWalk treeWalk = new TreeWalk(this.repository) { Recursive = true };
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

            var status = GitFileStatus.NotControlled;
            if (treeWalk.Next())
            {
                status = GetFileStatus(treeWalk);
            }

            if (status == GitFileStatus.NotControlled)
            {
                var dirCacheEntry2 = dirCache.GetEntry(fileNameRel);
                if (dirCacheEntry2 != null)
                {
                    var treeEntry2 = TreeWalk.ForPath(repository, fileNameRel, revTree);
                    if (treeEntry2 != null && treeEntry2.GetObjectId(0).Equals(dirCacheEntry2.GetObjectId()))
                        return GitFileStatus.Tracked;
                }
            }
            return GitFileStatus.NotControlled;
        }

        private GitFileStatus GetFileStatus(TreeWalk treeWalk)
        {
            AbstractTreeIterator treeIterator = treeWalk.GetTree<AbstractTreeIterator>(TREE);
            DirCacheIterator dirCacheIterator = treeWalk.GetTree<DirCacheIterator>(INDEX);
            WorkingTreeIterator workingTreeIterator = treeWalk.GetTree<WorkingTreeIterator>(WORKDIR);
            if (dirCacheIterator != null)
            {
                DirCacheEntry dirCacheEntry = dirCacheIterator.GetDirCacheEntry();
                if (dirCacheEntry != null && dirCacheEntry.Stage > 0)
                {
                    return GitFileStatus.Conflict;
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

            return GitFileStatus.NotControlled;
        }

        public GitFileStatus GetFileStatusNoCacheOld(string fileName)
        {
            //Debug.WriteLine(string.Format("===+ GetFileStatusNoCache {0}", fileName));

            var fileNameRel = GetRelativeFileName(fileName);

            TreeEntry treeEntry = this.commitTree == null ? null : this.commitTree.FindBlobMember(fileNameRel);
            GitIndex.Entry indexEntry = this.index==null? null : this.index.GetEntry(fileNameRel);

            //the order of 'if' below is important
            if (indexEntry != null)
            {
                if (treeEntry == null)
                {
                    return GitFileStatus.Added;
                }
                if (!File.Exists(fileName))
                {
                    return GitFileStatus.Deleted;
                }
                if (File.Exists(fileName) && indexEntry.IsModified(repository.WorkTree, true))
                {
                    return GitFileStatus.Modified;
                }
                if (treeEntry != null && !treeEntry.GetId().Equals(indexEntry.GetObjectId()))
                {
                    return GitFileStatus.Staged;
                }
                if (indexEntry.GetStage() != 0)
                {
                    return GitFileStatus.Conflict;
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
                    if (ignoreRules != null && ignoreRules.Any(rule => rule.IsMatch(fileName, false)))
                    {
                        return GitFileStatus.Ignored;
                    }

                    return GitFileStatus.New;
                }
            }

            return GitFileStatus.NotControlled;
        }

        #endregion

        #region Full/Relative File Name, Cache Key

        private string GetCacheKey(string fileName)
        {
            return GetRelativeFileName(fileName).ToLower();
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
                if (revTree != null)
                {
                    var entry = TreeWalk.ForPath(repository, fileName, revTree);
                    if (entry != null && !entry.IsSubtree)
                    {
                        var blob = repository.Open(entry.GetObjectId(0));
                        if (blob != null) return blob.GetCachedBytes();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("Get File Content: {0}\r\n{1}", fileName, ex.ToString());
            }

            return null;
        } 
        #endregion

        #region repository status: branch, in the middle of xxx
        public string CurrentBranch
        {
            get
            {
                if (this.HasGitRepository)
                {
                    var branch = this.repository.GetBranch();

                    if (branch != null)
                    {
                        if (IsInTheMiddleOfBisect) branch += " | BISECTING";
                        if (IsInTheMiddleOfMerge) branch += " | MERGING";
                        if (IsInTheMiddleOfPatch) branch += " | AM";
                        if (IsInTheMiddleOfRebase) branch += " | REBASE";
                        if (IsInTheMiddleOfRebaseI) branch += " | REBASE-i";
                    }
                    return branch;
                }
                else
                    return "";
            }
        }

        public bool IsInTheMiddleOfBisect
        {
            get
            {
                return this.HasGitRepository ? FileExistsInRepo("BISECT_START") : false;
            }
        }

        public bool IsInTheMiddleOfMerge
        {
            get
            {
                return this.HasGitRepository ? FileExistsInRepo("MERGE_HEAD") : false;
            }
        }

        public bool IsInTheMiddleOfPatch
        {
            get
            {
                return this.HasGitRepository ? FileExistsInRepo("rebase-*", "applying") : false;
            }
        }

        public bool IsInTheMiddleOfRebase
        {
            get
            {
                return this.HasGitRepository ? FileExistsInRepo("rebase-*", "rebasing") : false;
            }
        }

        public bool IsInTheMiddleOfRebaseI
        {
            get
            {
                return this.HasGitRepository ? FileExistsInRepo("rebase-*", "interactive") : false;
            }
        }
        
        private bool FileExistsInRepo(string fileName)
        {
            return File.Exists(Path.Combine(this.repository.Directory, fileName));
        }

        private bool FileExistsInRepo(string directory, string fileName)
        {
            if (Directory.Exists(this.repository.Directory))
            {
                foreach (var dir in Directory.GetDirectories(this.repository.Directory, directory))
                {
                    if (File.Exists(Path.Combine(dir, fileName))) return true;
                }
            }
            return false;
        }

        public void CheckOutBranch(string branch, bool createNew = false)
        {
            if (!this.HasGitRepository) return;
            if (GitBash.Exists)
            {
                try
                {
                    GitBash.Run(string.Format("checkout {0} {1}", (createNew ? "-b" : ""), branch), this.GitWorkingDirectory);
                }
                catch (Exception ex)
                {
                    if (!ex.Message.StartsWith("Switched to a new branch")) throw;
                }
            }
            else
            {
                Git git = new Git(this.repository);

                git.Checkout().SetName(branch)
                    .SetCreateBranch(createNew)
                    .Call();
            }
        }

        #endregion
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

            if (GitBash.Exists)
            {
                if (head == null)
                {
                    GitBash.Run(string.Format("rm --cached -- \"{0}\"", fileNameRel), this.GitWorkingDirectory);
                }
                else
                {
                    GitBash.Run(string.Format("reset -- \"{0}\"", fileNameRel), this.GitWorkingDirectory);
                }
            }
            else
            {
                TreeEntry treeEntry = null;
                if (commitTree != null) treeEntry = commitTree.FindBlobMember(fileNameRel);

                //var index = repository.GetIndex();
                //index.RereadIfNecessary();

                index.Remove(repository.WorkTree, fileName);

                if (treeEntry != null)
                {
                    index.AddEntry(treeEntry);
                }
                index.Write();
            }

            this.cache.Remove(GetCacheKey(fileName));
            this.changedFiles = null;
        }

        /// <summary>
        /// Requires absolute path
        /// </summary>
        /// <param name="fileName"></param>
        public void StageFile(string fileName)
        {
            if (!this.HasGitRepository) return;
            //var index = repository.GetIndex();
            //index.RereadIfNecessary();
            if (GitBash.Exists)
            {
                if (File.Exists(fileName))
                {
                    GitBash.Run(string.Format("add \"{0}\"", GetRelativeFileName(fileName)), this.GitWorkingDirectory);
                }
                else
                {
                    GitBash.Run(string.Format("rm --cached -- \"{0}\"", GetRelativeFileName(fileName)), this.GitWorkingDirectory);
                }
            }
            else
            {
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
            }

            this.cache.Remove(GetCacheKey(fileName));
            this.changedFiles = null;
        }

        public string Commit(string message)
        {
            if (!this.HasGitRepository) return null;

            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Commit message must not be null or empty!", "message");

            string msg = "";
            if (GitBash.Exists)
            {
                var msgFile = Path.Combine(this.repository.Directory, "COMMITMESSAGE");
                File.WriteAllText(msgFile, message);
                msg = GitBash.Run(string.Format("commit -F \"{0}\"", msgFile), this.GitWorkingDirectory);
                if (msg.IndexOf('\n') > 0) msg = msg.Split('\n')[0];
                File.Delete(msgFile);
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
                var msgFile = Path.Combine(this.repository.Directory, "COMMITMESSAGE");
                File.WriteAllText(msgFile, message);
                msg = GitBash.Run(string.Format("commit --amend -F \"{0}\"", msgFile), this.GitWorkingDirectory);
                if (msg.IndexOf('\n') > 0) msg = msg.Split('\n')[0];
                File.Delete(msgFile);
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
                GitBash.Run("config core.ignorecase true", folderName);
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

        #region Diff file
        /// <summary>
        /// Diff working file with last commit
        /// </summary>
        /// <param name="fileName">Expect relative path</param>
        /// <returns>diff file in temp folder</returns>
        public string DiffFile(string fileName)
        {
            try
            {
                if (!this.HasGitRepository) return "";

                var tmpFileName = Path.ChangeExtension(Path.GetTempFileName(), ".diff");
                var status = GetFileStatus(fileName);
                if (head == null || status == GitFileStatus.New || status == GitFileStatus.Added)
                {
                    tmpFileName = Path.ChangeExtension(tmpFileName, Path.GetExtension(fileName));
                    File.Copy(GetFullPath(fileName), tmpFileName);
                    return tmpFileName;
                }

                if (GitBash.Exists)
                {
                    var fileNameRel = GetRelativeFileName(fileName);

                    GitBash.RunCmd(string.Format("diff HEAD -- \"{0}\" > \"{1}\"", fileNameRel, tmpFileName), this.GitWorkingDirectory);
                }
                else
                {
                    HistogramDiff hd = new HistogramDiff();
                    hd.SetFallbackAlgorithm(null);

                    var fullName = GetFullPath(fileName);

                    RawText b = new RawText(File.Exists(GetFullPath(fileName)) ?
                                            File.ReadAllBytes(fullName) : new byte[0]);
                    RawText a = new RawText(GetFileContent(fileName) ?? new byte[0]);

                    var list = hd.Diff(RawTextComparator.DEFAULT, a, b);

                    using (Stream stream = File.Create(tmpFileName))
                    {
                        DiffFormatter df = new DiffFormatter(stream);
                        df.Format(list, a, b);
                        df.Flush();
                    }

                    //using (Stream mstream = new MemoryStream(),
                    //              stream = new BufferedStream(mstream))
                    //{
                    //    DiffFormatter df = new DiffFormatter(stream);
                    //    df.Format(list, a, b);
                    //    df.Flush();
                    //    stream.Seek(0, SeekOrigin.Begin);
                    //    var ret = new StreamReader(stream).ReadToEnd();
                    //    File.WriteAllText(tmpFileName, ret);
                    //}
                }

                return tmpFileName;
            }
            catch (Exception ex)
            {
                Log.WriteLine("Refresh: {0}\r\n{1}", this.initFolder, ex.ToString());

                return "";
            }
        }

        public string DiffFile(string fileName, string commitId1, string commitId2)
        {
            try
            {
                if (!this.HasGitRepository) return "";

                var tmpFileName = Path.ChangeExtension(Path.GetTempFileName(), ".diff");
                var fileNameRel = GetRelativeFileName(fileName);

                if (GitBash.Exists)
                {
                    GitBash.RunCmd(string.Format("diff {2} {3} -- \"{0}\" > \"{1}\"", fileNameRel, tmpFileName, commitId1, commitId2), this.GitWorkingDirectory);
                }
                else
                {
                    HistogramDiff hd = new HistogramDiff();
                    hd.SetFallbackAlgorithm(null);

                    RawText a = string.IsNullOrWhiteSpace(commitId1) ? new RawText(new byte[0]) :
                        new RawText(this.RepositoryGraph.GetFileContent(commitId1, fileNameRel) ?? new byte[0]);
                    RawText b = string.IsNullOrWhiteSpace(commitId2) ? new RawText(new byte[0]) :
                        new RawText(this.RepositoryGraph.GetFileContent(commitId2, fileNameRel) ?? new byte[0]);

                    var list = hd.Diff(RawTextComparator.DEFAULT, a, b);

                    using (Stream stream = new FileStream(tmpFileName, System.IO.FileMode.CreateNew))
                    {
                        DiffFormatter df = new DiffFormatter(stream);
                        df.Format(list, a, b);
                        df.Flush();
                    }

                    //using (Stream mstream = new MemoryStream(),
                    //      stream = new BufferedStream(mstream))
                    //{
                    //    DiffFormatter df = new DiffFormatter(stream);
                    //    df.Format(list, a, b);
                    //    df.Flush();
                    //    stream.Seek(0, SeekOrigin.Begin);
                    //    var ret = new StreamReader(stream).ReadToEnd();
                    //    ret = ret.Replace("\r", "").Replace("\n", "\r\n");
                    //    File.WriteAllText(tmpFileName, ret);
                    //}

                }

                return tmpFileName;
            }
            catch (Exception ex)
            {
                Log.WriteLine("Refresh: {0}\r\n{1}", this.initFolder, ex.ToString());

                return "";
            }
        }
        
        #endregion

        #region Changed Files
        public IEnumerable<GitFile> ChangedFiles
        {
            get
            {
                if (changedFiles == null) changedFiles = GetChangedFiles();
                return changedFiles;
            }
        }

        private const int TREE = 0;
        private const int INDEX = 1;
        private const int WORKDIR = 2;

        internal IList<GitFile> GetChangedFiles()
        {
            if (!HasGitRepository) return new List<GitFile>();

            if (GitBash.Exists)
            {
                var output = GitBash.Run("status --porcelain -z --untracked-files", this.GitWorkingDirectory);
                return ParseGitStatus(output);
            }
            else
            {
                var list = new List<GitFile>();

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
                    WorkingTreeIterator workingTreeIterator = treeWalk.GetTree<WorkingTreeIterator>(WORKDIR);
                    if (workingTreeIterator != null && workingTreeIterator.IsEntryIgnored()) continue;
                    var fileName = GetFullPath(treeWalk.PathString);
                    if (Directory.Exists(fileName)) continue; // this excludes sub modules

                    var status = GetFileStatus(treeWalk);
                    list.Add(new GitFile
                    {
                        FileName = GetRelativeFileName(fileName),
                        Status = status
                    });

                    this.cache[GetCacheKey(fileName)] = status;
                }
                return list;
            }
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

        #region copied and modified from git extensions
        public IList<GitFile> ParseGitStatus(string statusString)
        {
            //Debug.WriteLine(statusString);

            var list = new List<GitFile>();
            if (string.IsNullOrEmpty(statusString)) return list;

            // trim warning messages
            var nl = new char[] { '\n', '\r' };
            string trimmedStatus = statusString.Trim(nl);
            int lastNewLinePos = trimmedStatus.LastIndexOfAny(nl);
            if (lastNewLinePos > 0)
            {
                int ind = trimmedStatus.LastIndexOf('\0');
                if (ind < lastNewLinePos) //Warning at end
                {
                    lastNewLinePos = trimmedStatus.IndexOfAny(nl, ind >= 0 ? ind : 0);
                    trimmedStatus = trimmedStatus.Substring(0, lastNewLinePos).Trim(nl);
                }
                else                                              //Warning at beginning
                    trimmedStatus = trimmedStatus.Substring(lastNewLinePos).Trim(nl);
            }


            //Split all files on '\0' (WE NEED ALL COMMANDS TO BE RUN WITH -z! THIS IS ALSO IMPORTANT FOR ENCODING ISSUES!)
            var files = trimmedStatus.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            for (int n = 0; n < files.Length; n++)
            {
                if (string.IsNullOrEmpty(files[n]))
                    continue;

                int splitIndex = files[n].IndexOfAny(new char[] { '\0', '\t', ' ' }, 1);

                string status = string.Empty;
                string fileName = string.Empty;

                if (splitIndex < 0)
                {
                    //status = files[n];
                    //fileName = files[n + 1];
                    //n++;
                    continue;
                }
                else
                {
                    status = files[n].Substring(0, splitIndex);
                    fileName = files[n].Substring(splitIndex);
                }

                //X shows the status of the index, and Y shows the status of the work tree

                char x = status[0];
                char y = status.Length > 1 ? status[1] : ' ';

                var gitFile = new GitFile { FileName = fileName.Trim() };

                switch (x)
                {
                    case '?':
                        gitFile.Status = GitFileStatus.New;
                        break;
                    case '!':
                        gitFile.Status = GitFileStatus.Ignored;
                        break;
                    case ' ':
                        if (y == 'M') gitFile.Status = GitFileStatus.Modified;
                        else if (y == 'D') gitFile.Status = GitFileStatus.Deleted;
                        break;
                    case 'M':
                        if (y == 'M') gitFile.Status = GitFileStatus.Modified;
                        else gitFile.Status = GitFileStatus.Staged;
                        break;
                    case 'A':
                        gitFile.Status = GitFileStatus.Added;
                        break;
                    case 'D':
                        gitFile.Status = GitFileStatus.Removed;
                        break;
                    case 'R':
                        gitFile.Status = GitFileStatus.Renamed;
                        break;
                    case 'C':
                        gitFile.Status = GitFileStatus.Copied;
                        break;

                    case 'U':
                        gitFile.Status = GitFileStatus.Conflict;
                        break;
                }
                list.Add(gitFile);
                
                this.cache[GetCacheKey(gitFile.FileName)] = gitFile.Status;
            }
            return list;
        }

        #endregion

        public void SaveFileFromRepository(string fileName, string tempFile)
        {
            if (!this.HasGitRepository || this.head == null) return;

            if (GitBash.Exists)
            {
                string fileNameRel = GetRelativeFileNameForGit(fileName);
                GitBash.RunCmd(string.Format("show \"HEAD:{0}\" > \"{1}\"", fileNameRel, tempFile), this.GitWorkingDirectory);
            }
            else
            {
                var data = GetFileContent(fileName);
                using (var binWriter = new BinaryWriter(File.Open(tempFile, System.IO.FileMode.Create)))
                {
                    binWriter.Write(data ?? new byte[] { });
                }
            }
        }

        public void CheckOutFile(string fileName)
        {
            if (!this.HasGitRepository || this.head == null) return;
            
            string fileNameRel = GetRelativeFileName(fileName);
            
            if (GitBash.Exists)
            {
                GitBash.Run(string.Format("checkout -- \"{0}\"", fileNameRel), this.GitWorkingDirectory);
            }
            else
            {
                GitFileStatus status = GetFileStatus(fileName);
                SaveFileFromRepository(fileName, fileName);
                if (status == GitFileStatus.Staged || status == GitFileStatus.Removed)
                {
                    UnStageFile(fileName);
                }
            }
        }

        public IEnumerable<string> Remotes
        {
            get
            {
                if (remotes == null && GitBash.Exists)
                {
                    remotes = GitBash.Run("remote", this.GitWorkingDirectory)
                        .Split('\n').Where(s=>!string.IsNullOrWhiteSpace(s));
                }
                return remotes;
            }
        }

        public IDictionary<string, string> Configs
        {
            get
            {
                if (configs == null && GitBash.Exists)
                {
                    var lines = GitBash.Run("config -l", this.GitWorkingDirectory)
                        .Split('\n').Where(s => !string.IsNullOrWhiteSpace(s) && s.IndexOf("=") > 0)
                        .OrderBy(s=>s);

                    configs = new Dictionary<string, string>();
                    foreach (var s in lines)
                    {
                        var pos = s.IndexOf("=");
                        var key = s.Substring(0, pos);
                        if(!configs.Keys.Contains(key))
                            configs.Add(key, s.Substring(pos+1));
                    }
                        
                }
                return configs ?? new Dictionary<string, string>();
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
