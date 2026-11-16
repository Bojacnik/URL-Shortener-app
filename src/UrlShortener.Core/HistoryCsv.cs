using System.Globalization;
using System.Text;

namespace UrlShortener.Core;

/// <summary>
/// History as CSV (RFC 4180), for backups and moving to another device.
/// Columns: created_at, provider, short_url, long_url, pinned. Opens fine in Excel, LibreOffice and Google Sheets.
/// </summary>
public static class HistoryCsv
{
    private static readonly string[] Columns = ["created_at", "provider", "short_url", "long_url", "pinned"];

    public static string Export(IEnumerable<HistoryEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Columns)).Append("\r\n");
        foreach (var e in entries)
        {
            sb.Append(Escape(e.CreatedAt.ToString("O", CultureInfo.InvariantCulture))).Append(',')
              .Append(Escape(e.ProviderId)).Append(',')
              .Append(Escape(e.ShortUrl)).Append(',')
              .Append(Escape(e.LongUrl)).Append(',')
              .Append(e.Pinned ? "true" : "false").Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Reads a CSV made by <see cref="Export"/> (or edited in a spreadsheet). Columns are found by name, so their
    /// order doesn't matter. Rows without a valid short and long link are skipped.
    /// </summary>
    /// <exception cref="FormatException">Not a history CSV (no short_url / long_url columns).</exception>
    public static List<HistoryEntry> Import(string csv)
    {
        var rows = Parse(csv.TrimStart('﻿'));
        if (rows.Count == 0) throw new FormatException("The file is empty.");

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(string name) => header.IndexOf(name);
        int shortCol = Col("short_url"), longCol = Col("long_url"), dateCol = Col("created_at"),
            providerCol = Col("provider"), pinnedCol = Col("pinned");
        if (shortCol < 0 || longCol < 0)
            throw new FormatException("The file has no short_url and long_url columns.");

        var result = new List<HistoryEntry>();
        foreach (var row in rows.Skip(1))
        {
            string Cell(int i) => i >= 0 && i < row.Count ? row[i].Trim() : "";
            if (!IsHttpUrl(Cell(shortCol)) || !IsHttpUrl(Cell(longCol))) continue;

            var created = DateTimeOffset.TryParse(Cell(dateCol), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d)
                ? d : DateTimeOffset.Now;
            var provider = Cell(providerCol) is { Length: > 0 } p ? p : new Uri(Cell(shortCol)).Host;
            var pinned = Cell(pinnedCol).Equals("true", StringComparison.OrdinalIgnoreCase) || Cell(pinnedCol) == "1";
            result.Add(new HistoryEntry(Cell(longCol), Cell(shortCol), provider, created, pinned));
        }
        return result;
    }

    /// <summary>Adds imported entries to existing ones; the same short link is only kept once.</summary>
    public static List<HistoryEntry> Merge(IEnumerable<HistoryEntry> existing, IEnumerable<HistoryEntry> imported)
    {
        var byShort = new Dictionary<string, HistoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in existing.Concat(imported))
        {
            if (byShort.TryGetValue(e.ShortUrl, out var old))
                byShort[e.ShortUrl] = old with { Pinned = old.Pinned || e.Pinned };
            else
                byShort[e.ShortUrl] = e;
        }
        return byShort.Values
            .OrderByDescending(e => e.Pinned)
            .ThenByDescending(e => e.CreatedAt)
            .Take(AppStorage.MaxHistory)
            .ToList();
    }

    private static bool IsHttpUrl(string s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "http" or "https";

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    /// <summary>Minimal RFC 4180 reader: quoted fields, doubled quotes, commas and line breaks inside quotes.</summary>
    internal static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    if (row.Any(f => f.Length > 0)) rows.Add(row);
                    row = [];
                    break;
                default: field.Append(c); break;
            }
        }
        row.Add(field.ToString());
        if (row.Any(f => f.Length > 0)) rows.Add(row);
        return rows;
    }
}
