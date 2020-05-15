using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tekla.Structures;
using TSD = Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;
using Tekla.Structures.Drawing.UI;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Drawing.Tools;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;

namespace Tekla.Technology.Akit.UserScript
{
	public class Script
    {		
		static readonly TSD.DrawingHandler drawingHandler = new TSD.DrawingHandler();
		static readonly TSM.Model model = new TSM.Model();
		
		public static void Run(Tekla.Technology.Akit.IScript akit)
        {
			 var currentDrawing = drawingHandler.GetActiveDrawing();
			 TSD.ViewBase view;
            Point point;
            TSD.Grid grid;
            GetPickedObjects(out grid, out view, out point);                

                NeighborLabelDrawer neighborLabelDrawer = new NeighborLabelDrawer(point, view, grid);

                if(!neighborLabelDrawer.IsParallelToX && 
                    neighborLabelDrawer.IsStartLabel)
                neighborLabelDrawer.SetLabelLocation(new DrawBottomXLabel());

                if (neighborLabelDrawer.IsParallelToX &&
                    neighborLabelDrawer.IsStartLabel)
                    neighborLabelDrawer.SetLabelLocation(new DrawLeftYLabel());

                if (!neighborLabelDrawer.IsParallelToX &&
                    !neighborLabelDrawer.IsStartLabel)
                    neighborLabelDrawer.SetLabelLocation(new DrawUpperXLabel());

                if (neighborLabelDrawer.IsParallelToX &&
                    !neighborLabelDrawer.IsStartLabel)
                    neighborLabelDrawer.SetLabelLocation(new DrawRightYLabel());


                neighborLabelDrawer.DrawNeighborLabel();
				currentDrawing.CommitChanges();
        }
		
		internal static void GetPickedObjects(out TSD.Grid grid, out TSD.ViewBase view, out Point point)
		{
			Picker picker = drawingHandler.GetPicker();
			TSD.DrawingObject drawingObj;
			picker.PickObject("Выберите ось", out drawingObj, out view);
			grid = drawingObj as TSD.Grid;
			picker.PickPoint("Выберите точку", out point, out view);
		}
	}
	class NeighborLabelDrawer
    {
        private Point _pickedPoint;
        private TSD.ViewBase _viewBase;        
        private TSD.Grid _fatherGrid;        
        private GridLine _relatedGridLine;        
        private bool _isStartLabel;
        private bool _isParallelToX;
        private double _frameHeight;
        private double _frameWidth;
        private string _nextLabelText;
        private string _prevLabelText;
        private IDrawLabel _drawNeighborLabel;

        public Point PickedPoint { get { return _pickedPoint;} set { _pickedPoint = value; }}
        public ViewBase ViewBase { get { return _viewBase;} set { _viewBase = value; }}
        public TSD.Grid FatherGrid { get { return _fatherGrid;} set { _fatherGrid = value; }}
        public GridLine RelatedGridLine { get { return _relatedGridLine;} set { _relatedGridLine = value; }}
        public bool IsStartLabel { get { return _isStartLabel;} set { _isStartLabel = value; }}
        public bool IsParallelToX { get { return _isParallelToX;} set { _isParallelToX = value; }}       
        public double FrameHeight { get { return _frameHeight;} set { _frameHeight = value; }}
        public double FrameWidth { get { return _frameWidth;} set { _frameWidth = value; }}
        public string NextLabelText { get { return _nextLabelText;} set { _nextLabelText = value; }}
        public string PrevLabelText { get { return _prevLabelText;} set { _prevLabelText = value; }}
        internal IDrawLabel DrawNeighborLabel1 { get { return _drawNeighborLabel;} set { _drawNeighborLabel = value; }}
        

        public NeighborLabelDrawer(Point pickedPoint, ViewBase viewBase, TSD.Grid grid)
        {
            _pickedPoint = pickedPoint;
            _viewBase = viewBase;
            _fatherGrid = grid;
            GetRelatedGridLineProperties();
            FindNextAndPrevLabel();
            GetOrientation();
        }

