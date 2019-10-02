using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XamlFSExplorer
{
    public class ExplorerGridView : GridView
    {
        static double _sizeSize = MeasureString("Size").Width + 16;
        static double _typeSize = MeasureString("Type").Width + 16;
        static double _modifiedSize = MeasureString("Modified").Width + 16;
        static double _nameSize = MeasureString("Name").Width + 16;

        public void ItemAdded(object item)
        {
            if (item is FSExplorerItem fseItem)
            {
                double w = 0;
                for (int i =0; i< Columns.Count; i++)
                {
                    var column = Columns[i];
                    switch (i)
                    {
                        case 0:
                            w = MeasureString(fseItem.Name).Width + 56;
                            w = Math.Max(w, _nameSize);
                            break;
                        case 1:
                            w = MeasureString(fseItem.LastModified).Width + 16;
                            w = Math.Max(w, _modifiedSize);
                            break;
                        case 2:
                            w = MeasureString(fseItem.TypeName).Width + 16;
                            w = Math.Max(w, _typeSize);
                            break;
                        case 3:
                            w = MeasureString(fseItem.Size).Width + 16;
                            w = Math.Max(w, _sizeSize);
                            break;
                    }
                    
                    if (w > column.Width) column.Width = w ;
                }
            }
        }

        public void OnCollectionReset()
        {
            foreach (var col in Columns) col.Width = 0;
        }

        private static Size MeasureString(string candidate)
        {
            if (candidate == null) return Size.Empty;
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal),
                SystemFonts.MessageFontSize,
                Brushes.Black);

            return new Size(formattedText.Width, formattedText.Height);
        }
    }
}
