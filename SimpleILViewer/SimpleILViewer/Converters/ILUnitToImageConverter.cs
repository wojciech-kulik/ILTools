using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SimpleILViewer.Converters
{
    public class ILUnitToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var itemType = ((ItemType)value).ToString().ToLower();
            return new BitmapImage(new Uri(String.Format("/SimpleILViewer;component/Images/{0}.png", itemType), UriKind.Relative));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
