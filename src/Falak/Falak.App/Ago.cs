using System;
using System.Globalization;

namespace Falak.App;

internal static class Ago
{
    public static string Format(DateTime when)
    {
        TimeSpan timeSpan = DateTime.Now - when;
        if (!UiText.IsArabic)
        {
            if (timeSpan.TotalSeconds < 45) return "Just now";
            if (timeSpan.TotalMinutes < 1.5) return "1 minute ago";
            if (timeSpan.TotalMinutes < 60) return $"{(int)Math.Round(timeSpan.TotalMinutes)} minutes ago";
            if (timeSpan.TotalHours < 24) return $"Today at {when:HH:mm}";
            return when.ToString("d MMM, HH:mm", CultureInfo.GetCultureInfo("en-US"));
        }
        if (timeSpan.TotalSeconds < 45) return "الآن";
        if (timeSpan.TotalMinutes < 1.5) return "قبل دقيقة";
        if (timeSpan.TotalMinutes < 2.5) return "قبل دقيقتين";
        if (timeSpan.TotalMinutes < 11) return $"قبل {(int)Math.Round(timeSpan.TotalMinutes)} دقائق";
        if (timeSpan.TotalMinutes < 60) return $"قبل {(int)Math.Round(timeSpan.TotalMinutes)} دقيقة";
        if (timeSpan.TotalHours < 24) return $"اليوم {when:HH:mm}";
        return when.ToString("d MMM، HH:mm", new CultureInfo("ar-SA")
        {
            DateTimeFormat = { Calendar = new GregorianCalendar() }
        });
    }
}
