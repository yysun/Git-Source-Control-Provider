using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Windows.Threading;

namespace GitScc
{
    [Guid("C4128D99-1000-41D1-A6C3-704E6C1A3DE2")]
    public class SccProviderService : IVsSccProvider,
        IVsSccManager2,
        IVsSccManagerTooltip,
        IVsSolutionEvents,
        IVsSolutionEvents2,
        IVsFileChangeEvents,
        IVsSccGlyphs,
        IDisposable,
        IVsUpdateSolutionEvents2
    {
        private bool _active = false;
        private BasicSccProvider _sccProvider = null;
        private List<GitFileStatusTracker> trackers;
        private uint _vsSolutionEventsCookie, _vsIVsFileChangeEventsCookie, _vsIVsUpdateSolutionEventsCookie;

        #region SccProvider Service initialization/unitialization
        public SccProviderService(BasicSccProvider sccProvider, List<GitFileStatusTracker> trackers)
        {
            this._sccProvider = sccProvider;
            this.trackers = trackers;

            // Subscribe to solution events
            IVsSolution sol = (IVsSolution)sccProvider.GetService(typeof(SVsSolution));
            sol.AdviseSolutionEvents(this, out _vsSolutionEventsCookie);

            var sbm = sccProvider.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
            if (sbm != null)
            {
                sbm.AdviseUpdateSolutionEvents(this, out _vsIVsUpdateSolutionEventsCookie);
            }
        }

        public void Dispose()
        {
            // Unregister from receiving solution events
            if (VSConstants.VSCOOKIE_NIL != _vsSolutionEventsCookie)
            {
                IVsSolution sol = (IVsSolution)_sccProvider.GetService(typeof(SVsSolution));
                sol.UnadviseSolutionEvents(_vsSolutionEventsCookie);
                _vsSolutionEventsCookie = VSConstants.VSCOOKIE_NIL;
            }

            if (VSConstants.VSCOOKIE_NIL != _vsIVsUpdateSolutionEventsCookie)
            {
                var sbm = _sccProvider.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
                sbm.UnadviseUpdateSolutionEvents(_vsIVsUpdateSolutionEventsCookie);
            }
        }
        #endregion

        #region IVsSccProvider interface functions
        /// <summary>
        /// Returns whether this source control provider is the active scc provider.
        /// </summary>
        public bool Active
        {
            get { return _active; }
        }

        // Called by the scc manager when the provider is activated. 
        // Make visible and enable if necessary scc related menu commands
        public int SetActive()
        {
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Git Source Control Provider set active"));
            _active = true;
            Refresh();
            return VSConstants.S_OK;
        }

        // Called by the scc manager when the provider is deactivated. 
        // Hides and disable scc related menu commands
        public int SetInactive()
        {
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Git Source Control Provider set inactive"));
            _active = false;
            CloseTracker();
            NodesGlyphsDirty = true;
            return VSConstants.S_OK;
        }

        public int AnyItemsUnderSourceControl(out int pfResult)
        {
            pfResult = 0;
            return VSConstants.S_OK;
        }
        #endregion

        #region IVsSccManager2 interface functions

        public int BrowseForProject(out string pbstrDirectory, out int pfOK)
        {
            // Obsolete method
            pbstrDirectory = null;
            pfOK = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int CancelAfterBrowseForProject()
        {
            // Obsolete method
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Returns whether the source control provider is fully installed
        /// </summary>
        public int IsInstalled(out int pbInstalled)
        {
            // All source control packages should always return S_OK and set pbInstalled to nonzero
            pbInstalled = 1;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Provide source control icons for the specified files and returns scc status of files
        /// </summary>
        /// <returns>The method returns S_OK if at least one of the files is controlled, S_FALSE if none of them are</returns>
        public int GetSccGlyph([InAttribute] int cFiles, [InAttribute] string[] rgpszFullPaths, [OutAttribute] VsStateIcon[] rgsiGlyphs, [OutAttribute] uint[] rgdwSccStatus)
        {
            Debug.Assert(cFiles == 1, "Only getting one file icon at a time is supported");
            // Return the icons and the status. While the status is a combination a flags, we'll return just values 
            // with one bit set, to make life easier for GetSccGlyphsFromStatus

            if (rgpszFullPaths[0] == null) return 0;

            GitFileStatus status = _active ? GetFileStatus(rgpszFullPaths[0]) : GitFileStatus.NotControlled;

            //Debug.WriteLine("==== GetSccGlyph {0} : {1}", rgpszFullPaths[0], status);

            if (rgdwSccStatus != null) rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CONTROLLED;

            switch (status)
            {
                case GitFileStatus.Tracked:
                    rgsiGlyphs[0] = GitSccOptions.Current.UseTGitIconSet ?
                                    (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.Tracked) :
                                    VsStateIcon.STATEICON_CHECKEDIN;
                    break;

                case GitFileStatus.Modified:
                    rgsiGlyphs[0] = GitSccOptions.Current.UseTGitIconSet ?
                                    (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.Modified):
                                    VsStateIcon.STATEICON_CHECKEDOUT;
                    break;

                case GitFileStatus.New:
                    rgsiGlyphs[0] = (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.Untracked);
                    break;

                case GitFileStatus.Added:
                case GitFileStatus.Staged:
                    rgsiGlyphs[0] = (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.Staged);
                    break;

                case GitFileStatus.NotControlled:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_BLANK;
                    break;

                case GitFileStatus.Ignored:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_EXCLUDEDFROMSCC;
                    break;

                case GitFileStatus.Conflict:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_DISABLED;
                    break;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Determines the corresponding scc status glyph to display, given a combination of scc status flags
        /// </summary>
        public int GetSccGlyphFromStatus([InAttribute] uint dwSccStatus, [OutAttribute] VsStateIcon[] psiGlyph)
        {
            //switch (dwSccStatus)
            //{
            //    case (uint)__SccStatus.SCC_STATUS_CHECKEDOUT:
            //        psiGlyph[0] = VsStateIcon.STATEICON_CHECKEDOUT;
            //        break;
            //    case (uint)__SccStatus.SCC_STATUS_CONTROLLED:
            //        psiGlyph[0] = VsStateIcon.STATEICON_CHECKEDIN;
            //        break;
            //    default:
            //        psiGlyph[0] = VsStateIcon.STATEICON_BLANK;
            //        break;
            //}
            return VSConstants.S_OK;
        }

        /// <summary>
        /// One of the most important methods in a source control provider, is called by projects that are under source control when they are first opened to register project settings
        /// </summary>
        public int RegisterSccProject([InAttribute] IVsSccProject2 pscp2Project, [InAttribute] string pszSccProjectName, [InAttribute] string pszSccAuxPath, [InAttribute] string pszSccLocalPath, [InAttribute] string pszProvider)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called by projects registered with the source control portion of the environment before they are closed. 
        /// </summary>
        public int UnregisterSccProject([InAttribute] IVsSccProject2 pscp2Project)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSccManagerTooltip interface functions

        /// <summary>
        /// Called by solution explorer to provide tooltips for items. Returns a text describing the source control status of the item.
        /// </summary>
        public int GetGlyphTipText([InAttribute] IVsHierarchy phierHierarchy, [InAttribute] uint itemidNode, out string pbstrTooltipText)
        {
            pbstrTooltipText = "";
            GitFileStatus status = GetFileStatus(phierHierarchy, itemidNode);
            pbstrTooltipText = status.ToString(); //TODO: use resources
            return VSConstants.S_OK;
        }
        #endregion

        #region IVsSolutionEvents interface functions

        public int OnAfterOpenSolution([InAttribute] Object pUnkReserved, [InAttribute] int fNewSolution)
        {
            //automatic switch the scc provider
            if (!Active && !GitSccOptions.Current.DisableAutoLoad)
            {
                OpenTracker();
                if (trackers.Count > 0)
                {
                    IVsRegisterScciProvider rscp = (IVsRegisterScciProvider) _sccProvider.GetService(typeof(IVsRegisterScciProvider));
                    rscp.RegisterSourceControlProvider(GuidList.guidSccProvider);
                }
            }
            Refresh(); 
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution([InAttribute] Object pUnkReserved)
        {
            CloseTracker();
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject([InAttribute] IVsHierarchy pStubHierarchy, [InAttribute] IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject([InAttribute] IVsHierarchy pHierarchy, [InAttribute] int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject([InAttribute] IVsHierarchy pHierarchy, [InAttribute] int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution([InAttribute] Object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject([InAttribute] IVsHierarchy pRealHierarchy, [InAttribute] IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject([InAttribute] IVsHierarchy pHierarchy, [InAttribute] int fRemoving, [InAttribute] ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution([InAttribute] Object pUnkReserved, [InAttribute] ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject([InAttribute] IVsHierarchy pRealHierarchy, [InAttribute] ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterMergeSolution([InAttribute] Object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSccGlyphs Members

        // Remember the base index where our custom scc glyph start
        private uint _customSccGlyphBaseIndex = 0;
        // Our custom image list
        ImageList _customSccGlyphsImageList;
        // Indexes of icons in our custom image list
        enum CustomSccGlyphs
        {
            Untracked = 0,
            Staged = 1,
            Modified = 2,
            Tracked = 3,
        };

        public int GetCustomGlyphList(uint BaseIndex, out uint pdwImageListHandle)
        {
            // If this is the first time we got called, construct the image list, remember the index, etc
            if (this._customSccGlyphsImageList == null)
            {
                // The shell calls this function when the provider becomes active to get our custom glyphs
                // and to tell us what's the first index we can use for our glyphs
                // Remember the index in the scc glyphs (VsStateIcon) where our custom glyphs will start
                this._customSccGlyphBaseIndex = BaseIndex;

                // Create a new imagelist
                this._customSccGlyphsImageList = new ImageList();

                // Set the transparent color for the imagelist (the SccGlyphs.bmp uses magenta for background)
                this._customSccGlyphsImageList.TransparentColor = Color.FromArgb(255, 0, 255);

                // Set the corret imagelist size (7x16 pixels, otherwise the system will either stretch the image or fill in with black blocks)
                this._customSccGlyphsImageList.ImageSize = new Size(7, 16);

                // Add the custom scc glyphs we support to the list
                // NOTE: VS2005 and VS2008 are limited to 4 custom scc glyphs (let's hope this will change in future versions)
                Image sccGlyphs = (Image)Resources.SccGlyphs;
                this._customSccGlyphsImageList.Images.AddStrip(sccGlyphs);
            }

            // Return a Win32 HIMAGELIST handle to our imagelist to the shell (by keeping the ImageList a member of the class we guarantee the Win32 object is still valid when the shell needs it)
            pdwImageListHandle = (uint)this._customSccGlyphsImageList.Handle;

            // Return success (If you don't want to have custom glyphs return VSConstants.E_NOTIMPL)
            return VSConstants.S_OK;

        }

        #endregion

        #region File Names

        private void SetSolutionExplorerTitle(string message)
        {
            var dte = (DTE)_sccProvider.GetService(typeof(DTE));
            dte.Windows.Item(EnvDTE.Constants.vsWindowKindSolutionExplorer).Caption = message;
        }

        /// <summary>
        /// Returns the filename of the solution
        /// </summary>
        public string GetSolutionFileName()
        {
            IVsSolution sol = (IVsSolution)_sccProvider.GetService(typeof(SVsSolution));
            string solutionDirectory, solutionFile, solutionUserOptions;
            if (sol.GetSolutionInfo(out solutionDirectory, out solutionFile, out solutionUserOptions) == VSConstants.S_OK)
            {
                return solutionFile;
            }
            else
            {
                return null;
            }
        }

        private string GetProjectFileName(IVsHierarchy hierHierarchy)
        {
            if (!(hierHierarchy is IVsSccProject2)) return GetSolutionFileName();

            var files = GetNodeFiles(hierHierarchy as IVsSccProject2, VSConstants.VSITEMID_ROOT);
            string fileName = files.Count <= 0 ? null : files[0];

            //try hierHierarchy.GetCanonicalName to get project name for web site
            if (fileName == null)
            {
                if (hierHierarchy.GetCanonicalName(VSConstants.VSITEMID_ROOT, out fileName) != VSConstants.S_OK) return null;
                return GetCaseSensitiveFileName(fileName);
            }
            return fileName;
        }

        private string GetFileName(IVsHierarchy hierHierarchy, uint itemidNode)
        {
            if (itemidNode == VSConstants.VSITEMID_ROOT)
            {
                if (hierHierarchy == null)
                    return GetSolutionFileName();
                else
                    return GetProjectFileName(hierHierarchy);
            }
            else
            {
                string fileName = null;
                if (hierHierarchy.GetCanonicalName(itemidNode, out fileName) != VSConstants.S_OK) return null;
                return GetCaseSensitiveFileName(fileName);
            }
        }

        private static string GetCaseSensitiveFileName(string fileName)
        {
            if (fileName == null) return fileName;

            if (Directory.Exists(fileName) || File.Exists(fileName))
            {
                try
                {
                    StringBuilder sb = new StringBuilder(1024);
                    GetShortPathName(fileName.ToUpper(), sb, 1024);
                    GetLongPathName(sb.ToString(), sb, 1024);
                    var fn = sb.ToString();
                    return string.IsNullOrWhiteSpace(fn) ? fileName : fn;
                }
                catch { }
            }

            return fileName;
        }

        [DllImport("kernel32.dll")]
        static extern uint GetShortPathName(string longpath, StringBuilder sb, int buffer);

        [DllImport("kernel32.dll")]
        static extern uint GetLongPathName(string shortpath, StringBuilder sb, int buffer);


        /// <summary>
        /// Returns a list of source controllable files associated with the specified node
        /// </summary>
        private IList<string> GetNodeFiles(IVsSccProject2 pscp2, uint itemid)
        {
            // NOTE: the function returns only a list of files, containing both regular files and special files
            // If you want to hide the special files (similar with solution explorer), you may need to return 
            // the special files in a hastable (key=master_file, values=special_file_list)

            // Initialize output parameters
            IList<string> sccFiles = new List<string>();
            if (pscp2 != null)
            {
                CALPOLESTR[] pathStr = new CALPOLESTR[1];
                CADWORD[] flags = new CADWORD[1];

                if (pscp2.GetSccFiles(itemid, pathStr, flags) == 0)
                {
                    for (int elemIndex = 0; elemIndex < pathStr[0].cElems; elemIndex++)
                    {
                        IntPtr pathIntPtr = Marshal.ReadIntPtr(pathStr[0].pElems, elemIndex);


                        String path = Marshal.PtrToStringAuto(pathIntPtr);
                        sccFiles.Add(path);

                        // See if there are special files
                        if (flags.Length > 0 && flags[0].cElems > 0)
                        {
                            int flag = Marshal.ReadInt32(flags[0].pElems, elemIndex);

                            if (flag != 0)
                            {
                                // We have special files
                                CALPOLESTR[] specialFiles = new CALPOLESTR[1];
                                CADWORD[] specialFlags = new CADWORD[1];

                                pscp2.GetSccSpecialFiles(itemid, path, specialFiles, specialFlags);
                                for (int i = 0; i < specialFiles[0].cElems; i++)
                                {
                                    IntPtr specialPathIntPtr = Marshal.ReadIntPtr(specialFiles[0].pElems, i * IntPtr.Size);
                                    String specialPath = Marshal.PtrToStringAuto(specialPathIntPtr);

                                    sccFiles.Add(specialPath);
                                    Marshal.FreeCoTaskMem(specialPathIntPtr);
                                }

                                if (specialFiles[0].cElems > 0)
                                {
                                    Marshal.FreeCoTaskMem(specialFiles[0].pElems);
                                }
                            }
                        }

                        Marshal.FreeCoTaskMem(pathIntPtr);

                    }
                    if (pathStr[0].cElems > 0)
                    {
                        Marshal.FreeCoTaskMem(pathStr[0].pElems);
                    }
                }
            }
            else if (itemid == VSConstants.VSITEMID_ROOT)
            {
                sccFiles.Add(GetSolutionFileName());
            }

            return sccFiles;
        }


        /// <summary>
        /// Gets the list of directly selected VSITEMSELECTION objects
        /// </summary>
        /// <returns>A list of VSITEMSELECTION objects</returns>
        private IList<VSITEMSELECTION> GetSelectedNodes()
        {
            // Retrieve shell interface in order to get current selection
            IVsMonitorSelection monitorSelection = _sccProvider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

            Debug.Assert(monitorSelection != null, "Could not get the IVsMonitorSelection object from the services exposed by this project");

            if (monitorSelection == null)
            {
                throw new InvalidOperationException();
            }

            List<VSITEMSELECTION> selectedNodes = new List<VSITEMSELECTION>();
            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainer = IntPtr.Zero;
            try
            {
                // Get the current project hierarchy, project item, and selection container for the current selection
                // If the selection spans multiple hierachies, hierarchyPtr is Zero
                uint itemid;
                IVsMultiItemSelect multiItemSelect = null;
                ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid, out multiItemSelect, out selectionContainer));

                if (itemid != VSConstants.VSITEMID_SELECTION)
                {
                    // We only care if there are nodes selected in the tree
                    if (itemid != VSConstants.VSITEMID_NIL)
                    {
                        if (hierarchyPtr == IntPtr.Zero)
                        {
                            // Solution is selected
                            VSITEMSELECTION vsItemSelection;
                            vsItemSelection.pHier = null;
                            vsItemSelection.itemid = itemid;
                            selectedNodes.Add(vsItemSelection);
                        }
                        else
                        {
                            IVsHierarchy hierarchy = (IVsHierarchy)Marshal.GetObjectForIUnknown(hierarchyPtr);
                            // Single item selection
                            VSITEMSELECTION vsItemSelection;
                            vsItemSelection.pHier = hierarchy;
                            vsItemSelection.itemid = itemid;
                            selectedNodes.Add(vsItemSelection);
                        }
                    }
                }
                else
                {
                    if (multiItemSelect != null)
                    {
                        // This is a multiple item selection.

                        //Get number of items selected and also determine if the items are located in more than one hierarchy
                        uint numberOfSelectedItems;
                        int isSingleHierarchyInt;
                        ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectionInfo(out numberOfSelectedItems, out isSingleHierarchyInt));
                        bool isSingleHierarchy = (isSingleHierarchyInt != 0);

                        // Now loop all selected items and add them to the list 
                        Debug.Assert(numberOfSelectedItems > 0, "Bad number of selected itemd");
                        if (numberOfSelectedItems > 0)
                        {
                            VSITEMSELECTION[] vsItemSelections = new VSITEMSELECTION[numberOfSelectedItems];
                            ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectedItems(0, numberOfSelectedItems, vsItemSelections));
                            foreach (VSITEMSELECTION vsItemSelection in vsItemSelections)
                            {
                                selectedNodes.Add(vsItemSelection);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (hierarchyPtr != IntPtr.Zero)
                {
                    Marshal.Release(hierarchyPtr);
                }
                if (selectionContainer != IntPtr.Zero)
                {
                    Marshal.Release(selectionContainer);
                }
            }

            return selectedNodes;
        }

        #endregion

        #region open and close tracker
        string lastMinotorFolder = "";
        string monitorFolder;

        internal void OpenTracker()
        {
            Debug.WriteLine("==== Open Tracker");
            trackers.Clear();

            var solutionFileName = GetSolutionFileName();

            if (!string.IsNullOrEmpty(solutionFileName))
            {
                monitorFolder = Path.GetDirectoryName(solutionFileName);

                GetLoadedControllableProjects().ForEach(h => AddProject(h as IVsHierarchy));

                if (monitorFolder != lastMinotorFolder)
                {
                    RemoveFolderMonitor();

                    IVsFileChangeEx fileChangeService = _sccProvider.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
                    if (VSConstants.VSCOOKIE_NIL != _vsIVsFileChangeEventsCookie)
                    {
                        fileChangeService.UnadviseDirChange(_vsIVsFileChangeEventsCookie);
                    }
                    fileChangeService.AdviseDirChange(monitorFolder, 1, this, out _vsIVsFileChangeEventsCookie);
                    lastMinotorFolder = monitorFolder;

                    Debug.WriteLine("==== Monitoring: " + monitorFolder + " " + _vsIVsFileChangeEventsCookie);
                }
            }
        }

        private void CloseTracker()
        {
            Debug.WriteLine("==== Close Tracker");
            trackers.Clear();
            RemoveFolderMonitor();
            NodesGlyphsDirty = true; // set refresh flag
            //RefreshToolWindows();
        }

        private void RemoveFolderMonitor()
        {

            if (VSConstants.VSCOOKIE_NIL != _vsIVsFileChangeEventsCookie)
            {
                IVsFileChangeEx fileChangeService = _sccProvider.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
                fileChangeService.UnadviseDirChange(_vsIVsFileChangeEventsCookie);
                Debug.WriteLine("==== Stop Monitoring: " + _vsIVsFileChangeEventsCookie.ToString());
                _vsIVsFileChangeEventsCookie = VSConstants.VSCOOKIE_NIL;
                lastMinotorFolder = "";
            }
        }

        #endregion

        #region IVsFileChangeEvents

        public int DirectoryChanged(string pszDirectory)
        {
            //Debug.WriteLine("==== dir changed REFRESH: " + pszDirectory);
            Refresh();

            return VSConstants.S_OK;
        }

        public int FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region Compare and undo

        internal bool CanCompareSelectedFile
        {
            get
            {
                var fileName = GetSelectFileName();
                GitFileStatus status = GetFileStatus(fileName);
                return status == GitFileStatus.Modified || status == GitFileStatus.Staged;
            }
        }

        internal string GetSelectFileName()
        {
            var selectedNodes = GetSelectedNodes();
            if (selectedNodes.Count <= 0) return null;
            return GetFileName(selectedNodes[0].pHier, selectedNodes[0].itemid);
        }

        internal void CompareSelectedFile()
        {
            var fileName = GetSelectFileName();
            CompareFile(fileName);
        }

        internal void CompareFile(string fileName)
        {
            GitFileStatus status = GetFileStatus(fileName);
            if (status == GitFileStatus.Modified || status == GitFileStatus.Staged)
            {
                string tempFile = Path.GetFileName(fileName);
                tempFile = Path.Combine(Path.GetTempPath(), tempFile);
                CurrentTracker.SaveFileFromRepository(fileName, tempFile);
                _sccProvider.RunDiffCommand(tempFile, fileName);
            }
        }

        internal void UndoSelectedFile()
        {
            var fileName = GetSelectFileName();
            UndoFileChanges(fileName);
        }

        internal void UndoFileChanges(string fileName)
        {
            GitFileStatus status = GetFileStatus(fileName);
            if (status == GitFileStatus.Modified || status == GitFileStatus.Staged ||
                status == GitFileStatus.Deleted || status == GitFileStatus.Removed)
            {
                var deleteMsg = "";
                if (status == GitFileStatus.Deleted || status == GitFileStatus.Removed)
                {
                    deleteMsg = @"

Note: you will need to click 'Show All Files' in solution explorer to see the file.";
                }
                
                if (MessageBox.Show("Are you sure you want to undo changes for " + Path.GetFileName(fileName) +
                    " and restore a version from the last commit? " + deleteMsg,
                    "Undo Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    //SaveFileFromRepository(fileName, fileName);
                    //if (status == GitFileStatus.Staged || status == GitFileStatus.Removed)
                    //{
                    //    CurrentTracker.UnStageFile(fileName);
                    //}

                    CurrentTracker.CheckOutFile(fileName);
                }
            }
        }

        internal void EditIgnore()
        {
            if (this.CurrentTracker != null && this.CurrentTracker.HasGitRepository)
            {
                var dte = BasicSccProvider.GetServiceEx<EnvDTE.DTE>();
                var fn = Path.Combine(this.CurrentTracker.GitWorkingDirectory, ".gitignore");
                if (!File.Exists(fn)) File.WriteAllText(fn, "# git ignore file");
                dte.ItemOperations.OpenFile(fn);
            }
        }

        #endregion

        #region IVsUpdateSolutionEvents2 Members

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel)
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Begin(ref int pfCancelUpdate)
        {
            Debug.WriteLine("Git Source Control Provider: suppress refresh before build...");
            NoRefresh = true;
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Cancel()
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
        {
            Debug.WriteLine("Git Source Control Provider: resume refresh after build...");
            NoRefresh = false;
            return VSConstants.S_OK;
        }

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region project trackers
        private void AddProject(IVsHierarchy pHierarchy)
        {
            string projectName = GetProjectFileName(pHierarchy);

            if (string.IsNullOrEmpty(projectName)) return;
            string projectDirecotry = Path.GetDirectoryName(projectName);

            //Debug.WriteLine("==== Adding project: " + projectDirecotry);

            string gitfolder = GitFileStatusTracker.GetRepositoryDirectory(projectDirecotry);

            if (string.IsNullOrEmpty(gitfolder) ||
                trackers.Any(t => t.HasGitRepository && 
                             string.Compare(t.GitWorkingDirectory, gitfolder, true)==0)) return;
            
            if (gitfolder.Length < monitorFolder.Length) monitorFolder = gitfolder;
            trackers.Add(new GitFileStatusTracker(gitfolder));
            
            //Debug.WriteLine("==== Added git tracker: " + gitfolder);
           
        }

        internal string CurrentBranchName
        {
            get
            {
                return CurrentTracker == null ? null : CurrentTracker.CurrentBranch;
            }
        }

        internal string CurrentGitWorkingDirectory
        {
            get
            {
                return CurrentTracker == null ? null : CurrentTracker.GitWorkingDirectory;
            }
        }

        internal GitFileStatusTracker CurrentTracker
        {
            get
            {
                string fileName = GetSelectFileName();
                if (trackers.Count == 1) 
                    return trackers[0];
                else
                    return GetTracker(fileName);
            }
        }

        internal GitFileStatusTracker GetSolutionTracker()
        {
            return GetTracker(GetSolutionFileName());
        }

        internal GitFileStatusTracker GetTracker(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            
            return trackers.Where(t => t.HasGitRepository && 
                                  IsParentFolder(t.GitWorkingDirectory, fileName))           
                           .OrderByDescending(t => t.GitWorkingDirectory.Length)
                           .FirstOrDefault();
        }

        private bool IsParentFolder(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName) ||
               !Directory.Exists(folder)) return false;

            bool b = false;
            var dir = new DirectoryInfo(Path.GetDirectoryName(fileName));
            while (!b && dir != null)
            {
                b = string.Compare(dir.FullName, folder, true) == 0;
                dir = dir.Parent;
            }
            return b;
        }

        private GitFileStatus GetFileStatus(string fileName)
        {
            var tracker = GetTracker(fileName);
            return tracker == null ? GitFileStatus.NotControlled :
                tracker.GetFileStatus(fileName);
        }

        private GitFileStatus GetFileStatus(IVsHierarchy phierHierarchy, uint itemidNode)
        {
            var fileName = GetFileName(phierHierarchy, itemidNode);
            return GetFileStatus(fileName);
        }

        //private void SaveFileFromRepository(string fileName, string tempFile)
        //{
        //    var tracker = CurrentTracker;
        //    if (tracker == null) return;
        //    var data = tracker.GetFileContent(fileName);
        //    using (var binWriter = new BinaryWriter(File.Open(tempFile, FileMode.Create)))
        //    {
        //        binWriter.Write(data ?? new byte[] { });
        //    }
        //}
        #endregion

        #region new Refresh methods

        internal bool NodesGlyphsDirty = false;
        internal bool NoRefresh = false;
        internal DateTime lastTimeRefresh = DateTime.Now.AddDays(-1);
        internal DateTime nextTimeRefresh = DateTime.Now;

        internal void Refresh()
        {
            if (!NoRefresh)
            {
                double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
                if (delta > 500)
                {
                    NodesGlyphsDirty = true;
                    lastTimeRefresh = DateTime.Now;
                    nextTimeRefresh = DateTime.Now;
                }
            }
        }

        public void UpdateNodesGlyphs()
        {
            if (NodesGlyphsDirty && !NoRefresh)
            {
                double delta = DateTime.Now.Subtract(nextTimeRefresh).TotalMilliseconds;
                if (delta > 200)
                {
                    Debug.WriteLine("==== UpdateNodesGlyphs: " + delta.ToString());

                    //Stopwatch stopwatch = new Stopwatch();
                    //stopwatch.Start();

                    NoRefresh = true;
                    OpenTracker();
                    RefreshNodesGlyphs();
                    RefreshToolWindows();
                    NoRefresh = false;  
                    NodesGlyphsDirty = false;

                    nextTimeRefresh = DateTime.Now; //important !!
                    //stopwatch.Stop();
                    //Debug.WriteLine("==== UpdateNodesGlyphs: " + stopwatch.ElapsedMilliseconds);
                }
            }
        }

        public void RefreshNodesGlyphs()
        {
            var solHier = (IVsHierarchy)_sccProvider.GetService(typeof(SVsSolution));
            var projectList = GetLoadedControllableProjects();

            // We'll also need to refresh the solution folders glyphs
            // to reflect the controlled state
            IList<VSITEMSELECTION> nodes = new List<VSITEMSELECTION>();

            // add project node items
            foreach (IVsHierarchy hr in projectList)
            {
                VSITEMSELECTION vsItem;
                vsItem.itemid = VSConstants.VSITEMID_ROOT;
                vsItem.pHier = hr;
                nodes.Add(vsItem);
            }

            RefreshNodesGlyphs(nodes);

            var caption = "Solution Explorer";
            string branch = CurrentBranchName;
            if (!string.IsNullOrEmpty(branch))
            {
                caption += " (" + branch + ")";
                SetSolutionExplorerTitle(caption);
            }
        }

        /// <summary>
        /// Returns a list of controllable projects in the solution
        /// </summary>
        public List<IVsSccProject2> GetLoadedControllableProjects()
        {
            var list = new List<IVsSccProject2>();

            IVsSolution sol = (IVsSolution) _sccProvider.GetService(typeof(SVsSolution));
            list.Add(sol as IVsSccProject2);

            Guid rguidEnumOnlyThisType = new Guid();
            IEnumHierarchies ppenum = null;
            ErrorHandler.ThrowOnFailure(sol.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref rguidEnumOnlyThisType, out ppenum));

            IVsHierarchy[] rgelt = new IVsHierarchy[1];
            uint pceltFetched = 0;
            while (ppenum.Next(1, rgelt, out pceltFetched) == VSConstants.S_OK &&
                   pceltFetched == 1)
            {
                IVsSccProject2 sccProject2 = rgelt[0] as IVsSccProject2;
                if (sccProject2 != null)
                {
                    list.Add(sccProject2);
                }
            }

            return list;
        }

        /// <summary>
        /// Refreshes the glyphs of the specified hierarchy nodes
        /// </summary>
        public void RefreshNodesGlyphs(IList<VSITEMSELECTION> selectedNodes)
        {
            foreach (VSITEMSELECTION vsItemSel in selectedNodes)
            {
                IVsSccProject2 sccProject2 = vsItemSel.pHier as IVsSccProject2;
                if (vsItemSel.itemid == VSConstants.VSITEMID_ROOT)
                {
                    if (sccProject2 == null)
                    {
                        // Note: The solution's hierarchy does not implement IVsSccProject2, IVsSccProject interfaces
                        // It may be a pain to treat the solution as special case everywhere; a possible workaround is 
                        // to implement a solution-wrapper class, that will implement IVsSccProject2, IVsSccProject and
                        // IVsHierarhcy interfaces, and that could be used in provider's code wherever a solution is needed.
                        // This approach could unify the treatment of solution and projects in the provider's code.

                        // Until then, solution is treated as special case
                        string[] rgpszFullPaths = new string[1];
                        rgpszFullPaths[0] = GetSolutionFileName();
                        VsStateIcon[] rgsiGlyphs = new VsStateIcon[1];
                        uint[] rgdwSccStatus = new uint[1];
                        GetSccGlyph(1, rgpszFullPaths, rgsiGlyphs, rgdwSccStatus);

                        // Set the solution's glyph directly in the hierarchy
                        IVsHierarchy solHier = (IVsHierarchy) _sccProvider.GetService(typeof(SVsSolution));
                        solHier.SetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_StateIconIndex, rgsiGlyphs[0]);
                    }
                    else
                    {
                        // Refresh all the glyphs in the project; the project will call back GetSccGlyphs() 
                        // with the files for each node that will need new glyph
                        sccProject2.SccGlyphChanged(0, null, null, null);
                    }
                }
                else
                {
                    // It may be easier/faster to simply refresh all the nodes in the project, 
                    // and let the project call back on GetSccGlyphs, but just for the sake of the demo, 
                    // let's refresh ourselves only one node at a time
                    IList<string> sccFiles = GetNodeFiles(sccProject2, vsItemSel.itemid);

                    // We'll use for the node glyph just the Master file's status (ignoring special files of the node)
                    if (sccFiles.Count > 0)
                    {
                        string[] rgpszFullPaths = new string[1];
                        rgpszFullPaths[0] = sccFiles[0];
                        VsStateIcon[] rgsiGlyphs = new VsStateIcon[1];
                        uint[] rgdwSccStatus = new uint[1];
                        GetSccGlyph(1, rgpszFullPaths, rgsiGlyphs, rgdwSccStatus);

                        uint[] rguiAffectedNodes = new uint[1];
                        rguiAffectedNodes[0] = vsItemSel.itemid;
                        sccProject2.SccGlyphChanged(1, rguiAffectedNodes, rgsiGlyphs, rgdwSccStatus);
                    }
                }
            }
        }
        #endregion

        #region git
        public bool IsSolutionGitControlled
        {
            get { return trackers.Count > 0; }
        }

        internal void InitRepo()
        {
            var solutionPath = Path.GetDirectoryName(GetSolutionFileName());
            GitFileStatusTracker.Init(solutionPath);
            File.WriteAllText(Path.Combine(solutionPath, ".gitignore"),
@"Thumbs.db
*.obj
*.exe
*.pdb
*.user
*.aps
*.pch
*.vspscc
*_i.c
*_p.c
*.ncb
*.suo
*.sln.docstates
*.tlb
*.tlh
*.bak
*.cache
*.ilk
*.log
[Bb]in
[Dd]ebug*/
*.lib
*.sbr
obj/
[Rr]elease*/
_ReSharper*/
[Tt]est[Rr]esult*"
            );
        } 
        #endregion

        private void RefreshToolWindows()
        {
            var window = this._sccProvider.FindToolWindow(typeof(PendingChangesToolWindow), 0, false) 
                as PendingChangesToolWindow;
            if (window != null) window.Refresh(this.CurrentTracker);

            //var window2 = this._sccProvider.FindToolWindow(typeof(HistoryToolWindow), 0, false)
            //    as HistoryToolWindow;
            //if (window2 != null) window2.Refresh(this.CurrentTracker);
        }
    }
}