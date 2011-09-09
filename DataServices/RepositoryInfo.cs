using System;
using System.Collections.Generic;
using System.Data.Services.Common;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Configuration;

namespace GitScc.DataServices
{
    [DataServiceKey("Id")]
    public class RepositoryInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RepoFolder { get; set; }

        public static RepositoryInfo Open(string directory)
        {
            var repo = new RepositoryInfo
            {
                Name = Path.GetFileNameWithoutExtension(directory),
                RepoFolder = directory,
                //Id = GetId(directory)
            };
            return repo;
        }

        public IEnumerable<Commit> Commits
        {
            get
            {
                var git = NGit.Api.Git.Open(RepoFolder);
                var commits = git.Log().Call();
                return from c in commits
                       select new Commit
                       {
                           Id = c.Id.Name,
                           ParentIds = string.Join(",", c.Parents.Select(p => p.Id.Name).ToArray()),
                           //CommitDateRelative = ss[2],
                           CommitterName = c.GetCommitterIdent().GetName(),
                           CommitterEmail = c.GetCommitterIdent().GetEmailAddress(),
                           CommitDate = c.GetCommitterIdent().GetWhen(),
                           Tree = new Tree
                           {
                               Id = c.Tree.Id.Name,
                               RepoFolder = this.RepoFolder,
                               Name = c.Tree.Name
                           },
                           Message = c.GetShortMessage(),
                       };
            }
        }

        public IEnumerable<Ref> Refs
        {
            get
            {
                var repository = NGit.Api.Git.Open(RepoFolder).GetRepository();
                var refs = from r in repository.GetAllRefs()
                           where !r.Value.IsSymbolic()
                           select new Ref
                           {
                               Id = r.Value.GetTarget().GetObjectId().Name,
                               RefName = r.Key.Replace("refs/", ""),
                           };

                repository.Close();
                return refs;
            }
        }
    }
}