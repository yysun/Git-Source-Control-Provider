namespace GitScc.Diff
{
    using System.Collections.Generic;

    public interface IGitCommands
    {
        IEnumerable<HunkRangeInfo> GetGitDiffFor(string filename);
    }
}