        public void SetLabelLocation(IDrawLabel drawNeighborLabel)
        {
            this._drawNeighborLabel = drawNeighborLabel;
        }
        public void DrawNeighborLabel()
        {
            this._drawNeighborLabel.DrawLabel(_pickedPoint, _viewBase, _frameHeight, _frameWidth, _nextLabelText, _prevLabelText);
        }
        protected void GetRelatedGridLineProperties()
        {
            double distBetweenPointAndStart, distBetweenPointAndEnd;
            Dictionary<GridLine, double> lengths = new Dictionary<GridLine, double>();
            Dictionary<GridLine, bool> startLabels = new Dictionary<GridLine, bool>();

            var gridLines = _fatherGrid.GetObjects();
            while (gridLines.MoveNext())
            {
                /* Поиск расстояния между выбранной точкой и концами оси */
                GridLine gl = gridLines.Current as GridLine;
                distBetweenPointAndStart = Distance.PointToPoint(_pickedPoint, gl.StartLabel.CenterPoint);
                distBetweenPointAndEnd = Distance.PointToPoint(_pickedPoint, gl.EndLabel.CenterPoint);
                /* Заполнение словаря: Ось-Минимальное расстояние до выбранной точки */
                lengths.Add(gl, Math.Min(distBetweenPointAndEnd, distBetweenPointAndStart));

                /* Определение близки ли мы к началу оси */
                if (distBetweenPointAndEnd < distBetweenPointAndStart)
                {
                    _isStartLabel = false;
                }
                else
                {
                    _isStartLabel = true;
                }
                startLabels.Add(gl, _isStartLabel);
            }

            /* 
             * Поиск ближайшей оси через сортировку словаря 
             * Для малого количества объектов - норм
             */
            var minKeyValue = lengths.OrderBy(kvp => kvp.Value).First();
            _relatedGridLine = minKeyValue.Key;

            /* Близость к началу оси на ближайшей оси */
            _isStartLabel = startLabels[_relatedGridLine];

            /* Размеры рамки */
            if (_isStartLabel)
            {
                _frameHeight = _relatedGridLine.StartLabel.FrameHeight;
                _frameWidth = _relatedGridLine.StartLabel.FrameWidth;
            }
            else
            {
                _frameHeight = _relatedGridLine.EndLabel.FrameHeight;
                _frameWidth = _relatedGridLine.EndLabel.FrameWidth;
            }

        }
        protected void GetOrientation()
        {
            /* Поиск направления оси X гридлайна */
            LineSegment gridLineAxisX = new LineSegment(_relatedGridLine.StartLabel.CenterPoint,
                _relatedGridLine.EndLabel.CenterPoint);

            TSM.Grid modelGrid = (TSM.Grid)Extensions.SelectModelObject(_fatherGrid.ModelIdentifier.ID);
            CoordinateSystem gridCS = modelGrid.GetCoordinateSystem();

            Vector gridLineX = gridLineAxisX.GetDirectionVector();
            Vector gridX = gridCS.AxisX;
            /* Определение параллельности оси X гридлайна оси грида  */
            if (Tekla.Structures.Geometry3d.Parallel.VectorToVector(gridLineX, gridX)) _isParallelToX = true;
            else
                _isParallelToX = false;
        }
        protected void FindNextAndPrevLabel()
        {
            TSM.Grid modelGrid = Extensions.SelectModelObject(_fatherGrid.ModelIdentifier.ID) as TSM.Grid;
            string gridLabelsX = modelGrid.LabelX;
            string gridLabelsY = modelGrid.LabelY;

            string[] labelsX = gridLabelsX.Split(null);
            string[] labelsY = gridLabelsY.Split(null);

            string nextLabel, prevLabel, curLabel;
            if (!String.IsNullOrEmpty(_relatedGridLine.StartLabel.GridLabelText))
                curLabel = _relatedGridLine.StartLabel.GridLabelText;
            else
                curLabel = _relatedGridLine.EndLabel.GridLabelText;

            Extensions.FindNextAndPreviousLabelText(curLabel, labelsX, labelsY, out prevLabel, out nextLabel);

            _nextLabelText = nextLabel;
            _prevLabelText = prevLabel;
        }
    }
    interface IDrawLabel
    {
        void DrawLabel(Point pickedPoint, TSD.ViewBase viewBase, 
            double frameHeight, double frameWidth,
            string nextLabel, string prevLabel);
    }
    /*       
     *       
     *       (⌐■_■)–︻╦╤─ * * * * *
     *       
     */
    class DrawBottomXLabel : IDrawLabel
    {
        void IDrawLabel.DrawLabel(Point pickedPoint, TSD.ViewBase viewBase,
            double frameHeight, double frameWidth,
            string nextLabel, string prevLabel)
        {
            if (!String.IsNullOrEmpty(nextLabel))
            {
                Point startToNext = new Point(pickedPoint.X + frameWidth / 2, pickedPoint.Y - frameHeight / 2);
                Point endToNext = new Point(pickedPoint.X + 1.5 * frameWidth, pickedPoint.Y - frameHeight / 2);
                Point textNext = new Point(endToNext.X + frameWidth / 2, endToNext.Y);
                Drawer.InsertLine(viewBase, startToNext, endToNext);
                Drawer.InsertText(viewBase, textNext, nextLabel);
            }
            if (!String.IsNullOrEmpty(prevLabel))
            {
                Point startToPrev = new Point(pickedPoint.X - frameWidth / 2, pickedPoint.Y - frameHeight / 2);
                Point endToPrev = new Point(pickedPoint.X - 1.5 * frameWidth, pickedPoint.Y - frameHeight / 2);
                Point textPrev = new Point(endToPrev.X - frameWidth / 2, endToPrev.Y);
                Drawer.InsertLine(viewBase, startToPrev, endToPrev);
                Drawer.InsertText(viewBase, textPrev, prevLabel);
            }
        }

    }
    class DrawLeftYLabel : IDrawLabel
    {
        void IDrawLabel.DrawLabel(Point pickedPoint, TSD.ViewBase viewBase,
            double frameHeight, double frameWidth,
            string nextLabel, string prevLabel)
        {
            if (!String.IsNullOrEmpty(nextLabel))
            {
                Point startToNext = new Point(pickedPoint.X - frameWidth / 2, pickedPoint.Y + frameHeight / 2);
                Point endToNext = new Point(pickedPoint.X - frameWidth / 2, pickedPoint.Y + 1.5 * frameHeight);
                Point textNext = new Point(endToNext.X, endToNext.Y + frameHeight / 2);
                Drawer.InsertLine(viewBase, startToNext, endToNext);
                Drawer.InsertText(viewBase, textNext, nextLabel);
            }
            if (!String.IsNullOrEmpty(prevLabel))
            {
                Point startToPrev = new Point(pickedPoint.X - frameWidth / 2, pickedPoint.Y - frameHeight / 2);
                Point endToPrev = new Point(pickedPoint.X - frameWidth / 2, pickedPoint.Y - 1.5 * frameHeight);
                Point textPrev = new Point(endToPrev.X, endToPrev.Y - frameHeight / 2);
                Drawer.InsertLine(viewBase, startToPrev, endToPrev);
                Drawer.InsertText(viewBase, textPrev, prevLabel);
            }
        }
    }
    class DrawUpperXLabel : IDrawLabel
    {
        void IDrawLabel.DrawLabel(Point pickedPoint, TSD.ViewBase viewBase,
            double frameHeight, double frameWidth,
            string nextLabel, string prevLabel)
        {
            if (!String.IsNullOrEmpty(nextLabel))
            {
                Point startToNext = new Point(pickedPoint.X + frameWidth / 2, pickedPoint.Y + frameHeight / 2);
                Point endToNext = new Point(pickedPoint.X + 1.5 * frameWidth, pickedPoint.Y + frameHeight / 2);
                Point textNext = new Point(endToNext.X + frameWidth / 2, endToNext.Y);
                Drawer.InsertLine(viewBase, startToNext, endToNext);
                Drawer.InsertText(viewBase, textNext, nextLabel);
            }
            if (!String.IsNullOrEmpty(prevLabel))
            {
                Point startToPrev = new Point(pickedPoint.X - frameWidth / 2, pickedPoint.Y + frameHeight / 2);
                Point endToPrev = new Point(pickedPoint.X - 1.5 * frameWidth, pickedPoint.Y + frameHeight / 2);
                Point textPrev = new Point(endToPrev.X - frameWidth / 2, endToPrev.Y);
                Drawer.InsertLine(viewBase, startToPrev, endToPrev);
                Drawer.InsertText(viewBase, textPrev, prevLabel);
            }
        }
    }
    class DrawRightYLabel : IDrawLabel
    {
        void IDrawLabel.DrawLabel(Point pickedPoint, TSD.ViewBase viewBase,
            double frameHeight, double frameWidth,
            string nextLabel, string prevLabel)
        {
            if (!String.IsNullOrEmpty(nextLabel))
            {
                Point startToNext = new Point(pickedPoint.X + frameWidth / 2, pickedPoint.Y + frameHeight / 2);
                Point endToNext = new Point(pickedPoint.X + frameWidth / 2, pickedPoint.Y + 1.5 * frameHeight);
                Point textNext = new Point(endToNext.X, endToNext.Y + frameHeight / 2);
                Drawer.InsertLine(viewBase, startToNext, endToNext);
                Drawer.InsertText(viewBase, textNext, nextLabel);
            }
            if (!String.IsNullOrEmpty(prevLabel))
            {
                Point startToPrev = new Point(pickedPoint.X + frameWidth / 2, pickedPoint.Y - frameHeight / 2);
                Point endToPrev = new Point(pickedPoint.X + frameWidth / 2, pickedPoint.Y - 1.5 * frameHeight);
                Point textPrev = new Point(endToPrev.X, endToPrev.Y - frameHeight / 2);
                Drawer.InsertLine(viewBase, startToPrev, endToPrev);
                Drawer.InsertText(viewBase, textPrev, prevLabel);
            }
        }
    }
    class Drawer
    {
        internal static void InsertLine(ViewBase view, Point start, Point end)
        {
            TSD.Line line = new TSD.Line(view, start, end);
            TSD.Line.LineAttributes lineAttributes = new TSD.Line.LineAttributes();
            lineAttributes.Arrowhead = 
                new ArrowheadAttributes(ArrowheadPositions.End, ArrowheadTypes.FilledArrow, 2, 3);
            lineAttributes.Line = 
                new LineTypeAttributes(LineTypes.SolidLine, DrawingColors.Black);
            line.Attributes = lineAttributes;
            line.Insert();
            line.Modify();
        }
        internal static void InsertText(ViewBase view, Point point, string label)
        {
            try
            {
                string font = String.Empty;
                double circleDiam = 0.0;
                double scale = ((View)view).Attributes.Scale;
                
                TeklaStructuresSettings.GetAdvancedOption("XS_DIMENSION_FONT", ref font);
                TeklaStructuresSettings.GetAdvancedOption("XS_DRAWING_GRID_LABEL_FRAME_FIXED_WIDTH", ref circleDiam);

                Text text = new Text(view, point, label);
                Text.TextAttributes textAttributes = new Text.TextAttributes();
                textAttributes.Font = new FontAttributes(DrawingColors.Black, 5, font, false, false);                
                text.Attributes = textAttributes;

                if (circleDiam == 0) circleDiam = 5*scale;
                Circle circle = new Circle(view, point, 0.5*circleDiam*scale);
                Circle.CircleAttributes circleAttributes = new Circle.CircleAttributes();
                circleAttributes.Line.Color = DrawingColors.Black;
                circle.Attributes = circleAttributes;

                text.Insert();
                text.Modify();
                circle.Insert();
                circle.Modify();
            }
            catch { }
        }
    }

    class Extensions
    {
        internal static TSM.ModelObject SelectModelObject(int objectId)
        {
            TSM.ModelObject result = null;
            TSM.ModelObject modelObject =
                new TSM.Model().SelectModelObject(new Identifier(objectId));
            result = modelObject;
            return result;
        }

        internal static void FindNextAndPreviousLabelText(string curLabel, string[] labelsX, string[] labelsY, out string previous, out string next)
        {
            previous = null;
            next = null;

            if (labelsX.Contains(curLabel))
            {
                try
                {
                    int labelIndex = Array.FindIndex(labelsX, l => l.Contains(curLabel));
                    next = labelsX[labelIndex + 1];
                    previous = labelsX[labelIndex - 1];
                }
                catch { }
            }
            else if (labelsY.Contains(curLabel))
            {
                try
                {
                    int labelIndex = Array.FindIndex(labelsY, l => l.Contains(curLabel));
                    next = labelsY[labelIndex + 1];
                    previous = labelsY[labelIndex - 1];
                }
                catch { }
            }
        }
    }
}
