using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Artemis.App;

public sealed class BoolToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? "✔" : "✘";

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
