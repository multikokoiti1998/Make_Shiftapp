using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Csv
{

    // 既存の DatabaseHelper を使う前提
    public interface IShiftCsvBuilder
    {
        IReadOnlyList<ShiftRow> BuildRows(DateTime start, DateTime end);
        IReadOnlyList<MatrixRecord> BuildMatrix(DateTime start, DateTime end);
    }

    public sealed class ShiftCsvBuilder : IShiftCsvBuilder
    {
        private readonly DatabaseHelper _db; // あなたの実装を参照
        private readonly Dictionary<int, string> _symbolCache = new();

        public ShiftCsvBuilder(DatabaseHelper db) => _db = db;

        public IReadOnlyList<ShiftRow> BuildRows(DateTime start, DateTime end)
        {
            var items = _db.GetShiftRow(start, end);

            return items.Select(x => new ShiftRow
            {
                EmployeeId = x.EmployeeId,
                EmployeeName = x.EmployeeName,
                Date = x.Date,
                ShiftSymbol = x.ShiftSymbol
            }).ToList();
        }

        public IReadOnlyList<MatrixRecord> BuildMatrix(DateTime start, DateTime end)
        {
            var rows = BuildRows(start, end);
            return rows.GroupBy(r => new { r.EmployeeId, r.EmployeeName })
                       .Select(g =>
                       {
                           var rec = new MatrixRecord { EmployeeId = g.Key.EmployeeId, EmployeeName = g.Key.EmployeeName };
                           foreach (var r in g) rec.Map[r.Date.Date] = r.ShiftSymbol;
                           return rec;
                       })
                       .OrderBy(r => r.EmployeeName)
                       .ToList();
        }

    }

}
