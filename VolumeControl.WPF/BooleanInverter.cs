﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace VolumeControl.WPF
{
    /// <summary>
    /// <see cref="Binding"/> converter for boolean types that negates the expression.
    /// </summary>
    public class BooleanInverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !System.Convert.ToBoolean(value);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !System.Convert.ToBoolean(value);
    }
}
