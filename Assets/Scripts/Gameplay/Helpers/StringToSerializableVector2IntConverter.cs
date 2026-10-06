using System;
using System.ComponentModel;
using System.Globalization;
using Hearthglade.Gameplay.Common;

namespace Hearthglade.Gameplay.Helpers
{
    public class StringToSerializableVector2IntConverter : TypeConverter {

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            var casted = value as string;
            var xy = casted.Substring(1, casted.Length-2).Trim(' ').Split(",");
            return xy != null
                ? new SerializableVector2Int(int.Parse(xy[0]), int.Parse(xy[1]))
                : base.ConvertFrom(context, culture, value);
        }
    }
}