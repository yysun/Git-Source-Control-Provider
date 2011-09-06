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
        const int PADDING = 20;
        const int BOX_HEIGHT = 50;
        const int BOX_WIDTH = 90;
        const int BOX_BORDER_WIDTH = 4;
        const int BOX_RADIUS = 6;
        const int BOX_HSPACE = 80;
        const int BOX_VSPACE = 80;

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

                var  pw = new PlotWalk(repository);
                var heads = repository.GetAllRefs().Values.Select(r =>
                    pw.LookupCommit(repository.Resolve(r.GetObjectId().Name))).ToList();
                pw.MarkStart(heads);
                PlotCommitList<PlotLane> pcl = new PlotCommitList<PlotLane>();
                pcl.Source(pw);               
                pcl.FillTo(MAX_COMMITS);

                var commits = pcl.ToArray();

                for (int i = commits.Count() - 1; i >= 0; i--)
                {
                    var commit = commits[i];

                    var grid = new Grid { ToolTip = commit.GetShortMessage() };

                    var rect = new Rectangle
                    {
                        Width = BOX_WIDTH,
                        Height = BOX_HEIGHT,
                        RadiusX = BOX_RADIUS,
                        RadiusY = BOX_RADIUS,
                        Stroke = new SolidColorBrush(Color.FromArgb(200, 0, 128, 0)),
                        StrokeThickness = BOX_BORDER_WIDTH,
                        Fill = new SolidColorBrush(Color.FromArgb(120, 180, 255, 120)),
                    };

                    double left = PADDING + (commits.Count() - i - 1) * (BOX_WIDTH + BOX_HSPACE);
                    double top = PADDING + commit.GetLane().GetPosition() * BOX_VSPACE;

                    Canvas.SetLeft(grid, left);
                    Canvas.SetTop(grid, top);

                    grid.Children.Add(rect);
                    grid.Children.Add(new TextBlock 
                    { 
                        Text = commit.Id.Name.Substring(0, 5),
                        Width = BOX_WIDTH,
                        FontSize = 20,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                    });

                    this.canvasContainer.Children.Add(grid);
                }

                this.canvasRoot.Width =
                this.canvasContainer.Width = PADDING * 2 + commits.Count() * (BOX_WIDTH + BOX_HSPACE);

                this.canvasRoot.Height =
                this.canvasContainer.Height = 300;

                AdjustCanvasSize();

            };

            dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
        }
    }
}
