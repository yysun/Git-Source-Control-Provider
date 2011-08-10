using System;
using System.Collections.Generic;
using System.Text;

namespace GitScc
{
    public enum GitFileStatus
    {
        NotControlled,
        New,
        Tracked,
        Modified,
        Staged,
        Removed,
        Added,
        Deleted,
        MergeConflict,
        Ignored,
    }

    public class GitFile
    {
        public GitFileStatus Status { get; set; }
        public string FileName { get; set; }
        public bool IsStaged { get; set; }
    }
}
