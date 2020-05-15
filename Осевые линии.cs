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
using System.Collections;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        static readonly TSD.DrawingHandler drawingHandler = new TSD.DrawingHandler();
        static readonly TSM.Model model = new TSM.Model();
		
		public static void Run(Tekla.Technology.Akit.IScript akit)
        {
			 var currentDrawing = drawingHandler.GetActiveDrawing();

            DrawingObjectEnumerator parts = GetSelectedParts();

            foreach (var obj in parts)
            {
                var part = obj as TSD.Part;

                if (part != null)
                {
                    ViewBase view = part.GetView();
                    CenterLinesDrawer cld = new CenterLinesDrawer(part, view);
                    if (GetPartProfileType(part).Contains("R"))
                    {
                        cld.SetDrawerMethod(new RoundPartCenterLinesDrawer());
                    }
                    else
                    if (!GetPartProfileType(part).Contains("B"))
                    {
                        cld.SetDrawerMethod(new ProfilePartCenterLinesDrawer());                       
                    }
                    else
                    if (SelectModelObject(part.ModelIdentifier.ID) is TSM.ContourPlate || GetPartProfileType(part).Contains("B"))
                    {
                        cld.SetDrawerMethod(new PlateCenterLinesDrawer());
                    }

                    cld.DrawPartCenterLines();
                }
            }

            currentDrawing.CommitChanges();	          
        }
		 internal static DrawingObjectEnumerator GetSelectedParts()
        {
            Drawing activeDrawing = drawingHandler.GetActiveDrawing();
            ViewBase sheet = activeDrawing.GetSheet();
            DrawingObjectEnumerator objects = sheet.GetAllObjects();
            objects.Reset();
            DrawingObjectSelector dos = drawingHandler.GetDrawingObjectSelector();
            objects = dos.GetSelected();
            return objects;
        }
        
        internal static string GetPartProfileType(TSD.Part drawingPart)
        {
            TSM.Part modelPart = SelectModelObject(drawingPart.ModelIdentifier.ID)
                as TSM.Part;
            string profileType = String.Empty;
            modelPart.GetReportProperty("PROFILE_TYPE", ref profileType);
            return profileType;
        }        
        internal static TSM.ModelObject SelectModelObject(int objectId)
        {
            TSM.ModelObject result = null;
            TSM.ModelObject modelObject =
                new TSM.Model().SelectModelObject(new Identifier(objectId));
            result = modelObject;
            return result;
        }
     }
	  class CenterLinesDrawer
    {        
        TSD.Part _drawingPart;
        ViewBase _viewBase;              
        private IDrawCenterLines _drawCenterLines;
        double _partWidth;
        double _partHeight;
        double _partLength;        
        double[] _partStart;
        double[] _partEnd;
        CoordinateSystem _partCS;
        
        public CenterLinesDrawer(TSD.Part drawingPart, ViewBase viewBase)
        {
            _drawingPart = drawingPart;
            _viewBase = viewBase;            
        }        
        public void SetDrawerMethod(IDrawCenterLines drawCenterLines)
        {
            this._drawCenterLines = drawCenterLines;
        }
        public void DrawPartCenterLines()
        {
            ProcessPart();

            this._drawCenterLines.DrawCenterLines(_viewBase, _partStart, _partEnd,
                _partWidth, _partHeight, _partLength, _partCS);

        }
                
        /// <summary>
        /// Возможно, использование этого метода - не лучшее решение и нужно было написать
        /// отдельный класс для процессинга парта
        /// </summary>
        internal void ProcessPart()
        {
            TSM.Part modelPart = 
                Extensions.GetModelObjectFromDrawingObject(_drawingPart) as TSM.Part;

            _partCS = modelPart.GetCoordinateSystem();

            modelPart.GetReportProperty("WIDTH", ref _partWidth);
            modelPart.GetReportProperty("HEIGHT", ref _partHeight);
            modelPart.GetReportProperty("LENGTH", ref _partLength);

            var partCLPoints = modelPart.GetCenterLine(false);
            Point partStart = partCLPoints[0] as Point;
            Point partEnd;

            // Двойная проверка: и в main, и здесь
            if (!(modelPart is TSM.ContourPlate))
            {
                partEnd = partCLPoints[partCLPoints.Count - 1] as Point;
            }
            else
            {
                // Это сработает только с 4-хугольными пластинами, расположеными перпеникулярно плоскости вида
                partEnd = partCLPoints[partCLPoints.Count - 2] as Point;
            }
            _partStart = new double[3] { partStart.X, partStart.Y, partStart.Z };
            _partEnd = new double[3] { partEnd.X, partEnd.Y, partEnd.Z };

        }
    }    

    interface IDrawCenterLines
    {
        /// <summary>
        /// Основная проблема в том, что некоторым классам нужны не все аргументы
        /// Возможно, стратегия - не лучший паттерн для решения задачи и надо реализовать фабрику
        /// </summary>
        /// <param name="view"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="length"></param>
        /// <param name="partCS"></param>
        void DrawCenterLines(ViewBase view, 
            double[] start, double[] end,
            double width, double height, 
            double length,
            CoordinateSystem partCS);
    }

    class PlateCenterLinesDrawer : IDrawCenterLines
    {
        public void DrawCenterLines(ViewBase view, double[] start, double[] end, 
            double width, double height, double length, CoordinateSystem partCS)
        {
            // Дублирование кода
            double scale = ((View)view).Attributes.Scale;
            double delta = 2 * scale;

            // Бессмысленное действие: представление точки как массива 
            // в классе CenterLinesDrawer и обратно, как точки здесь
            Point startPoint = new Point(start[0], start[1], start[2]);
            Point endPoint = new Point(end[0], end[1], end[2]);

            // Дублирование кода
            Point o =
                new Point((end[0] + start[0]) / 2, (end[1] + start[1]) / 2, (end[2] + start[2]) / 2);
            // Вспомогательный LineSegment
            LineSegment auxLS = new LineSegment(o, endPoint);
            // Вспомогательный вектор направления
            Vector auxVec = auxLS.GetDirectionVector();
            // Векторы для растягивания конечных точек за пределы солида
            Vector clVec = Extensions.ExtendedVector(auxVec, delta);
            Vector nclVec = (-1) * clVec;
            // Растягивание конечных точек за пределы солида
            startPoint.Translate(nclVec.X, nclVec.Y, nclVec.Z);
            endPoint.Translate(clVec.X, clVec.Y, clVec.Z);

            Drawer.DrawLine(view, startPoint, endPoint);  
        }
    }

    class ProfilePartCenterLinesDrawer : IDrawCenterLines
    {
        public void DrawCenterLines(ViewBase view, double[] start, double[] end,
            double width, double height, double length, CoordinateSystem partCS)
        {
            // Дублирование кода
            double scale = ((View)view).Attributes.Scale;
            double delta = 2 * scale;

            //Point o = partCS.Origin;
            Point o =
                new Point((end[0] + start[0]) / 2, (end[1] + start[1]) / 2, (end[2] + start[2]) / 2);
            var x = new Point(o);
            var y = new Point(o);
            var z = new Point(o);

            var nx = new Point(o);
            var ny = new Point(o);
            var nz = new Point(o);

            Vector rX = Extensions.RoundVector(partCS.AxisX.GetNormal()) * length * 0.5;
            Vector rY = Extensions.RoundVector(partCS.AxisY.GetNormal()) * height * 0.5;
            Vector rZ = 
                Extensions.RoundVector(Vector.Cross(partCS.AxisX, partCS.AxisY).GetNormal()) * width * 0.5;

            rX = Extensions.ExtendedVector(rX, delta);
            rY = Extensions.ExtendedVector(rY, delta);
            rZ = Extensions.ExtendedVector(rZ, delta);

            Vector nrX = (-1) * rX;
            Vector nrY = (-1) * rY;
            Vector nrZ = (-1) * rZ;

            x.Translate(rX.X, rX.Y, rX.Z);
            y.Translate(rY.X, rY.Y, rY.Z);
            z.Translate(rZ.X, rZ.Y, rZ.Z);

            nx.Translate(nrX.X, nrX.Y, nrX.Z);
            ny.Translate(nrY.X, nrY.Y, nrY.Z);
            nz.Translate(nrZ.X, nrZ.Y, nrZ.Z);

            Drawer.DrawLine(view, nx, x);
            Drawer.DrawLine(view, ny, y);
            Drawer.DrawLine(view, nz, z);
        }
    }

    class RoundPartCenterLinesDrawer : IDrawCenterLines
    {
        public void DrawCenterLines(ViewBase view, double[] start, double[] end,
            double width, double height, double length, CoordinateSystem partCS)
        {
            // Дублирование кода
            double scale = ((View)view).Attributes.Scale;
            double delta = Math.Max(width, height)/2+2*scale;

            Point partCenter = 
                new Point((end[0]+start[0])/2,(end[1]+start[1])/2, (end[2]+start[2])/2);

            Point iStart = new Point(partCenter.X + delta, partCenter.Y, partCenter.Z);
            Point iEnd = new Point(partCenter.X - delta, partCenter.Y, partCenter.Z);
            
            Drawer.DrawLine(view, iStart, iEnd);
             
            Point jStart = new Point(partCenter.X, partCenter.Y + delta, partCenter.Z);
            Point jEnd = new Point(partCenter.X, partCenter.Y- delta, partCenter.Z);

            Drawer.DrawLine(view, jStart, jEnd); 
            
            Point kStart = new Point(partCenter.X, partCenter.Y, partCenter.Z + delta);
            Point kEnd = new Point(partCenter.X, partCenter.Y, partCenter.Z - delta);

            Drawer.DrawLine(view, kStart, kEnd);
        }
    }

    class Drawer
    {
        internal static void DrawLine(ViewBase view, Point start, Point end)
        {
            if (start != null && end != null)
            {
                TSD.Line line = new TSD.Line(view,
                    Extensions.TransformPointToDisplay(start, view),
                    Extensions.TransformPointToDisplay(end, view));

                TSD.Line.LineAttributes la = new TSD.Line.LineAttributes();
                la.Line.Type = LineTypes.DashDot;
                la.Line.Color = DrawingColors.Black;
                line.Attributes = la;

                line.Insert();
                line.Modify();
            }
        }     
    }

    class Extensions
    {
        internal static TSM.ModelObject GetModelObjectFromDrawingObject(TSD.ModelObject drawingObject)
        {
            TSM.ModelObject modelObject = 
                (new TSM.Model()).SelectModelObject(drawingObject.ModelIdentifier);            
            return modelObject;
        }
        
        internal static Point TransformPointToDisplay(Point point, ViewBase view)
        {
            if (point != null)
            {
                CoordinateSystem display = ((View)view).DisplayCoordinateSystem;

                Matrix matrix = MatrixFactory.ToCoordinateSystem(display);
                Point result = matrix.Transform(point);
                return result;
            }
            return null;
        } 
        internal static Vector RoundVector(Point vector)
        {
            var result = new Vector
            {
                X = Math.Round(vector.X, 5),
                Y = Math.Round(vector.Y, 5),
                Z = Math.Round(vector.Z, 5)
            };
            return result;
        }
        internal static Vector ExtendedVector(Vector v, double ext)
        {
            double vLen = Math.Sqrt(Math.Pow(v.X, 2) + Math.Pow(v.Y, 2) + Math.Pow(v.Z, 2));
            Vector u = new Vector(v.X / vLen, v.Y / vLen, v.Z / vLen);

            return new Vector(u.X * (vLen + ext), u.Y * (vLen + ext), u.Z * (vLen + ext));
        }        
    }
 }
