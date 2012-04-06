/**************************************************************************\
    Copyright Microsoft Corporation. All Rights Reserved.
\**************************************************************************/

namespace Microsoft.Windows.Shell
{
    // CONSIDER:
    // The V4 implementation of this includes logic to handle hangs and crashes in Explorer.
    // The native ITaskbarList3 APIs inconsistently expose when this happens by returning
    // HRESULT_FROM_WIN32(ERROR_TIMEOUT) and HRESULT_FROM_WIN32(ERROR_INVALID_WINDOW_HANDLE).
    // When WPF sees these HRESULTs returned it has some retry heuristics based on the error.
    //
    // This implementation currently just swallows FAILED HRESULTs.  This prevents the app
    // from crashing in these scenarios, but there's potential for properties to get out of sync
    // or for repeated attempts to set these properties even when Explorer isn't running.

    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using Standard;

    public enum TaskbarItemProgressState
    {
        None,
        Indeterminate,
        Normal,
        Error,
        Paused,
    }

    public sealed class TaskbarItemInfo : Freezable
    {
        // Magic constant determined by Shell.
        private const int c_MaximumThumbButtons = 7;

        // Register Window Message used by Shell to notify that the corresponding taskbar button has been added to the taskbar.
        private static readonly WM WM_TASKBARBUTTONCREATED = NativeMethods.RegisterWindowMessage("TaskbarButtonCreated");

        private static readonly Thickness _EmptyThickness = new Thickness();

        private SafeGdiplusStartupToken _gdipToken;
        private bool _haveAddedButtons;
        private Window _window;
        private HwndSource _hwndSource;
        private ITaskbarList3 _taskbarList;
        private readonly Size _overlaySize;
        private bool _isAttached;

        protected override Freezable CreateInstanceCore()
        {
            return new TaskbarItemInfo();
        }

        #region Attached Properties and support methods.

        /// <summary>
        /// TaskbarItem Attached Dependency Property
        /// </summary>
        public static readonly DependencyProperty TaskbarItemInfoProperty = DependencyProperty.RegisterAttached(
            "TaskbarItemInfo",
            typeof(TaskbarItemInfo),
            typeof(TaskbarItemInfo),
            new PropertyMetadata(null, _OnTaskbarItemInfoChanged, _CoerceTaskbarItemInfoValue));

        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public static TaskbarItemInfo GetTaskbarItemInfo(Window window)
        {
            Verify.IsNotNull(window, "window");
            return (TaskbarItemInfo)window.GetValue(TaskbarItemInfoProperty);
        }

        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public static void SetTaskbarItemInfo(Window window, TaskbarItemInfo value)
        {
            Verify.IsNotNull(window, "window");
            window.SetValue(TaskbarItemInfoProperty, value);
        }

        /// <summary>
        /// Handles changes to the TaskbarItem property.
        /// </summary>
        private static void _OnTaskbarItemInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(d))
            {
                return;
            }

            var window = (Window)d;
            var oldbar = (TaskbarItemInfo)e.OldValue;
            var newbar = (TaskbarItemInfo)e.NewValue;

            if (oldbar == newbar)
            {
                return;
            }

            if (!Utility.IsOSWindows7OrNewer)
            {
                return;
            }

            if (oldbar != null && oldbar._window != null)
            {
                oldbar._DetachWindow();
            }

