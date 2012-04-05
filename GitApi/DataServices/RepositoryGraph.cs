using System;
using System.Collections.Generic;
using System.Linq;
using NGit;
using NGit.Api;
using NGit.Revplot;
using NGit.Revwalk;
using NGit.Treewalk;
using NGit.Treewalk.Filter;
using NGit.Util;
using NGit.Storage.File;

namespace GitScc.DataServices
{
    public class RepositoryGraph
    {
        //private string workingDirectory;
        Repository repository;

        private List<Commit> commits;
        private List<Ref> refs;
        private List<GraphNode> nodes;
        private List<GraphLink> links;
        private bool isSimplified;

        public RepositoryGraph(Repository repository)
        {
            //this.workingDirectory = repoFolder;
            this.repository = repository;
        }

        public List<Commit> Commits
        {
            get
            {
                if (commits == null)
                {
                    PlotWalk plotWalk = null;
                    try
                    {
                        plotWalk = new PlotWalk(repository);

                        var heads = repository.GetAllRefs().Values.Select(r =>
                           plotWalk.LookupCommit(repository.Resolve(r.GetObjectId().Name)));
                        
                        foreach (var h in heads)
                        {
                            try
                            {
                                plotWalk.MarkStart(h);
                            }
                            catch { } // better than crash
                        }
                        
                        PlotCommitList<PlotLane> pcl = new PlotCommitList<PlotLane>();
                        pcl.Source(plotWalk);
                        pcl.FillTo(100);

                        commits = pcl.Select(c => new Commit
                        {
                            Id = c.Id.Name,
                            ParentIds = c.Parents.Select(p => p.Id.Name).ToList(),
                            CommitDateRelative = RelativeDateFormatter.Format(c.GetAuthorIdent().GetWhen()),
                            CommitterName = c.GetAuthorIdent().GetName(),
                            CommitterEmail = c.GetAuthorIdent().GetEmailAddress(),
                            CommitDate = c.GetAuthorIdent().GetWhen(),
                            Message = c.GetShortMessage(),
                        }).ToList();

                        commits.ForEach(commit => commit.ChildIds =
                            commits.Where(c => c.ParentIds.Contains(commit.Id))
                                   .Select(c => c.Id).ToList());
                    }
                    finally
                    {
                        if (plotWalk != null) plotWalk.Dispose();
                    }
                    
                }
                return commits;
            }
        }

        public List<Ref> Refs
        {
            get
            {
                if (refs == null && repository != null)
                {
                    refs = (from r in repository.GetAllRefs()
                            select new Ref
                            {
                                Id = r.Value.GetPeeledObjectId() != null ?
                                     r.Value.GetPeeledObjectId().Name:
                                     r.Value.GetTarget().GetObjectId().Name,
                                RefName = r.Key,
                            }).ToList();
                }
                return refs;
            }
        }

        public List<GraphNode> Nodes
        {
            get
            {
                if (nodes == null) GenerateGraph();
                return nodes;
            }
        }

        public List<GraphLink> Links
        {
            get
            {
                if (links == null) GenerateGraph();
                return links;
            }
        }

        private void GenerateGraph()
        {
            GenerateGraph(Commits);
            if (IsSimplified)
            {
                GenerateGraph(GetSimplifiedCommits());
            }
        }

