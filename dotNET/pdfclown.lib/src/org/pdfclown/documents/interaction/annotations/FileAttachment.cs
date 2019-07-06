/*
  Copyright 2008-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using org.pdfclown.bytes;
using org.pdfclown.documents;
using org.pdfclown.documents.files;
using org.pdfclown.objects;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace org.pdfclown.documents.interaction.annotations
{
    /**
      <summary>File attachment annotation [PDF:1.6:8.4.5].</summary>
      <remarks>It represents a reference to a file, which typically is embedded in the PDF file.
      </remarks>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class FileAttachment
      : Markup,
        IFileResource
    {
        #region types
        /**
          <summary>Icon to be used in displaying the annotation [PDF:1.6:8.4.5].</summary>
        */
        public enum IconTypeEnum
        {
            /**
              <summary>Graph.</summary>
            */
            Graph,
            /**
              <summary>Paper clip.</summary>
            */
            PaperClip,
            /**
              <summary>Push pin.</summary>
            */
            PushPin,
            /**
              <summary>Tag.</summary>
            */
            Tag
        };
        #endregion

        #region static
        #region fields
        private static readonly Dictionary<IconTypeEnum, PdfName> IconTypeEnumCodes;

        private static readonly IconTypeEnum DefaultIconType = IconTypeEnum.PushPin;
        #endregion

        #region constructors
        static FileAttachment()
        {
            IconTypeEnumCodes = new Dictionary<IconTypeEnum, PdfName>();
            IconTypeEnumCodes[IconTypeEnum.Graph] = PdfName.Graph;
            IconTypeEnumCodes[IconTypeEnum.PaperClip] = PdfName.Paperclip;
            IconTypeEnumCodes[IconTypeEnum.PushPin] = PdfName.PushPin;
            IconTypeEnumCodes[IconTypeEnum.Tag] = PdfName.Tag;
        }
        #endregion

        #region interface
        #region private
        /**
          <summary>Gets the code corresponding to the given value.</summary>
        */
        private static PdfName ToCode(IconTypeEnum value)
        { return IconTypeEnumCodes[value]; }

        /**
          <summary>Gets the icon type corresponding to the given value.</summary>
        */
        private static IconTypeEnum ToIconTypeEnum(PdfName value)
        {
            foreach (KeyValuePair<IconTypeEnum, PdfName> iconType in IconTypeEnumCodes)
            {
                if (iconType.Value.Equals(value))
                    return iconType.Key;
            }
            return DefaultIconType;
        }
        #endregion
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public FileAttachment(Page page, SKRect box, string text, FileSpecification dataFile)
            : base(page, PdfName.FileAttachment, box, text)
        { DataFile = dataFile; }

        internal FileAttachment(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the icon to be used in displaying the annotation.</summary>
        */
        public IconTypeEnum IconType
        {
            get { return ToIconTypeEnum((PdfName)BaseDataObject[PdfName.Name]); }
            set { BaseDataObject[PdfName.Name] = value != DefaultIconType ? ToCode(value) : null; }
        }

        #region IFileResource
        public FileSpecification DataFile
        {
            get { return FileSpecification.Wrap(BaseDataObject[PdfName.FS]); }
            set { BaseDataObject[PdfName.FS] = value.BaseObject; }
        }
        #endregion
        #endregion
        #endregion
        #endregion
    }
}