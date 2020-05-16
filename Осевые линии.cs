/* 
Eugeny Leschenko
NIP-Informatica, 2020
 */

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
		public static void Run(Tekla.Technology.Akit.IScript akit)
        {
			TSD.DrawingHandler drawingHandler = new TSD.DrawingHandler();
            var currentDrawing = drawingHandler.GetActiveDrawing();

            DrawingObjectEnumerator parts = Extensions.GetSelectedParts();

            foreach (var obj in parts)
            {
                var part = obj as TSD.Part;

                if (part != null)
                {
                    ViewBase view = part.GetView();
                    CenterLinesDrawer drawer = new CenterLinesDrawer(part, view);
                    // если деталь - труба или пруток
                    if (Extensions.GetPartProfileType(part).Contains("R")) 
                    {
                        drawer.SelectDrawerMethod(new RoundBeamCenterLinesDrawer());
                    }                    
                    else
                    // если деталь - пластина
                    if (Extensions.SelectModelObject(part.ModelIdentifier.ID) is TSM.ContourPlate || 
                        Extensions.GetPartProfileType(part).Contains("B"))
                    {
                        drawer.SelectDrawerMethod(new PlateCenterLinesDrawer());
                    }
                    else
                    {
                        drawer.SelectDrawerMethod(new BeamCenterLinesDrawer());
                    }
                    drawer.DrawPartCenterLines();
                }
            }
            currentDrawing.CommitChanges();        
        }
    }

    class CenterLinesDrawer
    {             
        private IDrawerMethod _drawCenterLines;        
        TsPart _tsPart;
        TsView _tsView;
        
        public CenterLinesDrawer(TSD.Part drawingPart, ViewBase viewBase)
        {            
            _tsPart = new TsPart(drawingPart);
            _tsView = new TsView(viewBase);
        }        
        public void SelectDrawerMethod(IDrawerMethod drawCenterLines)
        {
            this._drawCenterLines = drawCenterLines;
        }
        public void DrawPartCenterLines()
        {    
            this._drawCenterLines.DrawCenterLines(_tsPart, _tsView);
        }
    }    

    interface IDrawerMethod
    {
        void DrawCenterLines(TsPart tsPart, TsView tsView);
    }
    
    class PlateCenterLinesDrawer : IDrawerMethod
    {
        public void DrawCenterLines(TsPart tsPart, TsView tsView)
        {
            
            double scale = tsView.scale;
            double delta = 2 * scale;

            double[] start = tsPart.partStart;
            double[] end = tsPart.partEnd;

            Point o = tsPart.origin;
            
            Point startPoint = new Point(start[0], start[1], start[2]);
            Point endPoint = new Point(end[0], end[1], end[2]);
            
            LineSegment auxLS = new LineSegment(o, endPoint);
            
            Vector auxVec = auxLS.GetDirectionVector();
            
            Vector clVec = Extensions.ExtendVector(auxVec, delta);
            Vector nclVec = (-1) * clVec;
            
            startPoint.Translate(nclVec.X, nclVec.Y, nclVec.Z);
            endPoint.Translate(clVec.X, clVec.Y, clVec.Z);

            Inserter.InsertLine(tsView._viewBase, startPoint, endPoint);  
        }
    }

    class BeamCenterLinesDrawer : IDrawerMethod
    {
        public void DrawCenterLines(TsPart tsPart, TsView tsView)
        {
            
            double scale = tsView.scale;
            double delta = 2 * scale;
            
            Point o =tsPart.origin;

            CoordinateSystem partCS = tsPart.partCS;

            double width = tsPart.width;
            double height = tsPart.height;
            double length = tsPart.length;

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

            rX = Extensions.ExtendVector(rX, delta);
            rY = Extensions.ExtendVector(rY, delta);
            rZ = Extensions.ExtendVector(rZ, delta);

            Vector nrX = (-1) * rX;
            Vector nrY = (-1) * rY;
            Vector nrZ = (-1) * rZ;

            x.Translate(rX.X, rX.Y, rX.Z);
            y.Translate(rY.X, rY.Y, rY.Z);
            z.Translate(rZ.X, rZ.Y, rZ.Z);

            nx.Translate(nrX.X, nrX.Y, nrX.Z);
            ny.Translate(nrY.X, nrY.Y, nrY.Z);
            nz.Translate(nrZ.X, nrZ.Y, nrZ.Z);

            ViewBase view = tsView._viewBase;

            Inserter.InsertLine(view, nx, x);
            Inserter.InsertLine(view, ny, y);
            Inserter.InsertLine(view, nz, z);
        }
    }

    class RoundBeamCenterLinesDrawer : IDrawerMethod
    {
        public void DrawCenterLines(TsPart tsPart, TsView tsView)
        {
            
            double scale = tsView.scale;
            double width = tsPart.width;
            double height = tsPart.height;
            ViewBase view = tsView._viewBase;
            
            double delta = Math.Max(width, height)/2+2*scale;
            Point partCenter = tsPart.origin;

            Point iStart = new Point(partCenter.X + delta, partCenter.Y, partCenter.Z);
            Point iEnd = new Point(partCenter.X - delta, partCenter.Y, partCenter.Z);
            
            Inserter.InsertLine(view, iStart, iEnd);
             
            Point jStart = new Point(partCenter.X, partCenter.Y + delta, partCenter.Z);
            Point jEnd = new Point(partCenter.X, partCenter.Y- delta, partCenter.Z);

            Inserter.InsertLine(view, jStart, jEnd); 
            
            Point kStart = new Point(partCenter.X, partCenter.Y, partCenter.Z + delta);
            Point kEnd = new Point(partCenter.X, partCenter.Y, partCenter.Z - delta);

            Inserter.InsertLine(view, kStart, kEnd);
        }
    }

    class Inserter
    {
        internal static void InsertLine(ViewBase view, Point start, Point end)
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
    public class TsPart
    {
        public TSD.Part _drawingPart;
        public readonly double width;
        public readonly double height;
        public readonly double length;
        public readonly double[] partStart;
        public readonly double[] partEnd;
        public readonly CoordinateSystem partCS;
        public Point origin;

        public TsPart(TSD.Part drawingPart)
        {
            _drawingPart = drawingPart;
            TSM.Part modelPart =
               Extensions.GetModelObjectFromDrawingObject(_drawingPart) as TSM.Part;

            modelPart.GetReportProperty("WIDTH", ref width);
            modelPart.GetReportProperty("HEIGHT", ref height);
            modelPart.GetReportProperty("LENGTH", ref length);

            var partCLPoints = modelPart.GetCenterLine(false);
            Point partStartP = partCLPoints[0] as Point;
            Point partEndP;
            
            if (!(modelPart is TSM.ContourPlate))
            {
                partEndP = partCLPoints[partCLPoints.Count - 1] as Point;
            }
            else
            {                
                partEndP = partCLPoints[partCLPoints.Count - 2] as Point;
            }
            partStart = new double[3] { partStartP.X, partStartP.Y, partStartP.Z };
            partEnd = new double[3] { partEndP.X, partEndP.Y, partEndP.Z };

            partCS = modelPart.GetCoordinateSystem();
            origin =
                new Point((partEnd[0] + partStart[0]) / 2, 
                (partEnd[1] + partStart[1]) / 2, 
                (partEnd[2] + partStart[2]) / 2);
        }
    }

    public class TsView
    {
        public ViewBase _viewBase;
        public double scale;

        public TsView(ViewBase viewBase)
        {
            _viewBase = viewBase;
            scale = ((View)_viewBase).Attributes.Scale;
        }
    }
    class Extensions
    {
        internal static DrawingObjectEnumerator GetSelectedParts()
        {
            TSD.DrawingHandler drawingHandler = new TSD.DrawingHandler();
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
        internal static Vector ExtendVector(Vector v, double ext)
        {
            double vLen = Math.Sqrt(Math.Pow(v.X, 2) + Math.Pow(v.Y, 2) + Math.Pow(v.Z, 2));
            Vector u = new Vector(v.X / vLen, v.Y / vLen, v.Z / vLen);

            return new Vector(u.X * (vLen + ext), u.Y * (vLen + ext), u.Z * (vLen + ext));
        }
    } 	 
 }