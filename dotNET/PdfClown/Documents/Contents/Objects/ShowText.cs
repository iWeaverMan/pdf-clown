/*
  Copyright 2007-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Documents.Contents.Fonts;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfClown.Documents.Contents.Scanner;
using System.Text;
using PdfClown.Util.Math.Geom;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>Abstract 'show a text string' operation [PDF:1.6:5.3.2].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public abstract class ShowText : Operation
    {
        #region types
        public interface IScanner
        {
            /**
              <summary>Notifies the scanner about a text character.</summary>
              <param name="textChar">Scanned character.</param>
              <param name="textCharBox">Bounding box of the scanned character.</param>
            */
            void ScanChar(char textChar, Quad textCharBox);
        }
        public TextStringWrapper textString;
        #endregion

        #region dynamic
        #region constructors
        protected ShowText(string @operator) : base(@operator)
        { }

        protected ShowText(string @operator, params PdfDirectObject[] operands) : base(@operator, operands)
        { }

        protected ShowText(string @operator, IList<PdfDirectObject> operands) : base(@operator, operands) { }
        #endregion

        #region interface
        #region public

        /**
         <summary>Gets/Sets the encoded text.</summary>
         <remarks>Text is expressed in native encoding: to resolve it to Unicode, pass it
         to the decode method of the corresponding font.</remarks>
       */
        public abstract byte[] Text
        {
            get;
            set;
        }

        /**
          <summary>Gets/Sets the encoded text elements along with their adjustments.</summary>
          <remarks>Text is expressed in native encoding: to resolve it to Unicode, pass it
          to the decode method of the corresponding font.</remarks>
          <returns>Each element can be either a byte array or a number:
            <list type="bullet">
              <item>if it's a byte array (encoded text), the operator shows text glyphs;</item>
              <item>if it's a number (glyph adjustment), the operator inversely adjusts the next glyph position
              by that amount (that is: a positive value reduces the distance between consecutive glyphs).</item>
            </list>
          </returns>
        */
        public virtual IList<object> Value
        {
            get => new List<object>() { Text };
            set => Text = (byte[])value[0];
        }

        public override void Scan(GraphicsState state)
        {
            if (textString == null)
                textString = new TextStringWrapper(state.Scanner, false);
            textString.TextChars.Clear();
            Scan(state, new TextStringWrapper.ShowTextScanner(textString));
        }

        /**
          <summary>Executes scanning on this operation.</summary>
          <param name="state">Graphics state context.</param>
          <param name="textScanner">Scanner to be notified about text contents.
          In case it's null, the operation is applied to the graphics state context.</param>
        */
        public virtual void Scan(GraphicsState state, IScanner textScanner)
        {
            /*
              TODO: I really dislike this solution -- it's a temporary hack until the event-driven
              parsing mechanism is implemented...
            */
            /*
              TODO: support to vertical writing mode.
            */

            double contextHeight = state.Scanner.ContextSize.Height;
            Font font = state.Font ?? Font.LatestFont;
            if (font == null)
                return;
            double fontSize = state.FontSize;
            double scaledFactor = Font.GetScalingFactor(fontSize) * state.Scale;
            bool wordSpaceSupported = !(font is CompositeFont);
            double wordSpace = wordSpaceSupported ? state.WordSpace * state.Scale : 0;
            double charSpace = state.CharSpace * state.Scale;
            SKMatrix ctm = state.Ctm;
            SKMatrix tm = state.TextState.Tm;
            //var encoding = font.GetEnoding();
            var context = state.Scanner.RenderContext;
            var fill = context != null && state.RenderModeFill ? state.FillColorSpace?.GetPaint(state.FillColor, state.FillAlpha) : null;
            var typeface = font?.GetTypeface();
            var nameTypeface = font?.GetTypefaceByName();
            if (context != null)
            {
                context.Save();
            }

            if (fill != null)
            {
                fill.TextSize = (float)state.FontSize;
                fill.TextScaleX = (float)state.Scale;
            }

            var stroke = context != null && state.RenderModeStroke ? state.StrokeColorSpace?.GetPaint(state.StrokeColor, state.StrokeAlpha) : null;
            if (stroke != null)
            {
                stroke.Typeface = typeface;
                stroke.TextSize = (float)state.FontSize;
                stroke.TextScaleX = (float)state.Scale;
                stroke.Style = SKPaintStyle.Stroke;
                stroke.StrokeWidth = (float)state.LineWidth;
                stroke.StrokeCap = state.LineCap.ToSkia();
                stroke.StrokeJoin = state.LineJoin.ToSkia();
                stroke.StrokeMiter = (float)state.MiterLimit;
            }
            if (this is ShowTextToNextLine showTextToNextLine)
            {
                double? newWordSpace = showTextToNextLine.WordSpace;
                if (newWordSpace != null)
                {
                    //if (textScanner == null)
                    { state.WordSpace = newWordSpace.Value; }
                    if (wordSpaceSupported)
                    { wordSpace = newWordSpace.Value * state.Scale; }
                }
                double? newCharSpace = showTextToNextLine.CharSpace;
                if (newCharSpace != null)
                {
                    //if (textScanner == null)
                    { state.CharSpace = newCharSpace.Value; }
                    charSpace = newCharSpace.Value * state.Scale;
                }
                tm = state.TextState.Tlm;
                SKMatrix.PreConcat(ref tm, new SKMatrix { Values = new float[] { 1, 0, 0, 0, 1, (float)-state.Lead, 0, 0, 1 } });
            }
            else
            { tm = state.TextState.Tm; }

            foreach (object textElement in Value)
            {
                if (textElement is byte[] byteElement) // Text string.
                {
                    string textString = font.Decode(byteElement);

                    foreach (char textChar in textString)
                    {
                        //NOTE: The text rendering matrix is recomputed before each glyph is painted
                        // during a text-showing operation.
                        SKMatrix trm = ctm;
                        SKMatrix.PreConcat(ref trm, tm);
                        SKMatrix.PreConcat(ref trm, SKMatrix.MakeScale(1, -1));

                        if (context != null
                            && !(textString.Length == 1
                            && (textString[0] == ' '
                            || textString[0] == '\r'
                            || textString[0] == '\n'
                            || char.IsControl(textString[0])
                            )))
                        {
                            var text = font is Type1Font
                                ? System.Text.Encoding.UTF8.GetBytes(new[] { textChar })
                                //: font is Type1Font && typeface != null
                                //? BitConverter.GetBytes(font.GetGlyph(textChar))
                                : font is Type2Font
                                ? System.Text.Encoding.UTF8.GetBytes(new[] { textChar })
                                : font.Encode(textChar.ToString());
                            context.SetMatrix(trm);
                            if (fill != null)
                            {
                                fill.Typeface = typeface;
                                if (fill.ContainsGlyphs(text))
                                {
                                    context.DrawText(text, 0, 0, fill);
                                }
                                else if (typeface != nameTypeface)
                                {
                                    fill.Typeface = nameTypeface;
                                    context.DrawText(text, 0, 0, fill);
                                }
                                else
                                { }
                            }

                            if (stroke != null)
                            {
                                context.DrawText(text, 0, 0, stroke);
                            }
                        }
                        double charWidth = font.GetWidth(textChar) * scaledFactor;

                        if (textScanner != null)
                        {
                            var charBox = SKRect.Create(0, (float)(-font.GetAscent(fontSize)), (float)charWidth, (float)font.GetHeight(textChar, fontSize));
                            var quad = new Quad(charBox);
                            quad.Transform(ref trm);
                            textScanner.ScanChar(textChar, quad);
                        }
                        /*
                          NOTE: After the glyph is painted, the text matrix is updated
                          according to the glyph displacement and any applicable spacing parameter.
                        */
                        SKMatrix.PreConcat(ref tm, SKMatrix.MakeTranslation((float)(charWidth + charSpace + (textChar == ' ' ? wordSpace : 0)), 0));
                    }
                }
                else // Text position adjustment.
                {
                    SKMatrix.PreConcat(ref tm, SKMatrix.MakeTranslation((float)(-Convert.ToSingle(textElement) * scaledFactor), 0));
                }
            }
            if (context != null)
            {
                context.Restore();
            }

            //if (textScanner == null)
            {
                state.TextState.Tm = tm;

                if (this is ShowTextToNextLine)
                { state.TextState.Tlm = tm; }
            }
        }


        #endregion
        #endregion
        #endregion
    }
}