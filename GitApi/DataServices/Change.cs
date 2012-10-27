using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitScc.DataServices
{
    public class Change
    {
        public ChangeType ChangeType { get; set; }
        public string Name { get; set; }
    }

    public enum ChangeType
    {
        Added, 
        Deleted, 
        Modified, 
        TypeChanged, 
        Renamed, 
        Copied,
        Unmerged,
        Unknown
    }
}

