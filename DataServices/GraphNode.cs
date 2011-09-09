using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GitScc.DataServices
{
    public class GraphNode : Commit
    {
        public Ref[] Refs { get; set; }
        //public string[] Branches { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}