using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Csv
{

    public interface ICsvLayout<T>
    {
        IReadOnlyList<string> GetHeaders();
        IEnumerable<string[]> MaterializeRows(IEnumerable<T> rows);
    }

    /* 行形式（固定列） */
    internal sealed class RowLayout : ICsvLayout<ShiftRow>
    {
        public IReadOnlyList<string> GetHeaders() =>
            new[] { "employee_id", "employee_name", "date", "shift", "note", "registered_at" };

        public IEnumerable<string[]> MaterializeRows(IEnumerable<ShiftRow> rows)
        {
            foreach (var r in rows)
            {
                yield return new[]
                {
                    r.EmployeeId.ToString(),
                    r.EmployeeName,
                    r.Date.ToString("yyyy-MM-dd"),
                    r.ShiftSymbol,
                };
            }
        }
    }

    /* マトリクス形式（日付が列に並ぶ） */
    public sealed class MatrixLayout : ICsvLayout<MatrixRecord>
    {
        private readonly List<DateTime> _days;
        public MatrixLayout(DateTime start, DateTime end)
        {
            var s = start.Date; var e = end.Date;
            _days = Enumerable.Range(0, (int)(e - s).TotalDays + 1).Select(i => s.AddDays(i)).ToList();
        }

        public IReadOnlyList<string> GetHeaders() =>
            new[] { "employee_id", "employee_name" }.Concat(_days.Select(d => d.ToString("yyyy-MM-dd"))).ToArray();

        public IEnumerable<string[]> MaterializeRows(IEnumerable<MatrixRecord> records)
        {
            foreach (var r in records)
            {
                var row = new List<string> { r.EmployeeId.ToString(), r.EmployeeName };
                foreach (var d in _days)
                    row.Add(r.Map.TryGetValue(d, out var sym) ? sym : "");
                yield return row.ToArray();
            }
        }
    }

}