        private void GenerateGraph(IList<Commit> commits)
        {
            nodes = new List<GraphNode>();
            links = new List<GraphLink>();
            var lanes = new List<string>();

            var buf = new List<string>();
            int i = 0;

            foreach (var commit in commits)
            {
                var id = commit.Id;

                var refs = from r in this.Refs
                           where r.Id == id
                           select r;

                var children = (from c in commits
                               where c.ParentIds.Contains(id)
                               select c).ToList();

                var parents = (from c in commits
                              where c.ChildIds.Contains(id)
                              select c).ToList();
                var lane = lanes.IndexOf(id);

                if (lane < 0)
                {
                    lanes.Add(id);
                    lane = lanes.Count - 1;
                }

                int m = parents.Count() - 1;
                for (int n = m; n>=0; n--)
                {
                    if (lanes.IndexOf(parents[n].Id) <= 0)
                    {
                        if (n == m)
                            lanes[lane] = parents[n].Id;
                        else
                            lanes.Add(parents[n].Id);
                    }
                }
                lanes.Remove(id);

                var node = new GraphNode
                {
                    X = lane,
                    Y = i++,
                    Id = id,
                    Message = commit.Message,
                    CommitterName = commit.CommitterName,
                    CommitDateRelative = commit.CommitDateRelative,
                    Refs = refs.ToArray(),
                };

                nodes.Add(node);

                foreach (var ch in children)
                {
                    var cnode = (from n in nodes
                                 where n.Id == ch.Id
                                 select n).FirstOrDefault();

                    if (cnode != null)
                    {
                        links.Add(new GraphLink
                        {
                            X1 = cnode.X,
                            Y1 = cnode.Y,
                            X2 = node.X,
                            Y2 = node.Y,
                            Id = id
                        });
                    }
                }

            }
        }

        private List<Commit> GetSimplifiedCommits()
        {
            foreach (var commit in Commits)
            {
                if (commit.ParentIds.Count() == 1 && commit.ChildIds.Count() == 1 && !this.Refs.Any(r=>r.Id==commit.Id))
                {                   
                    var cid = commit.ChildIds[0];
                    var pid = commit.ParentIds[0];

                    var parent = Commits.Where(c => c.Id == pid).FirstOrDefault();
                    var child = Commits.Where(c => c.Id == cid).FirstOrDefault();

                    if (parent != null && child != null)
                    {
                        int x1 = GetLane(parent.Id);
                        int x2 = GetLane(commit.Id);
                        int x3 = GetLane(child.Id);

                        if (x1 == x2 && x2 == x3)
                        {
                            commit.deleted = true;
                            parent.ChildIds[parent.ChildIds.IndexOf(commit.Id)] = cid;
                            child.ParentIds[child.ParentIds.IndexOf(commit.Id)] = pid;
                        }
                        //commit.ChildIds.Clear();
                        //commit.ParentIds.Clear();
                    }
                }
            }

            return commits.Where(c => !c.deleted).ToList();
        }

        private int GetLane(string id)
        {
            return Nodes.Where(n=>n.Id == id).Select(n=>n.X).FirstOrDefault(); 
        }

        public bool IsSimplified {
            get { return isSimplified; }
            set { isSimplified = value; commits = null; nodes = null; links = null; }
        }

        private ObjectId GetTreeIdFromCommitId(Repository repository, string commitId)
        {
            var id = repository.Resolve(commitId);
            if (id == null) return null;

            RevWalk walk = new RevWalk(repository);
            RevCommit commit = walk.ParseCommit(id);
            walk.Dispose();
            return commit == null || commit.Tree == null ? null :
                commit.Tree.Id;
        }

        public Commit GetCommit(string commitId)
        {
            //commitId = repository.Resolve(commitId).Name;
            //return Commits.Where(c => c.Id.StartsWith(commitId)).FirstOrDefault();
            var id = repository.Resolve(commitId);
            if (id == null) return null;

            RevWalk walk = new RevWalk(repository);
            RevCommit commit = walk.ParseCommit(id);
            walk.Dispose();
            return commit == null || commit.Tree == null ? null : new Commit
                {
                    Id = commit.Id.Name,
                    ParentIds = commit.Parents.Select(p => p.Id.Name).ToList(),
                    CommitDateRelative = RelativeDateFormatter.Format(commit.GetAuthorIdent().GetWhen()),
                    CommitterName = commit.GetCommitterIdent().GetName(),
                    CommitterEmail = commit.GetCommitterIdent().GetEmailAddress(),
                    CommitDate = commit.GetCommitterIdent().GetWhen(),
                    Message = commit.GetShortMessage(),
                };
        }

        public GitTreeObject GetTree(string commitId)
        {
            if (repository == null) return null;

            var treeId = GetTreeIdFromCommitId(repository, commitId);
            var tree = new GitTreeObject 
            { 
                Id = treeId.Name, Name = "", IsTree=true, IsExpanded= true,
                repository = this.repository 
            };

            //expand first level
            //foreach (var t in tree.Children) t.IsExpanded = true; 
            return tree;
        }

