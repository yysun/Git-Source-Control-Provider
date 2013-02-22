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
        public string Subject { get; set; }
        public string Message { get; set; }
        public string TreeId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public DateTime AuthorDate { get; set; }
        public string AuthorDateRelative { get; set; }
        internal bool deleted { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", ShortId, Subject.Replace("\r", ""));
        }

        public string ShortId { get { return Id.Substring(0, 7); } }    }
}