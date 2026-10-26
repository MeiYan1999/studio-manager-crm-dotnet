using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace MayAlbum.Services;

public enum ExcelColumnKind
{
    Text,
    Wrap,
    Money,
    Number,
    Decimal
}

public sealed record ExcelColumn(
    string Header,
    ExcelColumnKind Kind = ExcelColumnKind.Text,
    double Width = 18);

public sealed record ExcelRow(IReadOnlyList<object?> Cells, bool Bold = false);

public sealed class ExcelSheet
{
    public ExcelSheet(string name, IEnumerable<ExcelColumn> columns)
    {
        Name = name;
        Columns = columns.ToList();
    }

    public string Name { get; }
    public List<ExcelColumn> Columns { get; }
    public List<ExcelRow> Rows { get; } = [];

    public ExcelSheet AddRow(params object?[] cells)
    {
        Rows.Add(new ExcelRow(cells));
        return this;
    }

    public ExcelSheet AddTotalRow(params object?[] cells)
    {
        Rows.Add(new ExcelRow(cells, Bold: true));
        return this;
    }

    public ExcelSheet AddBlankRow()
    {
        Rows.Add(new ExcelRow([]));
        return this;
    }
}

/// <summary>
/// Minimal XLSX writer. Generates a valid Open XML workbook with inline
/// strings, frozen headers, filters, column widths and VND number formats.
/// No third-party package or Office installation is required.
/// </summary>
public sealed class ExcelWorkbook
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private readonly List<ExcelSheet> _sheets = [];

    public ExcelSheet AddSheet(string name, params ExcelColumn[] columns)
    {
        var sheet = new ExcelSheet(SafeSheetName(name, _sheets.Count + 1), columns);
        _sheets.Add(sheet);
        return sheet;
    }

    public byte[] Build()
    {
        if (_sheets.Count == 0)
        {
            throw new InvalidOperationException("Workbook cần ít nhất một sheet.");
        }

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes());
            Write(archive, "_rels/.rels", RootRelationships());
            Write(archive, "xl/workbook.xml", WorkbookXml());
            Write(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships());
            Write(archive, "xl/styles.xml", StylesXml());
            for (var index = 0; index < _sheets.Count; index++)
            {
                Write(archive, $"xl/worksheets/sheet{index + 1}.xml", SheetXml(_sheets[index]));
            }
        }

        return stream.ToArray();
    }

    private string ContentTypes()
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<Override PartName=\"/xl/worksheets/sheet{index + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }

        builder.Append("</Types>");
        return builder.ToString();
    }

    private static string RootRelationships() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private string WorkbookXml()
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append($"<workbook xmlns=\"{SpreadsheetNs}\" xmlns:r=\"{RelationshipsNs}\">");
        builder.Append("<sheets>");
        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<sheet name=\"{XmlEscape(_sheets[index].Name)}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\"/>");
        }

        builder.Append("</sheets>");
        builder.Append("</workbook>");
        return builder.ToString();
    }

    private string WorkbookRelationships()
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<Relationship Id=\"rId{index + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{index + 1}.xml\"/>");
        }

        builder.Append($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        builder.Append("</Relationships>");
        return builder.ToString();
    }

    private static string StylesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        $"<styleSheet xmlns=\"{SpreadsheetNs}\">" +
        "<numFmts count=\"2\">" +
        "<numFmt numFmtId=\"164\" formatCode=\"#,##0\"/>" +
        "<numFmt numFmtId=\"165\" formatCode=\"#,##0.0\"/>" +
        "</numFmts>" +
        "<fonts count=\"3\">" +
        "<font><sz val=\"11\"/><color rgb=\"FF26211A\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "<font><b/><sz val=\"11\"/><color rgb=\"FF26211A\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "</fonts>" +
        "<fills count=\"3\">" +
        "<fill><patternFill patternType=\"none\"/></fill>" +
        "<fill><patternFill patternType=\"gray125\"/></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF26211A\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
        "</fills>" +
        "<borders count=\"2\">" +
        "<border><left/><right/><top/><bottom/><diagonal/></border>" +
        "<border><left/><right/><top/><bottom style=\"thin\"><color rgb=\"FFD9D0C1\"/></bottom><diagonal/></border>" +
        "</borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"9\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf>" +
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
        "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\"/>" +
        "</cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";

    private static string SheetXml(ExcelSheet sheet)
    {
        var lastColumn = ColumnName(Math.Max(0, sheet.Columns.Count - 1));
        var lastRow = Math.Max(1, sheet.Rows.Count + 1);
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append($"<worksheet xmlns=\"{SpreadsheetNs}\" xmlns:r=\"{RelationshipsNs}\">");
        builder.Append($"<dimension ref=\"A1:{lastColumn}{lastRow}\"/>");
        builder.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\" activeCell=\"A2\" sqref=\"A2\"/></sheetView></sheetViews>");
        builder.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
        builder.Append("<cols>");
        for (var index = 0; index < sheet.Columns.Count; index++)
        {
            var column = sheet.Columns[index];
            builder.Append($"<col min=\"{index + 1}\" max=\"{index + 1}\" width=\"{column.Width.ToString("0.##", CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
        }

        builder.Append("</cols>");
        builder.Append("<sheetData>");
        builder.Append("<row r=\"1\" ht=\"20\" customHeight=\"1\">");
        for (var index = 0; index < sheet.Columns.Count; index++)
        {
            builder.Append(InlineCell($"{ColumnName(index)}1", sheet.Columns[index].Header, 1));
        }

        builder.Append("</row>");
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            var excelRow = rowIndex + 2;
            if (row.Cells.Count == 0)
            {
                builder.Append($"<row r=\"{excelRow}\"/>");
                continue;
            }

            builder.Append($"<row r=\"{excelRow}\">");
            for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
            {
                var column = columnIndex < sheet.Columns.Count
                    ? sheet.Columns[columnIndex]
                    : new ExcelColumn("", ExcelColumnKind.Text);
                builder.Append(Cell(
                    $"{ColumnName(columnIndex)}{excelRow}",
                    row.Cells[columnIndex],
                    StyleFor(column.Kind, row.Bold)));
            }

            builder.Append("</row>");
        }

        builder.Append("</sheetData>");
        if (sheet.Columns.Count > 0)
        {
            builder.Append($"<autoFilter ref=\"A1:{lastColumn}{lastRow}\"/>");
        }

        builder.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>");
        builder.Append("</worksheet>");
        return builder.ToString();
    }

    private static int StyleFor(ExcelColumnKind kind, bool bold) => kind switch
    {
        ExcelColumnKind.Money => bold ? 7 : 2,
        ExcelColumnKind.Number => bold ? 8 : 3,
        ExcelColumnKind.Decimal => bold ? 8 : 4,
        ExcelColumnKind.Wrap => bold ? 6 : 5,
        _ => bold ? 6 : 0
    };

    private static string Cell(string reference, object? value, int style)
    {
        if (value is null)
        {
            return $"<c r=\"{reference}\" s=\"{style}\"/>";
        }

        if (value is decimal or double or float or int or long or short or byte)
        {
            var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return $"<c r=\"{reference}\" s=\"{style}\"><v>{number.ToString(CultureInfo.InvariantCulture)}</v></c>";
        }

        if (value is bool flag)
        {
            return $"<c r=\"{reference}\" s=\"{style}\" t=\"b\"><v>{(flag ? 1 : 0)}</v></c>";
        }

        return InlineCell(reference, value.ToString() ?? "", style);
    }

    private static string InlineCell(string reference, string value, int style) =>
        $"<c r=\"{reference}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlEscape(value)}</t></is></c>";

    private static string ColumnName(int index)
    {
        var name = "";
        var current = index;
        while (current >= 0)
        {
            name = (char)('A' + current % 26) + name;
            current = current / 26 - 1;
        }

        return name;
    }

    private static string SafeSheetName(string? value, int fallbackIndex)
    {
        var name = string.IsNullOrWhiteSpace(value) ? $"Sheet {fallbackIndex}" : value.Trim();
        foreach (var invalid in "[]:*?/\\")
        {
            name = name.Replace(invalid, ' ');
        }

        name = name.Trim();
        if (name.Length == 0)
        {
            name = $"Sheet {fallbackIndex}";
        }

        return name.Length <= 31 ? name : name[..31];
    }

    private static string XmlEscape(string? value)
    {
        var text = value ?? "";
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is '\t' or '\n' or '\r' || character >= 0x20)
            {
                builder.Append(character);
            }
        }

        return builder
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .ToString();
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
