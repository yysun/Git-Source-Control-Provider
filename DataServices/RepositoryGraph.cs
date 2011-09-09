using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NGit.Util;
using NGit;
using NGit.Revplot;
using NGit.Api;

namespace GitScc.DataServices
{
    public class RepositoryGraph
    {
        private string workingDirectory;

        private List<Commit> commits;
        private List<Ref> refs;
        private List<GraphNode> nodes;
        private List<GraphLink> links;
        private bool isSimplified;

        public RepositoryGraph(string repoFolder)
        {
            this.workingDirectory = repoFolder;
        }

        public List<Commit> Commits
        {
            get
            {
                if (commits == null)
                {
                    Repository repository = null;
                    try
                    {
                        repository = Git.Open(this.workingDirectory).GetRepository();

                        var pw = new PlotWalk(repository);
                        var heads = repository.GetAllRefs().Values.Select(r =>
                            pw.LookupCommit(repository.Resolve(r.GetObjectId().Name))).ToList();
                        pw.MarkStart(heads);
                        PlotCommitList<PlotLane> pcl = new PlotCommitList<PlotLane>();
                        pcl.Source(pw);
                        pcl.FillTo(200);

                        commits = pcl.Select(c => new Commit
                        {
                            Id = c.Id.Name,
                            ParentIds = c.Parents.Select(p => p.Id.Name).ToList(),
                            CommitDateRelative = RelativeDateFormatter.Format(c.GetAuthorIdent().GetWhen()),
                            CommitterName = c.GetCommitterIdent().GetName(),
                            CommitterEmail = c.GetCommitterIdent().GetEmailAddress(),
                            CommitDate = c.GetCommitterIdent().GetWhen(),
                            Message = c.GetShortMessage(),
                        }).ToList();

                        commits.ForEach(commit => commit.ChildIds =
                            commits.Where(c => c.ParentIds.Contains(commit.Id))
                                   .Select(c => c.Id).ToList());
                    }
                    finally
                    {
                        if(repository!=null) repository.Close();
                    }
                    
                }
                return commits;
            }
        }

        public List<Ref> Refs
        {
            get
            {
                if (refs == null)
                {
                    Repository repository = null;
                    try
                    {
                        repository = Git.Open(this.workingDirectory).GetRepository();

                        refs = (from r in repository.GetAllRefs()
                                //where !r.Value.IsSymbolic()
                                select new Ref
                                {
                                    Id = r.Value.GetTarget().GetObjectId().Name,
                                    RefName = r.Key,
                                }).ToList();
                    }
                    finally
                    {
                        if (repository != null) repository.Close();
                    }
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
            nodes = new List<GraphNode>();
            links = new List<GraphLink>();
            var lanes = new List<string>();

            int i = 0;

            var commits = isSimplified ? SimplifiedCommits() : Commits;

            foreach (var commit in commits)
            {
                var id = commit.Id;

                var refs = from r in this.Refs
                           where r.Id == id
                           select r;
                
                var children = from c in commits
                               where c.ParentIds.Contains(id)
                               select c;

                var lane = -1;
                if (children.Count() > 1)
                {
                    lanes.Clear();
                }
                else 
                {
                    var child = children.Where(c=>c.ParentIds.IndexOf(id)==0)
                                        .Select(c=>c.Id).FirstOrDefault();

                    lane = lanes.IndexOf(child);
                }

                if (lane < 0)
                {
                    lanes.Add(id);
                    lane = lanes.Count - 1;
                }
                else
                {
                    lanes[lane] = id;
                }
                
                var node = new GraphNode 
                {
                    X = lane, Y = i++, Id = id, Message = commit.Message,
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

        private List<Commit> SimplifiedCommits()
        {
            foreach (var commit in Commits)
            {
                if (commit.ParentIds.Count() == 1 && commit.ChildIds.Count() == 1 && !this.Refs.Any(r=>r.Id==commit.Id))
                {
                    commit.deleted = true;
                    var cid = commit.ChildIds[0];
                    var pid = commit.ParentIds[0];

                    var parent = Commits.Where(c => c.Id == pid).First();
                    var child = Commits.Where(c => c.Id == cid).First();

                    parent.ChildIds[parent.ChildIds.IndexOf(commit.Id)] = cid;
                    child.ParentIds[child.ParentIds.IndexOf(commit.Id)] = pid;

                    commit.ChildIds.Clear();
                    commit.ParentIds.Clear();
                }
            }

            return commits.Where(c => !c.deleted).ToList();
        }

        public bool IsSimplified {
            get { return isSimplified; }
            set { isSimplified = value; commits = null; nodes = null; links = null; }
        }
    }
}