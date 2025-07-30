using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;

namespace Bubbles4.Models;

public static class FileAssessor
{
    static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };

    static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rar", ".cbr", ".7z", ".zip", ".cbz", ".tar", ".tgz", ".epub"
    };

    static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    public static bool IsImage(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsArchive(string path)
    {
        return ArchiveExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsPdf(string path)
    {
        return PdfExtensions.Contains(Path.GetExtension(path));
    }
    
    public static bool IsImageDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return false;

            return Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Any(IsImage);
        }
        catch
        {
            return false;
        }
    }
    
    public static bool IsDescendantPath(string parentPath, string childPath)
    {
        var parentUri = new Uri(AppendDirectorySeparator(parentPath), UriKind.Absolute);
        var childUri = new Uri(AppendDirectorySeparator(childPath), UriKind.Absolute);
        return parentUri.IsBaseOf(childUri);
    }
    private static string AppendDirectorySeparator(string path)
    {
        return path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())
            ? path
            : path + System.IO.Path.DirectorySeparatorChar;
    }
    
    public static bool CouldBeDirectory(string path)
    {
        return string.IsNullOrEmpty(Path.GetExtension(path));
    }
    
    public static bool IsWatchable(string path)
    {
        if(IsImageDirectory(path) || IsArchive(path) || IsPdf(path) || IsImage(path))
            return true;
        
        if (CouldBeDirectory(path) && Directory.Exists(path)) 
            return true;
        
        return false;
    }
    public static bool IsFileReady(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch
        {
            return false;
        }
    }


public static DateTime? TryExtractDate(string input)
    {
        foreach (var strategy in _strategies)
        {
            var transformed = strategy(input);
            var date = TryParseWithRecognizer(transformed);
            if (date != null)
                return date;

            // Fallback for compact numeric
            var regexDate = TryRegexFallback(transformed);
            if (regexDate != null)
                return regexDate;
        }

        return null;
    }

    // Chain of preprocessing functions
    private static readonly List<Func<string, string>> _strategies = new()
    {
        // 1. Raw input
        s => s,

        // 2. Replace underscores, dashes, dots with space
        s => Regex.Replace(s, @"[-_.]", " "),

        // 3. Strip non-alphanum except space
        s => Regex.Replace(s, @"[^a-zA-Z0-9 ]+", ""),

        // 4. Collapse multiple spaces
        s => Regex.Replace(s, @"\s+", " ").Trim()
    };

    private static DateTime? TryParseWithRecognizer(string input)
    {
        var results = DateTimeRecognizer.RecognizeDateTime(input, Culture.English);

        foreach (var result in results)
        {
            if (result.Resolution?.TryGetValue("values", out var values) == true &&
                values is List<Dictionary<string, string>> list)
            {
                foreach (var entry in list)
                {
                    if (entry.TryGetValue("value", out var value) &&
                        DateTime.TryParse(value, out var dt))
                    {
                        return dt;
                    }
                }
            }
        }

        return null;
    }

    private static DateTime? TryRegexFallback(string input)
    {
        var match = Regex.Match(input, @"(?<!\d)(\d{4})(\d{2})(\d{2})(?!\d)");
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out int year) &&
            int.TryParse(match.Groups[2].Value, out int month) &&
            int.TryParse(match.Groups[3].Value, out int day))
        {
            try
            {
                return new DateTime(year, month, day);
            }
            catch { }
        }

        return null;
    }
    private static readonly HashSet<string> MonthNames = new HashSet<string>(
        new[] {
            "january", "february", "march", "april", "may", "june",
            "july", "august", "september", "october", "november", "december"
        });
    
    public static DateTime MergeDateAndTime(DateTime datePart, DateTime timePart)
    {
        return new DateTime(
            datePart.Year,
            datePart.Month,
            datePart.Day,
            timePart.Hour,
            timePart.Minute,
            timePart.Second,
            timePart.Millisecond,
            timePart.Kind  // preserves local/UTC/unspecified kind
        );
    }
}