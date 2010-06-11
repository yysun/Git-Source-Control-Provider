using System;
using System.Collections.Generic;
using System.Text;

namespace GitScc
{
    public enum GitFileStatus
    {
        NotControlled,
        UnTrackered,
        Trackered,
        Modified,
        Staged,
        Removed,
        Added,
        Missing,
        MergeConflict
    }

    public class GitFile
    {
        public GitFileStatus Status { get; set; }
        public string FileName { get; set; }
        public bool IsStaged { get; set; }
    }
}
