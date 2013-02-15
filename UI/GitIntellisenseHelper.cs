using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GitScc;
using GitScc.DataServices;

namespace GitScc.UI
{
    //Inspired by:
    //http://www.markembling.info/view/my-ideal-powershell-prompt-with-git-integration
    //https://github.com/dahlbyk/posh-git

    class GitIntellisenseHelper
    {
        internal static IEnumerable<string> GetOptions(GitFileStatusTracker tracker, string command)
        {
            if (tracker == null) return new string[] { };
            var options = Commands.Where(i => Regex.IsMatch(command, i.Key)).Select(i => i.Value).FirstOrDefault();
            if (options == null) return new string[] { };
            switch (options[0])
            {
                case "*branches*":
                    return tracker.RepositoryGraph.Refs
                        .Where(r => r.Type == RefTypes.Branch)
                        .Select(r => r.Name);

                case "*tags*":
                    return tracker.RepositoryGraph.Refs
                        .Where(r => r.Type == RefTypes.Tag)
                        .Select(r => r.Name);

                case "*remotes*":
                    return tracker.Remotes;

                case "*configs*":
                    return tracker.Configs.Keys;

                case "*commits*":
                    return tracker.RepositoryGraph.Commits
                        .OrderByDescending(c=>c.CommitDate)
                        .Select(r => r.ShortId);
            }

            if (options[0].Contains("|")) 
                return options[0].Split('|');
            else
                return options;
        }

        internal static string GetPrompt(GitFileStatusTracker tracker)
        {
            if(tracker==null || !tracker.HasGitRepository) return "No Git Repository";
            var changed = tracker.ChangedFiles;
            return string.Format("{0} +{1} ~{2} -{3} !{4}", tracker.CurrentBranch,
                changed.Where(f=> f.Status == GitFileStatus.New || f.Status == GitFileStatus.Added).Count(),
                changed.Where(f => f.Status == GitFileStatus.Modified || f.Status == GitFileStatus.Staged).Count(),
                changed.Where(f => f.Status == GitFileStatus.Deleted || f.Status == GitFileStatus.Removed).Count(),
                changed.Where(f => f.Status == GitFileStatus.Conflict).Count()
            );
        }

        static Dictionary<string, string[]> Commands = new Dictionary<string, string[]>{
            {"^git$", new string[] {"add", "bisect", "branch", "checkout", "commit", "config", "diff", "fetch", "format-patch", "grep",   
                               "log", "merge", "mv", "pull", "push", "rebase", "remote", "reset", "rm", "show", "status", "stash", "tag"}},

            {"^git bisect$", new string[] {"start|bad|good|skip|reset|help"}},
            {"^git rebase$", new string[] {"-i|--interactive|--continue|--skip|--abort"}},
            {"^git rebase -i$", new string[] {"HEAD~"}},
            {"^git rebase --interactive$", new string[] {"HEAD~"}},
            
            {"^git remote$", new string[] {"add|rename|rm|set-head|set-branches|set-url|show|prune|update"}},
            {"^git stash$", new string[] {"list|save|show|apply|drop|pop|branch|clear|create"}},
            //{"^git svn$", new string[] {"fetch|rebase|dcommit|info"}},

            {"^git checkout$", new string[] {"*branches*"}},
            {"^git branch -[dDmM]$", new string[] {"*branches*"}},
            {"^git tag -[asdfv]$", new string[] {"*tags*"}},
            {"^git tag .+$", new string[] {"*commits*"}},

            {"^git pull$", new string[] {"*remotes*"}},
            {"^git pull .+$", new string[] {"*branches*"}},
            {"^git push$", new string[] {"*remotes*"}},
            {"^git push .+$", new string[] {"*branches*"}},

            {"^git reset$", new string[] {"HEAD|--soft|--mixed|--hard|--merge|--keep"}},
            {"^git reset HEAD$", new string[] {"*commits*"}},

            {"^git config$", new string[] {"--global|--system|--local|--get|--add|--unset|--list|-l|--file"}},
            {"^git config\\s?(?:--global|--system|--local)?$", new string[] {"--get|--add|--unset|--list|-l"}},
            {"^git config\\s?(?:--global|--system|--local)?\\s?(?:--get|--add|--unset)$", new string[] {"*configs*"}},
            
        };
    }
}
