using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;

namespace GitScc.DataServices
{
    public class Blob : ITreeObject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public byte[] Content { get; set; }
    }
}