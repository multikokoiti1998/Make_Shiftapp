using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            _encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // Excel向けUTF-8 BOM
        }

        public async Task ExportAsync<T>(IEnumerable<T> data, ICsvLayout<T> layout, string filePath, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(fs, _encoding);

            await writer.WriteLineAsync(string.Join(_delimiter, layout.GetHeaders()));
            foreach (var row in layout.MaterializeRows(data))
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Join(_delimiter, row.Select(Escape)));
            }
            await writer.FlushAsync();
        }

        private static string Escape(string s)
        {
            if (s is null) return "";
            bool need = s.Contains('"') || s.Contains('\n') || s.Contains('\r') || s.Contains(',');
            return need ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
    }

}