            if (newbar != null)
            {
                newbar._SetWindow(window);
            }
        }

        /// <summary>
        /// Coerces the TaskbarItem value.
        /// </summary>
        private static object _CoerceTaskbarItemInfoValue(DependencyObject d, object value)
        {
            if (DesignerProperties.GetIsInDesignMode(d))
            {
                return value;
            }

            Verify.IsNotNull(d, "d");

            var w = (Window)d;
            var superbar = (TaskbarItemInfo)value;

            // May be null if detaching
            if (superbar != null)
            {
                // Can't attach the same TaskbarItemInfo object to multiple windows.
                // This may be a redundant set to the same Window, though.
                if (superbar._window != null && superbar._window != w)
                {
                    throw new NotSupportedException();
                }
            }

            w.VerifyAccess();

            return superbar;
        }

        #endregion

        #region Dependency Properties and support methods.

        /// <summary>
        /// ProgressState Dependency Property
        /// </summary>
        public static readonly DependencyProperty ProgressStateProperty = DependencyProperty.Register(
            "ProgressState",
            typeof(TaskbarItemProgressState),
            typeof(TaskbarItemInfo),
            new PropertyMetadata(
                TaskbarItemProgressState.None,
                (d, e) => ((TaskbarItemInfo)d)._OnProgressStateChanged(),
                (d, e) => _CoerceProgressState((TaskbarItemProgressState)e)));

        /// <summary>
        /// Gets or sets the ProgressState property.  This dependency property 
        /// indicates the progress state of the Window on the superbar.
        /// </summary>
        public TaskbarItemProgressState ProgressState
        {
            get { return (TaskbarItemProgressState)GetValue(ProgressStateProperty); }
            set { SetValue(ProgressStateProperty, value); }
        }

        /// <summary>
        /// Handles changes to the ProgressState property.
        /// </summary>
        private void _OnProgressStateChanged()
        {
            if (!_isAttached)
            {
                return;
            }

            _UpdateProgressState(true);
        }

        private static TaskbarItemProgressState _CoerceProgressState(TaskbarItemProgressState value)
        {
            switch (value)
            {
                case TaskbarItemProgressState.Error:
                case TaskbarItemProgressState.Indeterminate:
                case TaskbarItemProgressState.None:
                case TaskbarItemProgressState.Normal:
                case TaskbarItemProgressState.Paused:
                    break;
                default:
                    // Convert bad data into no-progress bar.
                    value = TaskbarItemProgressState.None;
                    break;
            }
            
            return value;
        }

        /// <summary>
        /// ProgressValue Dependency Property
        /// </summary>
        public static readonly DependencyProperty ProgressValueProperty = DependencyProperty.Register(
            "ProgressValue",
            typeof(double),
            typeof(TaskbarItemInfo),
            new PropertyMetadata(
                0d,
                (d, e) => ((TaskbarItemInfo)d)._OnProgressValueChanged(),
                (d, e) => _CoerceProgressValue((double)e)));

        /// <summary>
        /// Gets or sets the ProgressValue property.  This dependency property 
        /// indicates the value of the progress bar for the Window's Superbar item.
        /// </summary>
        public double ProgressValue
        {
            get { return (double)GetValue(ProgressValueProperty); }
            set { SetValue(ProgressValueProperty, value); }
        }

        private void _OnProgressValueChanged()
        {
            if (!_isAttached)
            {
                return;
            }

            _UpdateProgressValue(true);
        }

        private static double _CoerceProgressValue(double progressValue)
        {
            if (double.IsNaN(progressValue))
            {
                progressValue = 0;
            }

            progressValue = Math.Max(progressValue, 0);
            progressValue = Math.Min(1, progressValue);

            return progressValue;
        }

        /// <summary>
        /// Overlay Dependency Property
        /// </summary>
        public static readonly DependencyProperty OverlayProperty = DependencyProperty.Register(
            "Overlay",
            typeof(ImageSource),
            typeof(TaskbarItemInfo),
            new PropertyMetadata(null, (d, e) => ((TaskbarItemInfo)d)._OnOverlayChanged()));

        /// <summary>
        /// Gets or sets the Overlay property.  This dependency property 
        /// indicates the overlay that is used to indicate status for the associated Window.
        /// </summary>
        public ImageSource Overlay
        {
            get { return (ImageSource)GetValue(OverlayProperty); }
            set { SetValue(OverlayProperty, value); }
        }

        /// <summary>
        /// Handles changes to the Overlay property.
        /// </summary>
        private void _OnOverlayChanged()
        {
            if (!_isAttached)
            {
                return;
            }

            _UpdateOverlay(true);
        }

        /// <summary>
        /// Description Dependency Property
        /// </summary>
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            "Description",
            typeof(string),
            typeof(TaskbarItemInfo),
                new PropertyMetadata(
                    string.Empty,
                    (d, e) => ((TaskbarItemInfo)d)._OnDescriptionChanged()));

        /// <summary>
        /// Gets or sets the Description property.  This dependency property 
        /// indicates the tooltip to display on the thumbnail for this window.
        /// </summary>
        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        /// <summary>
        /// Handles changes to the Description property.
        /// </summary>
        private void _OnDescriptionChanged()
        {
            if (!_isAttached)
            {
                return;
            }

            _UpdateTooltip(true);
        }

        /// <summary>
        /// ThumbnailClipMargin Dependency Property
        /// </summary>
        public static readonly DependencyProperty ThumbnailClipMarginProperty = DependencyProperty.Register(
            "ThumbnailClipMargin",
            typeof(Thickness),
            typeof(TaskbarItemInfo),
            new PropertyMetadata(
                default(Thickness),
                (d, e) => ((TaskbarItemInfo)d)._OnThumbnailClipMarginChanged(),
                (d, e) => _CoerceThumbnailClipMargin((Thickness)e)));

        /// <summary>
        /// Gets or sets the LiveThumbnailClipMargin property.  This dependency property 
        /// indicates the border of the Window to clip when displayed in the taskbar thumbnail preview.
        /// </summary>
        public Thickness ThumbnailClipMargin
        {
            get { return (Thickness)GetValue(ThumbnailClipMarginProperty); }
            set { SetValue(ThumbnailClipMarginProperty, value); }
        }

        /// <summary>
        /// Handles changes to the LiveThumbnailClipMargin property.
        /// </summary>
        private void _OnThumbnailClipMarginChanged()
        {
            if (!_isAttached)
            {
                return;
            }

            _UpdateThumbnailClipping(true);
        }

        private static Thickness _CoerceThumbnailClipMargin(Thickness margin)
        {
            // Any negative margins we'll treat as no nil.
            if (margin.Left < 0
                || margin.Right < 0
                || margin.Top < 0
                || margin.Bottom < 0)
            {
                return _EmptyThickness;
            }
            return margin;
        }

        /// <summary>
        /// ThumbButtonInfos Dependency Property
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Infos")]
        public static readonly DependencyProperty ThumbButtonInfosProperty = DependencyProperty.Register(
            "ThumbButtonInfos",
            typeof(ThumbButtonInfoCollection),
            typeof(TaskbarItemInfo),
            new PropertyMetadata(
                // Default is null, but setting it to an empty collection in the constructor
                // to support the default mutable pattern.
                null,
                (d, e) => ((TaskbarItemInfo)d)._OnThumbButtonsChanged()));

        /// <summary>
        /// Gets or sets the ThumbButtonInfos property.  This dependency property 
        /// indicates the collection of command buttons to be displayed in the Window's DWM thumbnail.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Infos")]
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public ThumbButtonInfoCollection ThumbButtonInfos
        {
            get { return (ThumbButtonInfoCollection)GetValue(ThumbButtonInfosProperty); }
            set { SetValue(ThumbButtonInfosProperty, value); }
        }

        private void _OnThumbButtonsChanged()
        {
            if (!_isAttached)
            {
                return;
            }

            _UpdateThumbButtons(true);
        }

        #endregion

        // Caller is responsible for destroying the HICON.
        private IntPtr _GetHICONFromImageSource(ImageSource image, Size dimensions)
        {
            // Verify that GDI+ has been initialized.  Putting this behind a SafeHandle to ensure it gets shutdown.
            if (null == _gdipToken)
            {
                _gdipToken = SafeGdiplusStartupToken.Startup();
            }

            return Utility.GenerateHICON(image, dimensions);
        }

        public TaskbarItemInfo()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                ITaskbarList taskbarList = null;
                try
                {
                    taskbarList = CLSID.CoCreateInstance<ITaskbarList>(CLSID.TaskbarList);
                    taskbarList.HrInit();

                    // This QI will only work on Win7.
                    _taskbarList = taskbarList as ITaskbarList3;

                    taskbarList = null;
                }
                finally
                {
                    Utility.SafeRelease(ref taskbarList);
                }

                _overlaySize = new Size(
                    NativeMethods.GetSystemMetrics(SM.CXSMICON),
                    NativeMethods.GetSystemMetrics(SM.CYSMICON));
            }

            // Set ThumbButtons to an empty list so callers can just use the property.
            ThumbButtonInfos = new ThumbButtonInfoCollection();
        }

        private void _SetWindow(Window window)
        {
            Assert.IsNull(_window);

            if (null == window)
            {
                return;
            }

            _window = window;

            // If we're not on Win7 then just set this property, but don't register anything
            if (_taskbarList == null)
            {
                return;
            }

            // Use whether we can get an HWND to determine if the Window has been shown.
            // If this works we'll assume everything's kosher.
            // If it fails we're going to jump through a few hoops to try to set our
            // initial state only once the Window is ready.
            IntPtr hwnd = new WindowInteropHelper(_window).Handle;

            bool isAttached = hwnd != IntPtr.Zero;

            if (!isAttached)
            {
                // SourceInitialized is too early.  The Window isn't yet on the Superbar.
                // Instead listen for the Shell message for the Taskbar button to be created and respond to that.
                // This also keeps things working is Explorer stops, or the Window is temporarily removed from the taskbar.
                _window.SourceInitialized += _OnWindowSourceInitialized;
            }
            else
            {
                _hwndSource = HwndSource.FromHwnd(hwnd);
                _hwndSource.AddHook(_WndProc);

                _OnIsAttachedChanged(true);
            }
        }

        private void _OnWindowSourceInitialized(object sender, EventArgs e)
        {
            // We can become detached from the Window, so don't force
            // it to keep indirect references to this object.
            _window.SourceInitialized -= _OnWindowSourceInitialized;

            IntPtr hwnd = new WindowInteropHelper(_window).Handle;
            _hwndSource = HwndSource.FromHwnd(hwnd);
            // This should be early enough that the Taskbar button hasn't yet been created,
            // so we can still handle the creation message.
            _hwndSource.AddHook(_WndProc);

            // In case the application is run elevated, allow the
            // TaskbarButtonCreated and WM_COMMAND messages through.
            // In case the application is run with severe security restrictions,
            // don't propagate exceptions for a lack of being able to do this.
            // These methods return HRESULTs that we're ignoring.
            MSGFLTINFO dontCare;
            NativeMethods.ChangeWindowMessageFilterEx(hwnd, WM_TASKBARBUTTONCREATED, MSGFLT.ALLOW, out dontCare);
            NativeMethods.ChangeWindowMessageFilterEx(hwnd, WM.COMMAND, MSGFLT.ALLOW, out dontCare);
        }

        private IntPtr _WndProc(IntPtr hwnd, int uMsg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            WM message = (WM)uMsg;

            if (message == WM_TASKBARBUTTONCREATED)
            {
                _OnIsAttachedChanged(true);
                _isAttached = true;

                handled = false;
            }
            else
            {
                switch (message)
                {
                    case WM.COMMAND:
                        if (Utility.HIWORD(wParam.ToInt32()) == THUMBBUTTON.THBN_CLICKED)
                        {
                            int index = Utility.LOWORD(wParam.ToInt32());
                            ThumbButtonInfos[index].InvokeClick();
                            handled = true;
                        }
                        break;
                    case WM.SIZE:
                        _UpdateThumbnailClipping(_isAttached);
                        handled = false;
                        break;
                }
            }

            return IntPtr.Zero;
        }

        private void _OnIsAttachedChanged(bool attached)
        {
            if (attached)
            {
                Assert.IsNotNull(_window);
                Assert.IsNotNull(_hwndSource);
            }

            // If we're changing attached state, then we'll need to redo this.
            _haveAddedButtons = false;

            // If we're detaching from a Window when we don't have its HWND
            // then we don't have anything to remove.
            if (!attached && _hwndSource == null)
            {
                return;
            }

            _UpdateOverlay(attached);
            _UpdateProgressState(attached);
            _UpdateProgressValue(attached);
            _UpdateTooltip(attached);
            _UpdateThumbnailClipping(attached);
            _UpdateThumbButtons(attached);

            if (!attached)
            {
                _hwndSource = null;
            }
        }

        private void _DetachWindow()
        {
            Assert.IsNotNull(_window);

            // Remove all event listeners.
            _window.SourceInitialized -= _OnWindowSourceInitialized;

            // Set all Superbar properties to defaults.
            _isAttached = false;
            _OnIsAttachedChanged(false);

            _window = null;
        }

        private HRESULT _UpdateOverlay(bool attached)
        {
            ImageSource source = Overlay;

            // The additional string at the end of SetOverlayIcon sets the accDescription
            // for screen readers.  We don't currently have a property that utilizes this.

            if (null == source || !attached)
            {
                return _taskbarList.SetOverlayIcon(_hwndSource.Handle, IntPtr.Zero, null);
            }

            IntPtr hicon = IntPtr.Zero;
            try
            {
                hicon = _GetHICONFromImageSource(source, _overlaySize);
                return _taskbarList.SetOverlayIcon(_hwndSource.Handle, hicon, null);
            }
            finally
            {
                Utility.SafeDestroyIcon(ref hicon);
            }
        }

        private HRESULT _UpdateTooltip(bool attached)
        {
            string tooltip = Description ?? "";
            if (!attached)
            {
                tooltip = "";
            }

            return _taskbarList.SetThumbnailTooltip(_hwndSource.Handle, tooltip);
        }

        private HRESULT _UpdateProgressValue(bool attached)
        {
            // If we're not attached then don't modify this.
            if (!attached
                || ProgressState == TaskbarItemProgressState.None
                || ProgressState == TaskbarItemProgressState.Indeterminate)
            {
                return HRESULT.S_OK;
            }

            const ulong precisionValue = 1000;
            // The coersion should enforce this.
            Assert.BoundedDoubleInc(0, ProgressValue, 1);

            var intValue = (ulong)(ProgressValue * precisionValue);
            return _taskbarList.SetProgressValue(_hwndSource.Handle, intValue, precisionValue);
        }

        private HRESULT _UpdateProgressState(bool attached)
        {
            TaskbarItemProgressState ps = ProgressState;

            TBPF tbpf = TBPF.NOPROGRESS;
            if (attached)
            {
                switch (ps)
                {
                    case TaskbarItemProgressState.Error:
                        tbpf = TBPF.ERROR;
                        break;
                    case TaskbarItemProgressState.Indeterminate:
                        tbpf = TBPF.INDETERMINATE;
                        break;
                    case TaskbarItemProgressState.None:
                        tbpf = TBPF.NOPROGRESS;
                        break;
                    case TaskbarItemProgressState.Normal:
                        tbpf = TBPF.NORMAL;
                        break;
                    case TaskbarItemProgressState.Paused:
                        tbpf = TBPF.PAUSED;
                        break;
                    default:
                        // The coersion should have caught this.
                        Assert.Fail();
                        tbpf = TBPF.NOPROGRESS;
                        break;
                }
            }

            HRESULT hr = _taskbarList.SetProgressState(_hwndSource.Handle, tbpf);
            if (hr.Succeeded)
            {
                // Explicitly update this in case this property being set
                // to None or Indeterminate before made the value not update.
                hr = _UpdateProgressValue(attached);
            }

            return hr;
        }

        private HRESULT _UpdateThumbnailClipping(bool attached)
        {
            Assert.IsNotNull(_window);

            RefRECT interopRc = null;
            if (attached && ThumbnailClipMargin != _EmptyThickness)
            {
                Thickness margin = ThumbnailClipMargin;
                // Use the native GetClientRect.  Window.ActualWidth and .ActualHeight include the non-client areas.
                RECT physicalClientRc = NativeMethods.GetClientRect(_hwndSource.Handle);
                Rect logicalClientRc = DpiHelper.DeviceRectToLogical(new Rect(physicalClientRc.Left, physicalClientRc.Top, physicalClientRc.Width, physicalClientRc.Height));

                // Crop the clipping to ensure that the margin doesn't overlap itself.
                if (margin.Left + margin.Right >= logicalClientRc.Width
                    || margin.Top + margin.Bottom >= logicalClientRc.Height)
                {
                    interopRc = new RefRECT(0, 0, 0, 0);
                }
                else
                {
                    Rect logicalClip = new Rect(margin.Left, margin.Top, logicalClientRc.Width - margin.Left - margin.Right, logicalClientRc.Height - margin.Top - margin.Bottom);
                    Rect physicalClip = DpiHelper.LogicalRectToDevice(logicalClip);
                    interopRc = new RefRECT((int)physicalClip.Left, (int)physicalClip.Top, (int)physicalClip.Right, (int)physicalClip.Bottom);
                }
            }

            // This will fail in the interop layer if called too early.
            HRESULT hr = _taskbarList.SetThumbnailClip(_hwndSource.Handle, interopRc);
            Assert.IsTrue(hr.Succeeded);
            return hr;
        }

        private HRESULT _RegisterThumbButtons()
        {
            HRESULT hr = HRESULT.S_OK;

            if (!_haveAddedButtons)
            {
                // The ITaskbarList3 API requires that the maximum number of buttons to ever be used
                // are registered at the beginning.  Modifications can be made to this list later.
                var nativeButtons = new THUMBBUTTON[c_MaximumThumbButtons];

                for (int i = 0; i < c_MaximumThumbButtons; ++i)
                {
                    nativeButtons[i] = new THUMBBUTTON
                    {
                        iId = (uint)i,
                        dwFlags = THBF.NOBACKGROUND | THBF.DISABLED | THBF.HIDDEN,
                        dwMask = THB.FLAGS | THB.ICON | THB.TOOLTIP
                    };
                }

                // If this gets called (successfully) more than once it usually returns E_INVALIDARG.  It's not really
                // a failure and we potentially want to retry this operation.
                hr = _taskbarList.ThumbBarAddButtons(_hwndSource.Handle, (uint)nativeButtons.Length, nativeButtons);
                if (hr == HRESULT.E_INVALIDARG)
                {
                    hr = HRESULT.S_FALSE;
                }
                _haveAddedButtons = hr.Succeeded;
            }

            return hr;
        }

        private HRESULT _UpdateThumbButtons(bool attached)
        {
            var nativeButtons = new THUMBBUTTON[c_MaximumThumbButtons];

            HRESULT hr = _RegisterThumbButtons();
            if (hr.Failed)
            {
                return hr;
            }

            ThumbButtonInfoCollection thumbButtons = ThumbButtonInfos;

            try
            {
                uint currentButton = 0;
                if (attached && null != thumbButtons)
                {
                    foreach (ThumbButtonInfo wrappedTB in thumbButtons)
                    {
                        var nativeTB = new THUMBBUTTON
                        {
                            iId = (uint)currentButton,
                            dwMask = THB.FLAGS | THB.TOOLTIP | THB.ICON,
                        };

                        switch (wrappedTB.Visibility)
                        {
                            case Visibility.Collapsed:
                                // HIDDEN removes the button from layout logic.
                                nativeTB.dwFlags = THBF.HIDDEN;
                                break;

                            case Visibility.Hidden:
                                // To match WPF's notion of hidden, we want this not HIDDEN,
                                // but disabled, without background, and without icon.
                                nativeTB.dwFlags = THBF.DISABLED | THBF.NOBACKGROUND;
                                nativeTB.hIcon = IntPtr.Zero;

                                break;
                            default:
                            case Visibility.Visible:

                                nativeTB.szTip = wrappedTB.Description ?? "";
                                nativeTB.hIcon = _GetHICONFromImageSource(wrappedTB.ImageSource, _overlaySize);

                                if (!wrappedTB.IsBackgroundVisible)
                                {
                                    nativeTB.dwFlags |= THBF.NOBACKGROUND;
                                }

                                if (!wrappedTB.IsEnabled)
                                {
                                    nativeTB.dwFlags |= THBF.DISABLED;
                                }
                                else
                                {
                                    nativeTB.dwFlags |= THBF.ENABLED;
                                }

                                // This is separate from enabled/disabled
                                if (!wrappedTB.IsInteractive)
                                {
                                    nativeTB.dwFlags |= THBF.NONINTERACTIVE;
                                }

                                if (wrappedTB.DismissWhenClicked)
                                {
                                    nativeTB.dwFlags |= THBF.DISMISSONCLICK;
                                }
                                break;
                        }

                        nativeButtons[currentButton] = nativeTB;

                        ++currentButton;
                        if (currentButton == c_MaximumThumbButtons)
                        {
                            break;
                        }
                    }
                }

                // If we're not attached, or the list is less than the maximum number of buttons
                // then fill in the rest with collapsed, empty buttons.
                for (; currentButton < c_MaximumThumbButtons; ++currentButton)
                {
                    nativeButtons[currentButton] = new THUMBBUTTON
                    {
                        iId = (uint)currentButton,
                        dwFlags = THBF.NOBACKGROUND | THBF.DISABLED | THBF.HIDDEN,
                        dwMask = THB.FLAGS | THB.ICON | THB.TOOLTIP
                    };
                }

                // Finally, apply the update.
                return _taskbarList.ThumbBarUpdateButtons(_hwndSource.Handle, (uint)nativeButtons.Length, nativeButtons);
            }
            finally
            {
                foreach (var nativeButton in nativeButtons)
                {
                    IntPtr hico = nativeButton.hIcon;
                    if (IntPtr.Zero != hico)
                    {
                        Utility.SafeDestroyIcon(ref hico);
                    }
                }
            }
        }
    }
}
