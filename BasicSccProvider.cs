using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;

using MsVsShell = Microsoft.VisualStudio.Shell;
using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;
using Microsoft.VisualStudio.Shell;
using System.IO;
using System.Collections.Generic;
using GitScc.UI;
using System.Reflection;

namespace GitScc
{
    /////////////////////////////////////////////////////////////////////////////
    // BasicSccProvider
    [MsVsShell.ProvideLoadKey("Standard", "0.1", "Git Source Control Provider", "Yiyisun@hotmail.com", 15261)]
    [MsVsShell.DefaultRegistryRoot("Software\\Microsoft\\VisualStudio\\10.0Exp")]
    // Register the package to have information displayed in Help/About dialog box
    [MsVsShell.InstalledProductRegistration(false, "#100", "#101", "1.0.0.0", IconResourceID = CommandId.iiconProductIcon)]
    // Declare that resources for the package are to be found in the managed assembly resources, and not in a satellite dll
    [MsVsShell.PackageRegistration(UseManagedResourcesOnly = true)]
    // Register the resource ID of the CTMENU section (generated from compiling the VSCT file), so the IDE will know how to merge this package's menus with the rest of the IDE when "devenv /setup" is run
    // The menu resource ID needs to match the ResourceName number defined in the csproj project file in the VSCTCompile section
    // Everytime the version number changes VS will automatically update the menus on startup; if the version doesn't change, you will need to run manually "devenv /setup /rootsuffix:Exp" to see VSCT changes reflected in IDE
    [MsVsShell.ProvideMenuResource(1000, 1)]
    // Register a sample options page visible as Tools/Options/SourceControl/SampleOptionsPage when the provider is active
    [MsVsShell.ProvideOptionPageAttribute(typeof(SccProviderOptions), "Source Control", "Git Source Control Provider Options", 106, 107, false)]
    [ProvideToolsOptionsPageVisibility("Source Control", "Git Source Control Provider Options", "C4128D99-0000-41D1-A6C3-704E6C1A3DE2")]
    // Register a sample tool window visible only when the provider is active
    [MsVsShell.ProvideToolWindow(typeof(PendingChangesToolWindow), Style = VsDockStyle.Tabbed, Orientation = ToolWindowOrientation.Bottom)]
    [MsVsShell.ProvideToolWindowVisibility(typeof(PendingChangesToolWindow), "C4128D99-0000-41D1-A6C3-704E6C1A3DE2")]
    //[MsVsShell.ProvideToolWindow(typeof(HistoryToolWindow), Style = VsDockStyle.Tabbed, Orientation = ToolWindowOrientation.Bottom)]
    //[MsVsShell.ProvideToolWindowVisibility(typeof(HistoryToolWindow), "C4128D99-0000-41D1-A6C3-704E6C1A3DE2")]  
    //Register the source control provider's service (implementing IVsScciProvider interface)
    [MsVsShell.ProvideService(typeof(SccProviderService), ServiceName = "Git Source Control Service")]
    // Register the source control provider to be visible in Tools/Options/SourceControl/Plugin dropdown selector
    [ProvideSourceControlProvider("Git Source Control Provider", "#100")]
    // Pre-load the package when the command UI context is asserted (the provider will be automatically loaded after restarting the shell if it was active last time the shell was shutdown)
    //[MsVsShell.ProvideAutoLoad("C4128D99-0000-41D1-A6C3-704E6C1A3DE2")]
    [ProvideAutoLoad(UIContextGuids.SolutionExists)]
    // Declare the package guid
    [Guid("C4128D99-2000-41D1-A6C3-704E6C1A3DE2")]
    public class BasicSccProvider : MsVsShell.Package, IOleCommandTarget
    {
        private SccOnIdleEvent _OnIdleEvent = new SccOnIdleEvent();

        private List<GitFileStatusTracker> projects;
        private SccProviderService sccService = null;

        public BasicSccProvider()
        {
            _SccProvider = this;
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Entering constructor for: {0}", this.ToString()));
            GitBash.GitExePath = GitSccOptions.Current.GitBashPath;
            GitBash.UseUTF8FileNames = GitSccOptions.Current.UseUTF8FileNames;
        }

        /////////////////////////////////////////////////////////////////////////////
        // BasicSccProvider Package Implementation
        #region Package Members

