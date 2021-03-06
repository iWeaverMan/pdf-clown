/*
  Copyright 2009-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Util;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PdfClown.Documents.Contents.Fonts
{
    /**
      <summary>AFM file format parser [AFM:4.1].</summary>
    */
    public sealed class AfmParser
    {
        #region types
        /**
          <summary>Font header (Global font information).</summary>
        */
        public sealed class FontMetrics
        {
            public int Ascender;
            public int CapHeight;
            public int Descender;
            public string FontName;
            public bool IsCustomEncoding;
            public bool IsFixedPitch;
            public float ItalicAngle;
            public int UnderlinePosition;
            public int UnderlineThickness;
            public int StemH;
            public int StemV;
            public string Weight;
            public int XHeight;
            public int XMax;
            public int XMin;
            public int YMax;
            public int YMin;
        }
        #endregion

        #region dynamic
        #region fields
        public FontMetrics Metrics;

        public Dictionary<int, int> GlyphIndexes;
        public Dictionary<int, int> GlyphKernings;
        public Dictionary<int, int> GlyphWidths;

        public IInputStream FontData;
        #endregion

        #region constructors
        internal AfmParser(IInputStream fontData)
        {
            FontData = fontData;

            Load();
        }
        #endregion

        #region interface
        #region private
        private void Load()
        {
            Metrics = new FontMetrics();
            LoadFontHeader();
            LoadCharMetrics();
            LoadKerningData();
        }

        /**
          <summary>Loads the font header [AFM:4.1:3,4.1-4.4].</summary>
        */
        private void LoadFontHeader()
        {
            string line;
            Regex linePattern = new Regex("(\\S+)\\s+(.+)");
            while ((line = FontData.ReadLine()) != null)
            {
                MatchCollection lineMatches = linePattern.Matches(line);
                if (lineMatches.Count < 1)
                    continue;

                Match lineMatch = lineMatches[0];

                string key = lineMatch.Groups[1].Value;
                if (key.Equals("Ascender", StringComparison.Ordinal))
                { Metrics.Ascender = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("CapHeight", StringComparison.Ordinal))
                { Metrics.CapHeight = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("Descender", StringComparison.Ordinal))
                { Metrics.Descender = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("EncodingScheme", StringComparison.Ordinal))
                { Metrics.IsCustomEncoding = lineMatch.Groups[2].Value.Equals("FontSpecific", StringComparison.Ordinal); }
                else if (key.Equals("FontBBox", StringComparison.Ordinal))
                {
                    string[] coordinates = Regex.Split(lineMatch.Groups[2].Value, "\\s+");
                    Metrics.XMin = ConvertUtils.ParseAsIntInvariant(coordinates[0]);
                    Metrics.YMin = ConvertUtils.ParseAsIntInvariant(coordinates[1]);
                    Metrics.XMax = ConvertUtils.ParseAsIntInvariant(coordinates[2]);
                    Metrics.YMax = ConvertUtils.ParseAsIntInvariant(coordinates[3]);
                }
                else if (key.Equals("FontName", StringComparison.Ordinal))
                { Metrics.FontName = lineMatch.Groups[2].Value; }
                else if (key.Equals("IsFixedPitch", StringComparison.Ordinal))
                { Metrics.IsFixedPitch = Boolean.Parse(lineMatch.Groups[2].Value); }
                else if (key.Equals("ItalicAngle", StringComparison.Ordinal))
                { Metrics.ItalicAngle = ConvertUtils.ParseFloatInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("StdHW", StringComparison.Ordinal))
                { Metrics.StemH = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("StdVW", StringComparison.Ordinal))
                { Metrics.StemV = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("UnderlinePosition", StringComparison.Ordinal))
                { Metrics.UnderlinePosition = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("UnderlineThickness", StringComparison.Ordinal))
                { Metrics.UnderlineThickness = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("Weight", StringComparison.Ordinal))
                { Metrics.Weight = lineMatch.Groups[2].Value; }
                else if (key.Equals("XHeight", StringComparison.Ordinal))
                { Metrics.XHeight = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value); }
                else if (key.Equals("StartCharMetrics", StringComparison.Ordinal))
                    break;
            }
            if (Metrics.Ascender == 0)
            { Metrics.Ascender = Metrics.YMax; }
            if (Metrics.Descender == 0)
            { Metrics.Descender = Metrics.YMin; }
        }

        /**
          <summary>Loads individual character metrics [AFM:4.1:3,4,4.4,8].</summary>
        */
        private void LoadCharMetrics()
        {
            GlyphIndexes = new Dictionary<int, int>();
            GlyphWidths = new Dictionary<int, int>();

            string line;
            Regex linePattern = new Regex("C (\\S+) ; WX (\\S+) ; N (\\S+)");
            int implicitCharCode = short.MaxValue;
            while ((line = FontData.ReadLine()) != null)
            {
                MatchCollection lineMatches = linePattern.Matches(line);
                if (lineMatches.Count < 1)
                {
                    if (line.Equals("EndCharMetrics"))
                        break;

                    continue;
                }

                Match lineMatch = lineMatches[0];

                int charCode = ConvertUtils.ParseIntInvariant(lineMatch.Groups[1].Value);
                int width = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[2].Value);
                string charName = lineMatch.Groups[3].Value;
                if (charCode < 0)
                {
                    if (charName == null)
                        continue;

                    charCode = ++implicitCharCode;
                }
                int code = (
                  charName == null
                      || Metrics.IsCustomEncoding
                    ? charCode
                    : GlyphMapping.Default.NameToCode(charName).Value
                  );

                GlyphIndexes[code] = charCode;
                GlyphWidths[charCode] = width;
            }
        }

        /**
          <summary>Loads kerning data [AFM:4.1:3,4,4.5,9].</summary>
        */
        private void LoadKerningData()
        {
            GlyphKernings = new Dictionary<int, int>();

            string line;
            while ((line = FontData.ReadLine()) != null)
            {
                if (line.StartsWith("StartKernPairs"))
                    break;
            }

            Regex linePattern = new Regex("KPX (\\S+) (\\S+) (\\S+)");
            while ((line = FontData.ReadLine()) != null)
            {
                MatchCollection lineMatches = linePattern.Matches(line);
                if (lineMatches.Count < 1)
                {
                    if (line.Equals("EndKernPairs"))
                        break;

                    continue;
                }

                Match lineMatch = lineMatches[0];

                int code1 = GlyphMapping.Default.NameToCode(lineMatch.Groups[1].Value).Value;
                int code2 = GlyphMapping.Default.NameToCode(lineMatch.Groups[2].Value).Value;
                int pair = code1 << 16 + code2;
                int value = ConvertUtils.ParseAsIntInvariant(lineMatch.Groups[3].Value);

                GlyphKernings[pair] = value;
            }
        }
        #endregion
        #endregion
        #endregion
    }
}