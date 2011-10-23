using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GitScc.DataServices
{
    public class Commit
    {
        public string Id { get; set; }
        public IList<string> ParentIds { get; set; }
        public IList<string> ChildIds { get; set; }
        public string Message { get; set; }
        public string CommitterName { get; set; }
        public string CommitterEmail { get; set; }
        public DateTime CommitDate { get; set; }
        public string CommitDateRelative { get; set; }
        internal bool deleted { get; set; }
        internal int lane { get; set; }
    }
}