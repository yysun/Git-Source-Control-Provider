using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;

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
        IDisposable
    {
        private bool _active = false;
        private BasicSccProvider _sccProvider = null;
        private uint _vsSolutionEventsCookie, _vsIVsFileChangeEventsCookie;

        private GitFileStatusTracker _statusTracker = null;

        #region SccProvider Service initialization/unitialization
        public SccProviderService(BasicSccProvider sccProvider)
        {
            _sccProvider = sccProvider;
            _statusTracker = new GitFileStatusTracker();

            // Subscribe to solution events
            IVsSolution sol = (IVsSolution)_sccProvider.GetService(typeof(SVsSolution));
            sol.AdviseSolutionEvents(this, out _vsSolutionEventsCookie);

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
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Provider set active"));
            _active = true;
            _sccProvider.OnActiveStateChange();

            OpenTracker();
            RefreshSolutionNode();
            return VSConstants.S_OK;
        }

        // Called by the scc manager when the provider is deactivated. 
        // Hides and disable scc related menu commands
        public int SetInactive()
        {
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Provider set inactive"));

            _active = false;
            _sccProvider.OnActiveStateChange();

            CloseTracker();
            RefreshSolutionNode();
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
            GitFileStatus status = _statusTracker.GetFileStatus(rgpszFullPaths[0]);

            switch (status)
            {
                case GitFileStatus.Trackered:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDIN;
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CONTROLLED;
                    }
                    break;

                case GitFileStatus.Modified:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CHECKEDOUT;
                    }
                    break;

                case GitFileStatus.UnTrackered:
                    //rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    rgsiGlyphs[0] = (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.PendingAdd);
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CHECKEDOUT;
                    }
                    break;

                case GitFileStatus.Staged:
                    //rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    rgsiGlyphs[0] = (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.Staged);
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CHECKEDOUT;
                    }
                    break;

                case GitFileStatus.NotControlled:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_BLANK;
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_NOTCONTROLLED;
                    }
                    break;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Determines the corresponding scc status glyph to display, given a combination of scc status flags
        /// </summary>
        public int GetSccGlyphFromStatus([InAttribute] uint dwSccStatus, [OutAttribute] VsStateIcon[] psiGlyph)
        {
            switch (dwSccStatus)
            {
                case (uint)__SccStatus.SCC_STATUS_CHECKEDOUT:
                    psiGlyph[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    break;
                case (uint)__SccStatus.SCC_STATUS_CONTROLLED:
                    psiGlyph[0] = VsStateIcon.STATEICON_CHECKEDIN;
                    break;
                default:
                    psiGlyph[0] = VsStateIcon.STATEICON_BLANK;
                    break;
            }
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
            IList<string> files = GetNodeFiles(phierHierarchy as IVsSccProject2, itemidNode);
            if (files.Count == 0)
            {
                return VSConstants.S_OK;
            }
            GitFileStatus status = _statusTracker.GetFileStatus(files[0]);
            pbstrTooltipText = status.ToString(); //TODO: use resources

            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSolutionEvents interface functions

        public int OnAfterOpenSolution([InAttribute] Object pUnkReserved, [InAttribute] int fNewSolution)
        {
            OpenTracker();
            ReDrawStateGlyphs();
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution([InAttribute] Object pUnkReserved)
        {
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
            CloseTracker();
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
            PendingAdd = 0,
            Staged = 1,
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

        internal void OpenTracker()
        {
            string solutionFileName = GetSolutionFileName();

            if (!string.IsNullOrEmpty(solutionFileName))
            {

                string pathGetsolution = Path.GetDirectoryName(solutionFileName);
                _statusTracker.Open(pathGetsolution);

                IVsFileChangeEx fileChangeService = _sccProvider.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
                fileChangeService.AdviseDirChange(pathGetsolution, 1, this, out _vsIVsFileChangeEventsCookie);
            }
        }

   
        private void CloseTracker()
        {
            _statusTracker.Close();
            if (VSConstants.VSCOOKIE_NIL != _vsIVsFileChangeEventsCookie)
            {
                IVsFileChangeEx fileChangeService = _sccProvider.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
                fileChangeService.UnadviseDirChange(_vsIVsFileChangeEventsCookie);
            }
        }

        #region refresh

        internal void Refresh()
        {
            _statusTracker.Update();
            ReDrawStateGlyphs();
        }

        internal void ReDrawStateGlyphs()
        {
            IVsHierarchy sol = (IVsHierarchy)_sccProvider.GetService(typeof(SVsSolution));
            EnumHierarchyItems(sol as IVsHierarchy, VSConstants.VSITEMID_ROOT, 0);
        }


        private void EnumHierarchyItems(IVsHierarchy hierarchy, uint itemid, int recursionLevel)
        {

            if (recursionLevel > 1) return;

            int hr;
            IntPtr nestedHierarchyObj;
            uint nestedItemId;
            Guid hierGuid = typeof(IVsHierarchy).GUID;

            hr = hierarchy.GetNestedHierarchy(itemid, ref hierGuid, out nestedHierarchyObj, out nestedItemId);
            if (VSConstants.S_OK == hr && IntPtr.Zero != nestedHierarchyObj)
            {
                IVsHierarchy nestedHierarchy = Marshal.GetObjectForIUnknown(nestedHierarchyObj) as IVsHierarchy;
                Marshal.Release(nestedHierarchyObj);
                if (nestedHierarchy != null)
                {
                    EnumHierarchyItems(nestedHierarchy, nestedItemId, recursionLevel);
                }
            }
            else
            {
                object pVar;

                processNodeFunc(hierarchy, itemid);

                recursionLevel++;

                hr = hierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out pVar);
                if (VSConstants.S_OK == hr)
                {
                    uint childId = GetItemId(pVar);
                    while (childId != VSConstants.VSITEMID_NIL)
                    {
                        EnumHierarchyItems(hierarchy, childId, recursionLevel);
                        hr = hierarchy.GetProperty(childId, (int)__VSHPROPID.VSHPROPID_NextVisibleSibling, out pVar);
                        if (VSConstants.S_OK == hr)
                        {
                            childId = GetItemId(pVar);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the item id.
        /// </summary>
        /// <param name="pvar">VARIANT holding an itemid.</param>
        /// <returns>Item Id of the concerned node</returns>
        private uint GetItemId(object pvar)
        {
            if (pvar == null) return VSConstants.VSITEMID_NIL;
            if (pvar is int) return (uint)(int)pvar;
            if (pvar is uint) return (uint)pvar;
            if (pvar is short) return (uint)(short)pvar;
            if (pvar is ushort) return (uint)(ushort)pvar;
            if (pvar is long) return (uint)(long)pvar;
            return VSConstants.VSITEMID_NIL;
        }
        
        private void RefreshSolutionNode()
        {
            string fileName = GetSolutionFileName();
            if (string.IsNullOrEmpty(fileName)) return;

            VsStateIcon[] rgsiGlyphs = new VsStateIcon[1];
            uint[] rgdwSccStatus = new uint[1];
            GetSccGlyph(1, new string[] { fileName }, rgsiGlyphs, rgdwSccStatus);

            // Set the solution's glyph directly in the hierarchy
            IVsHierarchy solHier = (IVsHierarchy)_sccProvider.GetService(typeof(SVsSolution));
            solHier.SetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_StateIconIndex, rgsiGlyphs[0]);

            //string branch = _statusTracker.CurrentBranch;
            //if (!string.IsNullOrEmpty(branch))
            //{
            //    caption += " - " + branch;
            //}
            //solHier.SetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Caption, caption);

        }

        private void processNodeFunc(IVsHierarchy hierarchy, uint itemid)
        {

            var sccProject2 = hierarchy as IVsSccProject2;

            if (itemid == VSConstants.VSITEMID_ROOT)
            {
                if (sccProject2 == null)
                {
                    RefreshSolutionNode();
                }
                else
                {
                    // Refresh all the glyphs in the project; the project will call back GetSccGlyphs() 
                    // with the files for each node that will need new glyph
                    sccProject2.SccGlyphChanged(0, null, null, null);
                }
            }
        }

        /// <summary>
        /// Returns the filename of the solution
        /// </summary>
        public string GetSolutionFileName()
        {
            IVsSolution sol = (IVsSolution) _sccProvider.GetService(typeof(SVsSolution));
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

        #region IVsFileChangeEvents

        private DateTime lastTimeDirChangeFired;

        public int DirectoryChanged(string pszDirectory)
        {
            double delta = DateTime.Now.Subtract(lastTimeDirChangeFired).TotalMilliseconds;
            if (delta > 1000)
            {
                System.Threading.Thread.Sleep(100);
                Refresh();
            }
            lastTimeDirChangeFired = DateTime.Now;
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
                GitFileStatus status = _statusTracker.GetFileStatus(fileName);
                return status == GitFileStatus.Modified || status == GitFileStatus.Staged;
            }
        }

        internal string GetSelectFileName()
        {
            var selectedNodes = GetSelectedNodes();
            if (selectedNodes.Count <= 0) return null;

            var files = GetNodeFiles(selectedNodes[0].pHier as IVsSccProject2, selectedNodes[0].itemid);
            if (files.Count <= 0) return null;

            return files[0];
        }

        internal void CompareSelectedFile()
        {
            var fileName = GetSelectFileName();

            GitFileStatus status = _statusTracker.GetFileStatus(fileName);
            if (status == GitFileStatus.Modified || status == GitFileStatus.Staged)
            {
                string tempFile = Path.GetFileName(fileName);
                tempFile = Path.Combine(Path.GetTempPath(), tempFile);

                var data = _statusTracker.GetFileContent(fileName);
                using (var binWriter = new BinaryWriter(File.Open(tempFile, FileMode.Create)))
                {
                    binWriter.Write(data ?? new byte[] { });
                }

                _sccProvider.RunDiffCommand(tempFile, fileName);
            }
        }

        internal void UndoSelectedFile()
        {
            var fileName = GetSelectFileName();

            GitFileStatus status = _statusTracker.GetFileStatus(fileName);
            if (status == GitFileStatus.Modified || status == GitFileStatus.Staged)
            {
                if (MessageBox.Show("Are you sure you want to undo changes for " + Path.GetFileName(fileName) +
                    " and store it from last commit? ", 
                    "Undo Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var data = _statusTracker.GetFileContent(fileName);
                    using (var binWriter = new BinaryWriter(File.Open(fileName, FileMode.Create)))
                    {
                        binWriter.Write(data ?? new byte[] { });
                    }
                }
            }
        }

        #endregion
    }
}