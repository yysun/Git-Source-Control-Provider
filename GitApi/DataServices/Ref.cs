using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GitScc.DataServices
{
    public class Ref
    {
        public string Id { get; set; }
        public string RefName { get; set; }
        public string Name
        {
            get
            {
                var name = RefName.Replace("refs/", "");
                return name.Substring(name.IndexOf("/") + 1);
            }
        }
        public RefTypes Type
        {
            get
            {
                if (RefName == "HEAD") return RefTypes.HEAD;
                else if (RefName.StartsWith("refs/heads")) return RefTypes.Branch;
                else if (RefName.StartsWith("refs/tags")) return RefTypes.Tag;
                else if (RefName.StartsWith("refs/remotes")) return RefTypes.RemoteBranch;
                return RefTypes.Unknown;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public enum RefTypes
    {
        Unknown,
        HEAD,
        Branch,
        Tag,
        RemoteBranch
    }

}