        protected override void Initialize()
        {
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            projects = new List<GitFileStatusTracker>();
            sccService = new SccProviderService(this, projects);

            ((IServiceContainer)this).AddService(typeof(SccProviderService), sccService, true);

            // Add our command handlers for menu (commands must exist in the .vsct file)
            MsVsShell.OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as MsVsShell.OleMenuCommandService;
            if (mcs != null)
            {
                CommandID cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandRefresh);
                var menu = new MenuCommand(new EventHandler(OnRefreshCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandGitBash);
                menu = new MenuCommand(new EventHandler(OnGitBashCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandGitExtension);
                menu = new MenuCommand(new EventHandler(OnGitExtensionCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandCompare);
                menu = new MenuCommand(new EventHandler(OnCompareCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandUndo);
                menu = new MenuCommand(new EventHandler(OnUndoCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandInit);
                menu = new MenuCommand(new EventHandler(OnInitCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandEditIgnore);
                menu = new MenuCommand(new EventHandler(OnEditIgnore), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandGitTortoise);
                menu = new MenuCommand(new EventHandler(OnTortoiseGitCommand), cmd);

                mcs.AddCommand(menu);
                for (int i = 0; i < GitToolCommands.GitExtCommands.Count; i++)
                {
                    cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdGitExtCommand1 + i);
                    var mc = new MenuCommand(new EventHandler(OnGitExtCommandExec), cmd);
                    mcs.AddCommand(mc);
                }

                for (int i = 0; i < GitToolCommands.GitTorCommands.Count; i++)
                {
                    cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdGitTorCommand1 + i);
                    var mc = new MenuCommand(new EventHandler(OnGitTorCommandExec), cmd);
                    mcs.AddCommand(mc);
                }

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandPendingChanges);
                menu = new MenuCommand(new EventHandler(ShowPendingChangesWindow), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandHistory);
                menu = new MenuCommand(new EventHandler(ShowHistoryWindow), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingChangesCommitToBranch);
                menu = new MenuCommand(new EventHandler(OnSwitchBranchCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingChangesCommit);
                menu = new MenuCommand(new EventHandler(OnCommitCommand), cmd);
                mcs.AddCommand(menu);

                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingChangesAmend);
                menu = new MenuCommand(new EventHandler(OnAmendCommitCommand), cmd);
                mcs.AddCommand(menu);

            
                cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdSccCommandAbout);
                menu = new MenuCommand(new EventHandler(OnAbout), cmd);
                mcs.AddCommand(menu);
            }


            // Register the provider with the source control manager
            // If the package is to become active, this will also callback on OnActiveStateChange and the menu commands will be enabled
            IVsRegisterScciProvider rscp = (IVsRegisterScciProvider)GetService(typeof(IVsRegisterScciProvider));
            rscp.RegisterSourceControlProvider(GuidList.guidSccProvider);

            _OnIdleEvent.RegisterForIdleTimeCallbacks(GetGlobalService(typeof(SOleComponentManager)) as IOleComponentManager);
            _OnIdleEvent.OnIdleEvent += new OnIdleEvent(sccService.UpdateNodesGlyphs);

        }

        protected override void Dispose(bool disposing)
        {
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Entering Dispose() of: {0}", this.ToString()));

            _OnIdleEvent.OnIdleEvent -= new OnIdleEvent(sccService.UpdateNodesGlyphs);
            _OnIdleEvent.UnRegisterForIdleTimeCallbacks();
              
            sccService.Dispose();

            base.Dispose(disposing);
        }

        #endregion

        #region menu commands
        int IOleCommandTarget.QueryStatus(ref Guid guidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            Debug.Assert(cCmds == 1, "Multiple commands");
            Debug.Assert(prgCmds != null, "NULL argument");

            if ((prgCmds == null))  return VSConstants.E_INVALIDARG;

            // Filter out commands that are not defined by this package
            if (guidCmdGroup != GuidList.guidSccProviderCmdSet)
            {
                return (int)(Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED); 
            }

            OLECMDF cmdf = OLECMDF.OLECMDF_SUPPORTED;

            // All source control commands needs to be hidden and disabled when the provider is not active
            if (!sccService.Active)
            {
                cmdf = cmdf | OLECMDF.OLECMDF_INVISIBLE;
                cmdf = cmdf & ~(OLECMDF.OLECMDF_ENABLED);

                prgCmds[0].cmdf = (uint)cmdf;
                return VSConstants.S_OK;
            }

            // Process our Commands
            switch (prgCmds[0].cmdID)
            {
                case CommandId.imnuGitSourceControlMenu:
                    OLECMDTEXT cmdtxtStructure = (OLECMDTEXT)Marshal.PtrToStructure(pCmdText, typeof(OLECMDTEXT));
                    if (cmdtxtStructure.cmdtextf == (uint)OLECMDTEXTF.OLECMDTEXTF_NAME)
                    {
                        var branchName = sccService.CurrentBranchName;
                        string menuText = string.IsNullOrEmpty(branchName) ?
                            "Git" : "Git (" + branchName + ")";

                        SetOleCmdText(pCmdText, menuText);
                    }
                    break;

                case CommandId.icmdSccCommandGitBash:
                    if (GitBash.Exists)
                    {
                        cmdf |= OLECMDF.OLECMDF_ENABLED;
                    }
                    break;

                case CommandId.icmdSccCommandGitExtension:
                    var gitExtensionPath = GitSccOptions.Current.GitExtensionPath;
                    if (!string.IsNullOrEmpty(gitExtensionPath) && File.Exists(gitExtensionPath) && GitSccOptions.Current.NotExpandGitExtensions)
                    {
                        cmdf |= OLECMDF.OLECMDF_ENABLED;
                    }
                    else
                        cmdf |= OLECMDF.OLECMDF_INVISIBLE;
                    break;

                case CommandId.icmdSccCommandGitTortoise:
                    var tortoiseGitPath = GitSccOptions.Current.TortoiseGitPath;
                    if (!string.IsNullOrEmpty(tortoiseGitPath) && File.Exists(tortoiseGitPath) && GitSccOptions.Current.NotExpandTortoiseGit)
                    {
                        cmdf |= OLECMDF.OLECMDF_ENABLED;
                    }
                    else
                        cmdf |= OLECMDF.OLECMDF_INVISIBLE;
                    break;

                case CommandId.icmdSccCommandUndo:
                case CommandId.icmdSccCommandCompare:
                    if (GitBash.Exists && sccService.CanCompareSelectedFile) cmdf |= OLECMDF.OLECMDF_ENABLED;
                    break;

                case CommandId.icmdSccCommandEditIgnore:
                    if (sccService.IsSolutionGitControlled) cmdf |= OLECMDF.OLECMDF_ENABLED;
                    break;

                case CommandId.icmdSccCommandHistory:
                case CommandId.icmdSccCommandPendingChanges:
                case CommandId.icmdPendingChangesAmend:
                case CommandId.icmdPendingChangesCommit:
                case CommandId.icmdPendingChangesCommitToBranch:
                    if (GitBash.Exists && sccService.IsSolutionGitControlled) cmdf |= OLECMDF.OLECMDF_ENABLED;
                    break;

                case CommandId.icmdSccCommandAbout:
                case CommandId.icmdSccCommandRefresh:
                    //if (sccService.IsSolutionGitControlled)
                        cmdf |= OLECMDF.OLECMDF_ENABLED;
                    break;

                case CommandId.icmdSccCommandInit:
                    if (!sccService.IsSolutionGitControlled)
                        cmdf |= OLECMDF.OLECMDF_ENABLED;
                    else
                        cmdf |= OLECMDF.OLECMDF_INVISIBLE;
                    break;
            
                default:
                    var gitExtPath = GitSccOptions.Current.GitExtensionPath;
                    var torGitPath = GitSccOptions.Current.TortoiseGitPath;
                    if (prgCmds[0].cmdID >= CommandId.icmdGitExtCommand1 &&
                        prgCmds[0].cmdID < CommandId.icmdGitExtCommand1 + GitToolCommands.GitExtCommands.Count &&
                        !string.IsNullOrEmpty(gitExtPath) && File.Exists(gitExtPath) && !GitSccOptions.Current.NotExpandGitExtensions)
                    {
                        int idx = (int)prgCmds[0].cmdID - CommandId.icmdGitExtCommand1;
                        SetOleCmdText(pCmdText, GitToolCommands.GitExtCommands[idx].Name);
                        cmdf |= OLECMDF.OLECMDF_ENABLED;
                        break;
                    }
                    else if (prgCmds[0].cmdID >= CommandId.icmdGitTorCommand1 &&
                        prgCmds[0].cmdID < CommandId.icmdGitTorCommand1 + GitToolCommands.GitTorCommands.Count &&
                        !string.IsNullOrEmpty(torGitPath) && File.Exists(torGitPath) && !GitSccOptions.Current.NotExpandTortoiseGit)
                    {
                        int idx = (int)prgCmds[0].cmdID - CommandId.icmdGitTorCommand1;
                        SetOleCmdText(pCmdText, GitToolCommands.GitTorCommands[idx].Name);
                        cmdf |= OLECMDF.OLECMDF_ENABLED;
                        break;
                    }

                    else
                        return (int)(Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED);
            }


            prgCmds[0].cmdf = (uint) (cmdf);
            return VSConstants.S_OK;
        }

        public void SetOleCmdText(IntPtr pCmdText, string text)
        {
            OLECMDTEXT CmdText = (OLECMDTEXT)Marshal.PtrToStructure(pCmdText, typeof(OLECMDTEXT));
            char[] buffer = text.ToCharArray();
            IntPtr pText = (IntPtr)((long)pCmdText + (long)Marshal.OffsetOf(typeof(OLECMDTEXT), "rgwz"));
            IntPtr pCwActual = (IntPtr)((long)pCmdText + (long)Marshal.OffsetOf(typeof(OLECMDTEXT), "cwActual"));
            // The max chars we copy is our string, or one less than the buffer size,
            // since we need a null at the end.
            int maxChars = (int)Math.Min(CmdText.cwBuf - 1, buffer.Length);
            Marshal.Copy(buffer, 0, pText, maxChars);
            // append a null
            Marshal.WriteInt16((IntPtr)((long)pText + (long)maxChars * 2), (Int16)0);
            // write out the length + null char
            Marshal.WriteInt32(pCwActual, maxChars + 1);
        }

        private void OnRefreshCommand(object sender, EventArgs e)
        {
            sccService.NoRefresh = false;
            sccService.Refresh();
        }

        private void OnCompareCommand(object sender, EventArgs e)
        {
            sccService.CompareSelectedFile();
        }

        private void OnUndoCommand(object sender, EventArgs e)
        {
            sccService.UndoSelectedFile();
        }

        private void OnGitBashCommand(object sender, EventArgs e)
        {
            var gitBashPath = GitSccOptions.Current.GitBashPath;
            gitBashPath = gitBashPath.Replace("git.exe", "sh.exe");
            RunDetatched("cmd.exe", string.Format("/c \"{0}\" --login -i", gitBashPath));
        }

        private void OnEditIgnore(object sender, EventArgs e)
        {
            sccService.EditIgnore();
        }

        private void OnGitExtensionCommand(object sender, EventArgs e)
        {
            var gitExtensionPath = GitSccOptions.Current.GitExtensionPath;
            RunDetatched(gitExtensionPath, "");
        }

        internal void RunDiffCommand(string file1, string file2)
        {
            var difftoolPath = GitSccOptions.Current.DifftoolPath;
            RunCommand(difftoolPath, "\"" + file1 + "\" \"" + file2 + "\"");
        }

        private void OnInitCommand(object sender, EventArgs e)
        {
            sccService.InitRepo();
        }

        private void OnTortoiseGitCommand(object sender, EventArgs e)
        {
            var tortoiseGitPath = GitSccOptions.Current.TortoiseGitPath;
            RunDetatched(tortoiseGitPath, "/command:log");
        }

        private string GetTargetPath(GitToolCommand command)
        {
            var workingDirectory = sccService.CurrentGitWorkingDirectory;
            if (command.Scope == CommandScope.Project) return workingDirectory;
            var fileName = sccService.GetSelectFileName();
            if (fileName == sccService.GetSolutionFileName()) return workingDirectory;
            return fileName;
        }

        private void OnGitTorCommandExec(object sender, EventArgs e)
        {
            var menuCommand = sender as MenuCommand;
            if (null != menuCommand)
            {
                int idx = menuCommand.CommandID.ID - CommandId.icmdGitTorCommand1;

                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture,
                                  "Run GitTor Command {0}", GitToolCommands.GitTorCommands[idx].Command));

                var cmd = GitToolCommands.GitTorCommands[idx];
                var targetPath = GetTargetPath(cmd);

                var tortoiseGitPath = GitSccOptions.Current.TortoiseGitPath;
                RunDetatched(tortoiseGitPath, cmd.Command + " /path:\"" + targetPath + "\"");
            }
        }

