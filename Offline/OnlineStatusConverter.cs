using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Offline
{
    public class OnlineStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var isConnected = (bool)value;
            var connectedImage = string.Empty;

            if (isConnected)
            {
                connectedImage = "http://static.arcgis.com/images/Symbols/Shapes/GreenCircleLargeB.png";
            }
            else
            {
                connectedImage = "http://static.arcgis.com/images/Symbols/Shapes/RedCircleLargeB.png";
            }

            return connectedImage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
