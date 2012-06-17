using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using GitScc.DataServices;
using GitUI;
using System.Windows.Media.Animation;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for HistoryGraph.xaml
    /// </summary>
    public partial class HistoryGraph : UserControl
    {
        public HistoryGraph()
        {
            InitializeComponent();
        }

        #region zoom upon mouse wheel
/*
        private bool isDragging = false;
        private Point offset;
        
        private void canvasContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.isDragging = true;
            this.canvasContainer.CaptureMouse();
            offset = e.GetPosition(this.canvasContainer);
            offset.X *= this.Scaler.ScaleX;
            offset.Y *= this.Scaler.ScaleY;
        }

        private void canvasContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.isDragging)
            {
                this.isDragging = false;
                this.canvasContainer.ReleaseMouseCapture();
            }
        }

        private void canvasContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.isDragging)
            {
                Point newPosition = e.GetPosition(this.canvasRoot);
                Point newPoint = new Point(newPosition.X - offset.X, newPosition.Y - offset.Y);
                this.canvasContainer.SetValue(Canvas.LeftProperty, newPoint.X);
                this.canvasContainer.SetValue(Canvas.TopProperty, newPoint.Y);
            }
        }

        private void AdjustCanvasSize(double scale)
        {
            this.canvasContainer.Width = (PADDING * 2 + maxX * GRID_WIDTH);
            //this.canvasRoot.Width = this.canvasContainer.Width * scale;

            this.canvasContainer.Height = (PADDING * 2 + (maxY + 1) * GRID_HEIGHT);
            //this.canvasRoot.Height = this.canvasContainer.Height * scale;

        }

        private void scrollRoot_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double mouseDelta = Math.Sign(e.Delta);

            // calculate the new scale for the canvas
            double newScale = this.Scaler.ScaleX + (mouseDelta * .05);

            // Don't allow scrolling too big or small
            if (newScale < 0.1 || newScale > 5) return;

            // Set the zoom!
            this.Scaler.ScaleX = newScale;
            this.Scaler.ScaleY = newScale;
            AdjustCanvasSize(newScale);
        }
*/
        #endregion

        const int MAX_COMMITS = 200;
        const int PADDING = 50;
        const int GRID_HEIGHT = 180;
        const int GRID_WIDTH = 300;
        int maxX, maxY;
        string lastHash = null;

        private GitFileStatusTracker tracker;

        internal void Show(GitFileStatusTracker tracker, bool scroll)
        {
            this.tracker = tracker;

            //loading.Visibility = Visibility.Visible;

            Action action = () =>
            {
                try
                {
                    IList<GraphNode> commits = null;
                    string hash = null;

                    if (tracker != null && tracker.HasGitRepository)
                    {
                        commits = tracker.RepositoryGraph.Nodes;
                        hash = GetHashCode(commits);
                    }

                    bool changed = lastHash == null ? hash != null : !lastHash.Equals(hash);

                    if (changed)
                    {
                        lastHash = hash;

                        canvasContainer.Children.Clear();
                        maxX = maxY = 0;

                        if (changed && commits != null && commits.Any())
                        {
                            maxX = commits.Count();
                            maxY = commits.Max(c => c.X);

                            for (int i = commits.Count() - 1; i >= 0; i--)
                            {
                                var commit = commits[i];

                                #region Add commit box

                                var box = new CommitBox();
                                box.DataContext = new
                                {
                                    Id = commit.Id,
                                    ShortId = commit.ShortId,
                                    Comments = commit.Message,
                                    Author = commit.CommitterName,
                                    Date = commit.CommitDateRelative,
                                    Refs = commit.Refs
                                };

                                double left = GetScreenX(maxX - commit.Y);
                                double top = GetScreenY(commit.X);

                                Canvas.SetLeft(box, left);
                                Canvas.SetTop(box, top);
                                Canvas.SetZIndex(box, 10);

                                this.canvasContainer.Children.Add(box);

                                #endregion

                                #region Add Branches

                                var m = 0;
                                foreach (var head in commit.Refs.Where(r => r.Type == RefTypes.Branch || r.Type == RefTypes.HEAD))
                                {
                                    var control = new CommitHead
                                    {
                                        DataContext = head,
                                    };

                                    Canvas.SetLeft(control, left + CommitBox.WIDTH + 4);
                                    Canvas.SetTop(control, top + m++ * 30);

                                    this.canvasContainer.Children.Add(control);

                                }
                                #endregion

                                #region Add Tags
                                m = 0;
                                foreach (var tag in commit.Refs.Where(r => r.Type == RefTypes.Tag))
                                {
                                    var control = new CommitTag
                                    {
                                        DataContext = tag,
                                    };

                                    Canvas.SetLeft(control, left + m++ * 80); // TODO: get width of the control
                                    Canvas.SetTop(control, top - 24);

                                    this.canvasContainer.Children.Add(control);
                                }

                                #endregion

                                #region Add Remote Branches
                                m = 0;
                                foreach (var name in commit.Refs.Where(r => r.Type == RefTypes.RemoteBranch))
                                {
                                    var control = new CommitRemote
                                    {
                                        DataContext = new { Text = name },
                                    };

                                    Canvas.SetLeft(control, left + m++ * 100); // TODO: get width of the control
                                    Canvas.SetTop(control, top + CommitBox.HEIGHT + 4);

                                    this.canvasContainer.Children.Add(control);
                                }
                                #endregion
                            }

                            #region Add commit links

                            var links = tracker.RepositoryGraph.Links;

                            foreach (var link in links)
                            {
                                // current node
                                double x1 = link.Y1;
                                double y1 = link.X1;

                                // parent node
                                double x2 = link.Y2;
                                double y2 = link.X2;

                                bool flip = links.Any(lnk => lnk.X1 == x2 && lnk.Y2 == y2 && lnk.X1 == lnk.X2);

                                x1 = GetScreenX(maxX - x1);
                                y1 = GetScreenY(y1) + CommitBox.HEIGHT / 2;
                                x2 = GetScreenX(maxX - x2) + CommitBox.WIDTH;
                                y2 = GetScreenY(y2) + CommitBox.HEIGHT / 2;

                                if (y1 == y2)
                                {
                                    var line = new Line
                                    {
                                        Stroke = new SolidColorBrush(Color.FromArgb(255, 153, 182, 209)),
                                        StrokeThickness = 4,
                                    };
                                    line.X1 = x1;
                                    line.Y1 = y1;
                                    line.X2 = x2;
                                    line.Y2 = y2;
                                    this.canvasContainer.Children.Add(line);
                                }
                                else if (y1 > y2 && !flip)
                                {
                                    var x3 = x2 - CommitBox.WIDTH / 2;
                                    var path = new System.Windows.Shapes.Path
                                    {
                                        Stroke = new SolidColorBrush(Color.FromArgb(255, 153, 182, 209)),
                                        StrokeThickness = 4,
                                    };

                                    PathSegmentCollection pscollection = new PathSegmentCollection();

                                    pscollection.Add(new LineSegment(new Point(x2, y1), true));

                                    BezierSegment curve = new BezierSegment(
                                        new Point(x2, y1), new Point(x3, y1), new Point(x3, y2), true);
                                    pscollection.Add(curve);

                                    PathFigure pf = new PathFigure
                                    {
                                        StartPoint = new Point(x1, y1),
                                        Segments = pscollection,
                                    };
                                    PathFigureCollection pfcollection = new PathFigureCollection();
                                    pfcollection.Add(pf);
                                    PathGeometry pathGeometry = new PathGeometry();
                                    pathGeometry.Figures = pfcollection;
                                    path.Data = pathGeometry;

                                    this.canvasContainer.Children.Add(path);
                                }
                                else
                                {
                                    var x3 = x1 + CommitBox.WIDTH / 2;
                                    var path = new System.Windows.Shapes.Path
                                    {
                                        Stroke = new SolidColorBrush(Color.FromArgb(255, 153, 182, 209)),
                                        StrokeThickness = 4,
                                    };

                                    PathSegmentCollection pscollection = new PathSegmentCollection();

                                    BezierSegment curve = new BezierSegment(
                                        new Point(x3, y1), new Point(x3, y2), new Point(x1, y2), true);
                                    pscollection.Add(curve);

                                    pscollection.Add(new LineSegment(new Point(x2, y2), true));

                                    PathFigure pf = new PathFigure
                                    {
                                        StartPoint = new Point(x3, y1),
                                        Segments = pscollection,
                                    };
                                    PathFigureCollection pfcollection = new PathFigureCollection();
                                    pfcollection.Add(pf);
                                    PathGeometry pathGeometry = new PathGeometry();
                                    pathGeometry.Figures = pfcollection;
                                    path.Data = pathGeometry;

                                    this.canvasContainer.Children.Add(path);
                                }
                            }

                            #endregion
                        }

                        AdjustCanvasSize();
                    }

                    if (scroll)
                    {
                        //this.Scaler.ScaleX = this.Scaler.ScaleY = 1;
                        //AdjustCanvasSize();

                        this.zoomAndPanControl.ContentScale = 1;
                        this.canvasContainer.SetValue(Canvas.LeftProperty, 0.0);
                        this.canvasContainer.SetValue(Canvas.TopProperty, 0.0);
                        this.scrollRoot.ScrollToRightEnd();
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine("History Graph Show: {0}", ex.ToString());
                }

                //loading.Visibility = Visibility.Collapsed;

                HistoryViewCommands.GraphLoaded.Execute(null, this);
            };

            this.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        }

        private string GetHashCode(IList<GraphNode> commits)
        {
            if (commits == null) return null;
            var sb = new StringBuilder();
            foreach (var c in commits)
            {
                sb.Append(c.Id.Substring(5));
                foreach (var r in c.Refs) sb.Append(r.Id.Substring(5));
            }
            return sb.ToString();
        }

        private double GetScreenX(double x)
        {
            return PADDING + (x-1) * GRID_WIDTH;
        }

        private double GetScreenY(double y)
        {
            return PADDING + y * GRID_HEIGHT;
        }

        internal void SaveToFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;
            var ext = System.IO.Path.GetExtension(fileName);
            if (ext == ".png") ExportToPng(fileName);
            else if (ext == ".xps") ExportToXps(fileName);
        }

        private void ExportToXps(string fileName)
        {
            Transform transform = canvasContainer.LayoutTransform;
            canvasContainer.LayoutTransform = null;
            Size size = new Size(canvasContainer.Width, canvasContainer.Height);
            canvasContainer.Measure(size);
            canvasContainer.Arrange(new Rect(size));
            Package package = Package.Open(fileName, System.IO.FileMode.Create);
            XpsDocument doc = new XpsDocument(package);
            XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(doc);
            writer.Write(canvasContainer);
            doc.Close();
            package.Close();
            canvasContainer.LayoutTransform = transform;
        }

        private void ExportToPng(string fileName)
        {
            Transform transform = canvasContainer.LayoutTransform;
            canvasContainer.LayoutTransform = null;
            Size size = new Size(canvasContainer.Width, canvasContainer.Height);
            canvasContainer.Measure(size);
            canvasContainer.Arrange(new Rect(size));
            RenderTargetBitmap renderBitmap =
              new RenderTargetBitmap( (int)size.Width*300/96, (int)size.Height*300/96, 300d, 300d,
                PixelFormats.Pbgra32);
            renderBitmap.Render(canvasContainer);

            using (FileStream outStream = new FileStream(fileName, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(outStream);
            }
            canvasContainer.LayoutTransform = transform;
        }

        internal void ScrollToCommit(string commitId)
        {
            var id = commitId.Substring(0, 7);
            foreach (var element in this.canvasContainer.Children)
            {
                var box = element as CommitBox;
                if (box != null)
                {
                    box.Selected = box.txtId.Text == id;
                    if (box.Selected)
                    {
                        //this.Scaler.ScaleX = this.Scaler.ScaleY = 1;
                        zoomAndPanControl.ContentScale = 1;

                        //AdjustCanvasSize();
                        this.canvasContainer.SetValue(Canvas.LeftProperty, 0.0);
                        this.canvasContainer.SetValue(Canvas.TopProperty, 0.0);

                        var left = Canvas.GetLeft(box) + Canvas.GetLeft(canvasContainer) - this.ActualWidth / 2 + GRID_WIDTH / 2;
                        var top = Canvas.GetTop(box) + Canvas.GetTop(canvasContainer) - this.ActualHeight / 2 + GRID_HEIGHT / 2;

                        this.scrollRoot.ScrollToHorizontalOffset(left);
                        this.scrollRoot.ScrollToVerticalOffset(top);
                    }
                }
            }
        }

        #region Zoom and Pan
        /// <summary>
        /// Specifies the current state of the mouse handling logic.
        /// </summary>
        private MouseHandlingMode mouseHandlingMode = MouseHandlingMode.None;

        /// <summary>
        /// The point that was clicked relative to the ZoomAndPanControl.
        /// </summary>
        private Point origZoomAndPanControlMouseDownPoint;

        /// <summary>
        /// The point that was clicked relative to the content that is contained within the ZoomAndPanControl.
        /// </summary>
        private Point origContentMouseDownPoint;

        /// <summary>
        /// Records which mouse button clicked during mouse dragging.
        /// </summary>
        private MouseButton mouseButtonDown;

        /// <summary>
        /// Event raised on mouse down in the ZoomAndPanControl.
        /// </summary>
        private void zoomAndPanControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            canvasContainer.Focus();
            Keyboard.Focus(canvasContainer);

            mouseButtonDown = e.ChangedButton;
            origZoomAndPanControlMouseDownPoint = e.GetPosition(zoomAndPanControl);
            origContentMouseDownPoint = e.GetPosition(canvasContainer);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
                (e.ChangedButton == MouseButton.Left ||
                 e.ChangedButton == MouseButton.Right))
            {
                // Shift + left- or right-down initiates zooming mode.
                mouseHandlingMode = MouseHandlingMode.Zooming;
            }
            else if (mouseButtonDown == MouseButton.Left)
            {
                // Just a plain old left-down initiates panning mode.
                mouseHandlingMode = MouseHandlingMode.Panning;
            }

            if (mouseHandlingMode != MouseHandlingMode.None)
            {
                // Capture the mouse so that we eventually receive the mouse up event.
                zoomAndPanControl.CaptureMouse();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Event raised on mouse up in the ZoomAndPanControl.
        /// </summary>
        private void zoomAndPanControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseHandlingMode != MouseHandlingMode.None)
            {
                if (mouseHandlingMode == MouseHandlingMode.Zooming)
                {
                    if (mouseButtonDown == MouseButton.Left)
                    {
                        // Shift + left-click zooms in on the content.
                        ZoomIn();
                    }
                    else if (mouseButtonDown == MouseButton.Right)
                    {
                        // Shift + left-click zooms out from the content.
                        ZoomOut();
                    }
                }

                zoomAndPanControl.ReleaseMouseCapture();
                mouseHandlingMode = MouseHandlingMode.None;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Event raised on mouse move in the ZoomAndPanControl.
        /// </summary>
        private void zoomAndPanControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseHandlingMode == MouseHandlingMode.Panning)
            {
                //
                // The user is left-dragging the mouse.
                // Pan the viewport by the appropriate amount.
                //
                Point curContentMousePoint = e.GetPosition(canvasContainer);
                Vector dragOffset = curContentMousePoint - origContentMouseDownPoint;

                zoomAndPanControl.ContentOffsetX -= dragOffset.X;
                zoomAndPanControl.ContentOffsetY -= dragOffset.Y;

                e.Handled = true;
            }
        }

        /// <summary>
        /// Event raised by rotating the mouse wheel
        /// </summary>
        private void zoomAndPanControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (e.Delta > 0)
            {
                ZoomIn();
            }
            else if (e.Delta < 0)
            {
                ZoomOut();
            }
        }

        /// <summary>
        /// Zoom the viewport out by a small increment.
        /// </summary>
        private void ZoomOut()
        {
            zoomAndPanControl.ContentScale -= 0.1;
        }

        /// <summary>
        /// Zoom the viewport in by a small increment.
        /// </summary>
        private void ZoomIn()
        {
            zoomAndPanControl.ContentScale += 0.1;
        }
        #endregion

        #region Adjust Size
        private void AdjustCanvasSize()
        {
            this.canvasContainer.Width = Math.Max(this.ActualWidth - PADDING, (PADDING * 2 + maxX * GRID_WIDTH));
            this.canvasContainer.Height = Math.Max(this.ActualHeight * 2, (PADDING * 2 + (maxY + 1) * GRID_HEIGHT));
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustCanvasSize();
        } 
        #endregion
    }
}
