using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NGit;
using NGit.Revplot;
using NGit.Revwalk;

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

        #region zoom and pan

        private bool isDragging = false;
        private Point offset;

        /// <summary>
        /// Called when user presses mouse down. Start
        /// panning.
        /// </summary>
        private void canvasContainer_MouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            // Say we are dragging
            this.isDragging = true;
            this.canvasContainer.CaptureMouse();
            // Calculate the place where they clicked
            offset = e.GetPosition(this.canvasContainer);
            offset.X *= this.Scaler.ScaleX;
            offset.Y *= this.Scaler.ScaleY;
        }

        /// <summary>
        /// Called when user releases mouse. End panning
        /// </summary>
        private void canvasContainer_MouseLeftButtonUp(
            object sender, MouseButtonEventArgs e)
        {
            if (this.isDragging)
            {
                // Say we are done dragging
                this.isDragging = false;
                this.canvasContainer.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// Called when the user moves there mouse. If
        /// they have the mouse button pressed then we
        /// pan.
        /// </summary>
        private void canvasContainer_MouseMove(
            object sender, MouseEventArgs e)
        {
            if (this.isDragging)
            {
                // Calculate the new drag distance
                Point newPosition = e.GetPosition(this.canvasRoot);
                Point newPoint = new Point(newPosition.X - offset.X, newPosition.Y - offset.Y);

                // Set the values
                this.canvasContainer.SetValue(Canvas.LeftProperty, newPoint.X);
                this.canvasContainer.SetValue(Canvas.TopProperty, newPoint.Y);

                //var animationDuration = TimeSpan.FromSeconds(.1);

                //Translator.BeginAnimation(TranslateTransform.XProperty,
                //    new DoubleAnimation(newPosition.X, new Duration(animationDuration)));

                //Translator.BeginAnimation(TranslateTransform.YProperty,
                //    new DoubleAnimation(newPosition.Y, new Duration(animationDuration))); 


                AdjustCanvasSize();
            }
        }

        private void AdjustCanvasSize()
        {
            var x = Convert.ToDouble(this.canvasContainer.GetValue(Canvas.LeftProperty));
            var y = Convert.ToDouble(this.canvasContainer.GetValue(Canvas.TopProperty));

            if (Double.IsNaN(x)) x = 0.0;
            if (Double.IsNaN(y)) y = 0.0;

            var ww = x + this.canvasContainer.Width * this.Scaler.ScaleX;
            if (ww < this.scrollRoot.RenderSize.Width) ww = this.scrollRoot.RenderSize.Width;

            var hh = y + this.canvasContainer.Height * this.Scaler.ScaleY;
            if (hh < this.scrollRoot.RenderSize.Height) hh = this.scrollRoot.RenderSize.Height;

            this.canvasRoot.Width = ww;
            this.canvasRoot.Height = hh;
        }

        private void scrollRoot_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double mouseDelta = Math.Sign(e.Delta);

            // calculate the new scale for the canvas
            double newScale = this.Scaler.ScaleX + (mouseDelta * .2);

            // Don't allow scrolling too big or small
            if (newScale < 0.1 || newScale > 50) return;

            // Set the zoom!
            this.Scaler.ScaleX = newScale;
            this.Scaler.ScaleY = newScale;

            //var animationDuration = TimeSpan.FromSeconds(.2);
            //var scaleAnimate = new DoubleAnimation(newScale, new Duration(animationDuration));

            //this.Scaler.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimate);
            //this.Scaler.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimate);

            AdjustCanvasSize();
        }

        #endregion

        const int MAX_COMMITS = 200;
        const int PADDING = 50;
        const int BOX_HEIGHT = 120;
        const int BOX_WIDTH = 300;

        private Repository repository;

        internal void Show(Repository repository)
        {
            this.repository = repository;

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action act = () =>
            {
                canvasContainer.Children.Clear();

                var head = repository.Resolve("HEAD");
                if (head == null) return;

                var pw = new PlotWalk(repository);
                var heads = repository.GetAllRefs().Values.Select(r =>
                    pw.LookupCommit(repository.Resolve(r.GetObjectId().Name))).ToList();
                pw.MarkStart(heads);
                PlotCommitList<PlotLane> pcl = new PlotCommitList<PlotLane>();
                pcl.Source(pw);
                pcl.FillTo(MAX_COMMITS);

                var commits = pcl.ToList();
                var maxX = commits.Count();
                var maxY = commits.Max(c => c.GetLane().GetPosition());

                for (int i = commits.Count() - 1; i >= 0; i--)
                {
                    var commit = commits[i];

                    #region Add commit links

                    int idx = 0;
                    for (int n = 0; n < commit.ParentCount; n++)
                    {
                        var pid = commit.GetParent(n).Id;
                        var parent = commits.Where(c => c.Id == pid)
                            .OrderBy(c=>c.GetLane().GetPosition())
                            .FirstOrDefault();

                        if (parent != null)
                        {
                            // current node
                            double x1 = i;
                            double y1 = commit.GetLane().GetPosition();

                            // parent node
                            double x2 = commits.IndexOf(parent);
                            double y2 = parent.GetLane().GetPosition();

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
                                idx++;
                            }
                            else if(idx == 0)
                            {
                                var path = new Path
                                {
                                    Stroke = new SolidColorBrush(Color.FromArgb(255, 153, 182, 209)),
                                    StrokeThickness = 4,
                                };
                                
                                var x3 = x2 - CommitBox.WIDTH / 2;

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
                                idx++;
                            }
                            else 
                            {
                                var path = new Path
                                {
                                    Stroke = new SolidColorBrush(Color.FromArgb(255, 153, 182, 209)),
                                    StrokeThickness = 4,
                                };

                                var x3 = x1 + CommitBox.WIDTH / 2;

                                PathSegmentCollection pscollection = new PathSegmentCollection();

                                BezierSegment curve = new BezierSegment(
                                    new Point(x3, y1), new Point(x3, y2), new Point(x1, y2), true);
                                pscollection.Add(curve);

                                pscollection.Add(new LineSegment(new Point(x2, y2), true));

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
                                idx++;
                            }
                        }
                    }
                    #endregion

                    #region Add commit box
                    var box = new CommitBox();
                    box.DataContext = new
                    {
                            Id = commit.Id.Name.Substring(0, 5),
                            Comments = commit.GetShortMessage(),
                            Author = commit.GetAuthorIdent().GetName(),
                            Date = "",
                    };

                    double left = GetScreenX(maxX - i);
                    double top = GetScreenY(commit.GetLane().GetPosition());

                    Canvas.SetLeft(box, left);
                    Canvas.SetTop(box, top);
                    Canvas.SetZIndex(box, 10);

                    this.canvasContainer.Children.Add(box);

                    #endregion

                }

                this.canvasRoot.Width =
                this.canvasContainer.Width = PADDING * 2 + (maxX + 1) * BOX_WIDTH;

                this.canvasRoot.Height = PADDING * 2 + (maxY + 1) * BOX_HEIGHT;
                this.canvasContainer.Height = 300;

                AdjustCanvasSize();
            };

            dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
        }

        private double GetScreenX(double x)
        {
            return PADDING + (x-1) * BOX_WIDTH;
        }

        private double GetScreenY(double y)
        {
            return PADDING + y * BOX_HEIGHT;
        }

    }
}
