using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitScc
{
    public class GitProject
    {
        public string ProjectDirectory { get; set; }
        public GitFileStatusTracker Tracker { get; set; }
        //public uint IVsHierarchyEventsCookie { get; set; }
        //public IVsHierarchy Hierarchy { get; set; }
    }
}
