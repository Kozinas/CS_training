using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace Course.Import;

public static class OtherFormats
{
    private static readonly string[] Header = ["id", "date", "category", "amount"];

    public static List<RawExpense> ReadCsv(string text, int maxRecords = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRecords);
        using var input = new StringReader(text);
        using var csv = new CsvReader(input, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",", HasHeaderRecord = true, IgnoreBlankLines = false,
            DetectColumnCountChanges = true, ExceptionMessagesContainRawData = false
        });
        if (!csv.Read()) throw new InvalidDataException("csv_header");
        csv.ReadHeader();
        if (csv.HeaderRecord is null || !csv.HeaderRecord.SequenceEqual(Header))
            throw new InvalidDataException("csv_header");
        var rows = new List<RawExpense>();
        while (csv.Read())
        {
            if (rows.Count == maxRecords) throw new InvalidDataException("record_limit");
            rows.Add(new RawExpense(csv.GetField(0), csv.GetField(1), csv.GetField(2), csv.GetField(3)));
        }
        return rows;
    }

    public static XDocument ReadXml(string text)
    {
        using var input = new StringReader(text);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 1_048_576
        });
        return XDocument.Load(reader);
    }

    public static byte[] ZipSingle(string text)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("report.txt").Open(), new UTF8Encoding(false));
            writer.Write(text);
        }
        return bytes.ToArray();
    }

    public static async Task<string> ReadZipReportAsync(byte[] archive, int maxBytes, CancellationToken token)
    {
        using var bytes = new MemoryStream(archive, writable: false);
        using var zip = new ZipArchive(bytes, ZipArchiveMode.Read);
        if (zip.Entries.Count != 1 || zip.Entries[0].FullName != "report.txt")
            throw new InvalidDataException("zip_entry");
        await using var entry = zip.Entries[0].Open();
        byte[] result = await BoundedFiles.ReadAsync(entry, maxBytes, token);
        return new UTF8Encoding(false, true).GetString(result);
    }
}
