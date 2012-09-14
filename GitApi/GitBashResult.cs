using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitScc
{
    public class GitBashResult
    {
        public bool HasError { get; set; }

        public string Error { get; set; }

        public string Output { get; set; }
    }
}
