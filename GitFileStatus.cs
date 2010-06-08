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
        Deleted,
        Added,
        Missing,
    }

    public class GitFile
    {
        public GitFileStatus Status { get; set; }
        public string FileName { get; set; }
        public bool IsStaged { get; set; }
    }
}
