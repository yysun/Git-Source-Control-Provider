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
        //Deleted,
    }
}