        private void OnGitExtCommandExec(object sender, EventArgs e)
        {
            var menuCommand = sender as MenuCommand;
            if (null != menuCommand)
            {
                int idx = menuCommand.CommandID.ID - CommandId.icmdGitExtCommand1;
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture,
                                  "Run GitExt Command {0}", GitToolCommands.GitExtCommands[idx].Command));

                var gitExtensionPath = GitSccOptions.Current.GitExtensionPath;
                RunDetatched(gitExtensionPath, GitToolCommands.GitExtCommands[idx].Command);
            }
        }
        
        private void OnAbout(object sender, EventArgs e)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path = Path.Combine(path, "Readme.htm");
            Process.Start(path);
        }
        
        private void ShowPendingChangesWindow(object sender, EventArgs e)
        {
            ShowToolWindow(typeof(PendingChangesToolWindow));
        }

        private void ShowHistoryWindow(object sender, EventArgs e)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path = Path.Combine(path, "Dragon.pkg");
            var tmpPath = Path.Combine(Path.GetTempPath(), "Dragon.exe");

            try
            {
                File.Copy(path, tmpPath, true);
            }
            catch // try copy file silently
            {
            }

            if (File.Exists(tmpPath))
            {
                Process.Start(tmpPath, "\"" + sccService.CurrentTracker.GitWorkingDirectory + "\"");
            }
        }

        private void ShowToolWindow(Type type)
        {
            ToolWindowPane window = this.FindToolWindow(type, 0, true);
            IVsWindowFrame windowFrame = null;
            if (window != null && window.Frame != null)
            {
                windowFrame = (IVsWindowFrame)window.Frame;
            }
            if (windowFrame != null)
            {
                ErrorHandler.ThrowOnFailure(windowFrame.Show());
            }
        }

        private void OnSwitchBranchCommand(object sender, EventArgs e)
        {
            if (sccService.CurrentTracker == null || sccService.CurrentTracker.Repository == null) return;

            var branchPicker = new BranchPicker(
                sccService.CurrentTracker.Repository,
                sccService.CurrentTracker.RepositoryGraph.Refs);
            branchPicker.Show();
        }

        private void OnCommitCommand(object sender, EventArgs e)
        {
            GetToolWindowPane<PendingChangesToolWindow>().OnCommitCommand();
        }

        private void OnAmendCommitCommand(object sender, EventArgs e)
        {
            GetToolWindowPane<PendingChangesToolWindow>().OnAmendCommitCommand();
        }

        #endregion

        // This function is called by the IVsSccProvider service implementation when the active state of the provider changes
        // The package needs to show or hide the scc-specific commands 
        public virtual void OnActiveStateChange()
        {
        }

        public new Object GetService(Type serviceType)
        {
            return base.GetService(serviceType);
        }

        static BasicSccProvider _SccProvider = null;

        public static T GetServiceEx<T>()
        {
            if(_SccProvider == null) return default(T);
            return (T)_SccProvider.GetService(typeof(T));
        }

        #region Run Command
        internal void RunCommand(string cmd, string args)
        {
            var pinfo = new ProcessStartInfo(cmd)
            {
                Arguments = args,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = sccService.CurrentGitWorkingDirectory ??
                    Path.GetDirectoryName(sccService.GetSolutionFileName())
            };

            Process.Start(pinfo);

            //using (var process = Process.Start(pinfo))
            //{
            //    string output = process.StandardOutput.ReadToEnd();
            //    string error = process.StandardError.ReadToEnd();
            //    process.WaitForExit();

            //    if (!string.IsNullOrEmpty(error))
            //        throw new Exception(error);

            //    return output;
            //}
        }

        internal void RunDetatched(string cmd, string arguments)
        {
            using (Process process = new Process())
            {
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.ErrorDialog = false;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardInput = false;

                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.FileName = cmd;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = sccService.CurrentGitWorkingDirectory ??
                    Path.GetDirectoryName(sccService.GetSolutionFileName());
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                process.StartInfo.LoadUserProfile = true;

                process.Start();
            }
        } 
        #endregion

        //internal void OnSccStatusChanged()
        //{
        //    var pendingChangesToolWindow = GetToolWindowPane<PendingChangesToolWindow>();
        //    if (pendingChangesToolWindow != null)
        //    {
        //        pendingChangesToolWindow.Refresh(sccService.CurrentTracker);
        //    }
        //}

        private T GetToolWindowPane<T>() where T : ToolWindowPane
        {
            return (T)this.FindToolWindow(typeof(T), 0, true);
        }
    }
}