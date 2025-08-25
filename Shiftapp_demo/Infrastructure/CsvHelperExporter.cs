using Shiftapp_demo.Helper;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Shiftapp_demo.Helper.CsvHelperClass;

namespace Shiftapp_demo.Infrastructure
{
    public sealed class CsvHelperExporter : IShiftCsvExporter
    {
        private readonly string _delimiter;
        private readonly Encoding _encoding;

        // 既定: Excelで崩れにくい UTF-8 BOM + カンマ
        public CsvHelperExporter(string delimiter = ",", Encoding? encoding = null)
        {
            _delimiter = delimiter;
            _encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        public async Task ExportAsync(IEnumerable<ShiftCsvRow> rows, string filePath, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = _delimiter,
                Encoding = _encoding,
                NewLine = Environment.NewLine
            };

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs, _encoding);
            using var csv = new CsvWriter(writer, config);

            csv.WriteHeader<ShiftCsvRow>();
            await csv.NextRecordAsync();

            foreach (var r in rows)
            {
                ct.ThrowIfCancellationRequested();
                csv.WriteRecord(r);
                await csv.NextRecordAsync();
            }
            await writer.FlushAsync();
        }
    }
}
