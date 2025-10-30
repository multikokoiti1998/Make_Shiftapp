using System.IO;
using System.Text;

namespace Shiftapp_demo.Csv
{

    public interface IShiftCsvExporter
    {
        Task ExportAsync<T>(IEnumerable<T> data, ICsvLayout<T> layout, string filePath, CancellationToken ct = default);
    }
    public sealed class CsvHelperExporter : IShiftCsvExporter
    {
        private readonly string _delimiter;
        private readonly Encoding _encoding;

        public CsvHelperExporter(string delimiter = ",", Encoding? encoding = null)
        {
            _delimiter = delimiter;
            _encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        public async Task ExportAsync<T>(IEnumerable<T> data, ICsvLayout<T> layout, string filePath, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(fs, _encoding);

            await writer.WriteLineAsync(string.Join(_delimiter, layout.GetHeaders()));
            foreach (var row in layout.MaterializeRows(data))
            {
                ct.ThrowIfCancellationRequested();
                var escaped = row.Select(Escape);
                await writer.WriteLineAsync(string.Join(_delimiter, escaped));
            }
            await writer.FlushAsync();
        }

        private string Escape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool needQuote = s.Contains('"') || s.Contains('\n') || s.Contains('\r') || s.Contains(_delimiter);
            return needQuote ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
    }
}