        public Change[] GetChanges(string fromCommitId, string toCommitId)
        {
            if (repository == null) return null;

            var id1 = GetTreeIdFromCommitId(repository, fromCommitId);
            var id2 = GetTreeIdFromCommitId(repository, toCommitId);
            if (id1 == null || id2 == null) return null;
            else
                return GetChanges(repository, id2, id1);
        }

        public Change[] GetChanges(string commitId)
        {
            if (repository == null) return null;
            RevWalk walk = null;
            try
            {
                var id = repository.Resolve(commitId);
                walk = new RevWalk(repository);
                RevCommit commit = walk.ParseCommit(id);
                if (commit == null || commit.ParentCount == 0) return null;

                var pid = commit.Parents[0].Id;
                var pcommit = walk.ParseCommit(pid);
                return GetChanges(repository, commit.Tree.Id, pcommit.Tree.Id);
            }
            finally
            {
                if (walk != null) walk.Dispose();
            }
        }

        #region get changes

        // Modified version of GitSharp's Commit class
        private Change[] GetChanges(Repository repository, ObjectId id1, ObjectId id2)
        {
            var list = new List<Change>();
            TreeWalk walk = new TreeWalk(repository);
            walk.Reset(id1, id2);
            walk.Recursive = true;
            walk.Filter = TreeFilter.ANY_DIFF;
            while (walk.Next())
            {
                int m0 = walk.GetRawMode(0);
                if (walk.TreeCount == 2)
                {
                    int m1 = walk.GetRawMode(1);
                    var change = new Change
                    {
                        Name = walk.PathString,
                    };
                    if (m0 != 0 && m1 == 0)
                    {
                        change.ChangeType = ChangeType.Added;
                    }
                    else if (m0 == 0 && m1 != 0)
                    {
                        change.ChangeType = ChangeType.Deleted;
                    }
                    else if (m0 != m1 && walk.IdEqual(0, 1))
                    {
                        change.ChangeType = ChangeType.TypeChanged;
                    }
                    else
                    {
                        change.ChangeType = ChangeType.Modified;
                    }
                    list.Add(change);
                }
                else
                {
                    var raw_modes = new int[walk.TreeCount - 1];
                    for (int i = 0; i < walk.TreeCount - 1; i++)
                        raw_modes[i] = walk.GetRawMode(i + 1);
                    var change = new Change
                    {
                        Name = walk.PathString,
                    };
                    if (m0 != 0 && raw_modes.All(m1 => m1 == 0))
                    {
                        change.ChangeType = ChangeType.Added;
                        list.Add(change);
                    }
                    else if (m0 == 0 && raw_modes.Any(m1 => m1 != 0))
                    {
                        change.ChangeType = ChangeType.Deleted;
                        list.Add(change);
                    }
                    else if (raw_modes.Select((m1, i) => new { Mode = m1, Index = i + 1 }).All(x => !walk.IdEqual(0, x.Index))) // TODO: not sure if this condition suffices in some special cases.
                    {
                        change.ChangeType = ChangeType.Modified;
                        list.Add(change);
                    }
                    else if (raw_modes.Select((m1, i) => new { Mode = m1, Index = i + 1 }).Any(x => m0 != x.Mode && walk.IdEqual(0, x.Index)))
                    {
                        change.ChangeType = ChangeType.TypeChanged;
                        list.Add(change);
                    }
                }
            }
            return list.ToArray();
        }
        #endregion

        public byte[] GetFileContent(string commitId, string fileName)
        {
            if (repository == null) return null;
            RevWalk walk = null;
            try
            {
                var head = repository.Resolve(commitId);
                RevTree revTree = head == null ? null : new RevWalk(repository).ParseTree(head);

                var entry = TreeWalk.ForPath(repository, fileName, revTree);
                if (entry != null && !entry.IsSubtree)
                {
                    var blob = repository.Open(entry.GetObjectId(0));
                    if (blob != null) return blob.GetCachedBytes();
                }
            }
            catch { }
            finally
            {
                if (walk != null) walk.Dispose();
            }
            return null;
        }
    }
}