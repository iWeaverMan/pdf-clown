/*
  Copyright 2008-2012 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)

  This file should be part of the source code distribution of "PDF Clown library" (the
  Program): see the accompanying README files for more info.

  This Program is free software; you can redistribute it and/or modify it under the terms
  of the GNU Lesser General Public License as published by the Free Software Foundation;
  either version 3 of the License, or (at your option) any later version.

  This Program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
  either expressed or implied; without even the implied warranty of MERCHANTABILITY or
  FITNESS FOR A PARTICULAR PURPOSE. See the License for more details.

  You should have received a copy of the GNU Lesser General Public License along with this
  Program (see README files); if not, go to the GNU website (http://www.gnu.org/licenses/).

  Redistribution and use, with or without modification, are permitted provided that such
  redistributions retain the above copyright notice, license and disclaimer, along with
  this list of conditions.
*/

using PdfClown.Bytes;
using PdfClown.Documents;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Linq;
using PdfClown.Util.Math.Geom;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Abstract vertexed shape annotation.</summary>
    */
    [PDF(VersionEnum.PDF15)]
    public abstract class VertexShape : Shape
    {
        private SKPoint[] points;
        private Dictionary<int, IndexControlPoint> cache = new Dictionary<int, IndexControlPoint>();
        #region dynamic
        #region constructors
        protected VertexShape(Page page, SKRect box, string text, PdfName subtype)
            : base(page, box, text, subtype)
        { }

        protected VertexShape(PdfDirectObject baseObject)
            : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the coordinates of each vertex.</summary>
        */
        public SKPoint[] Points
        {
            get
            {
                if (points == null)
                {
                    PdfArray verticesObject = Vertices;

                    var pageMatrix = PageMatrix;
                    var length = verticesObject.Count;
                    points = new SKPoint[length / 2];
                    for (int i = 0, j = 0; i < length; i += 2, j++)
                    {
                        var mappedPoint = pageMatrix.MapPoint(new SKPoint(
                            ((IPdfNumber)verticesObject[i]).FloatValue,
                            ((IPdfNumber)verticesObject[i + 1]).FloatValue));
                        points[j] = mappedPoint;
                    }
                }
                return points;
            }
            set
            {
                if (points != value)
                {
                    points = value;
                }
                var pageMatrix = InvertPageMatrix;
                PdfArray verticesObject = new PdfArray();
                float pageHeight = Page.Box.Height;
                foreach (SKPoint vertex in value)
                {
                    var mappedPoint = pageMatrix.MapPoint(vertex);
                    verticesObject.Add(PdfReal.Get(mappedPoint.X));
                    verticesObject.Add(PdfReal.Get(mappedPoint.Y));
                }

                Vertices = verticesObject;
            }
        }

        public PdfArray Vertices
        {
            get => (PdfArray)BaseDataObject[PdfName.Vertices];
            set => BaseDataObject[PdfName.Vertices] = value;
        }

        public SKPoint this[int index]
        {
            get => Points[index];
            set
            {
                Points[index] = value;
                Points = points;
                RefreshBox();
            }
        }

        public SKPoint FirstPoint
        {
            get => Points.Length == 0 ? SKPoint.Empty : points[0];
            set
            {
                if (Points.Length > 0)
                {
                    points[0] = value;
                    Points = points;
                }
            }
        }

        public SKPoint LastPoint
        {
            get => Points.Length == 0 ? SKPoint.Empty : points[points.Length - 1];
            set
            {
                if (Points.Length > 0)
                {
                    points[points.Length - 1] = value;
                    Points = points;
                    RefreshBox();
                }
            }
        }

        public void AddPoint(SKPoint point)
        {
            var oldVertices = Points;
            var newVertices = new SKPoint[oldVertices.Length + 1];
            Array.Copy(oldVertices, newVertices, oldVertices.Length);
            newVertices[newVertices.Length - 1] = point;
            Points = newVertices;
        }

        public override void RefreshBox()
        {
            Appearance.Normal[null] = null;
            SKRect box = SKRect.Empty;
            foreach (SKPoint point in Points)
            {
                if (box == SKRect.Empty)
                { box = SKRect.Create(point.X, point.Y, 0, 0); }
                else
                { box.Add(point); }

            }
            Box = box;
        }


        public override void DrawSpecial(SKCanvas canvas)
        {
            using (var path = new SKPath())
            {
                path.AddPoly(Points.ToArray());
                path.Close();
                DrawPath(canvas, path);
            }
        }

        public override void MoveTo(SKRect newBox)
        {
            var oldBox = Box;
            //base.MoveTo(newBox);
            var dif = SKMatrix.MakeIdentity();
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeTranslation(newBox.MidX, newBox.MidY));
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeScale(newBox.Width / oldBox.Width, newBox.Height / oldBox.Height));
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeTranslation(-oldBox.MidX, -oldBox.MidY));
            for (int i = 0; i < Points.Length; i++)
            {
                points[i] = dif.MapPoint(points[i]);
            }
            Points = points;
            base.MoveTo(newBox);
        }

        public override IEnumerable<ControlPoint> GetControlPoints()
        {
            for (int i = 0; i < Points.Length; i++)
            {
                yield return cache.TryGetValue(i, out var controlPoint) ? controlPoint
                    : (cache[i] = new IndexControlPoint { Annotation = this, Index = i });
            }
            foreach (var cpBase in GetDefaultControlPoint())
            {
                yield return cpBase;
            }
        }
        #endregion
        #endregion
        #endregion
    }

    public class IndexControlPoint : ControlPoint
    {
        public VertexShape VertexShape => (VertexShape)Annotation;

        public int Index { get; set; }

        public override SKPoint Point
        {
            get => VertexShape[Index];
            set => VertexShape[Index] = value;
        }
    }
}