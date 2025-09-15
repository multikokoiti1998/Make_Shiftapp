using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Models
{
    public sealed class MatrixRecord
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public Dictionary<DateTime, string> Map { get; } = new();
    }
}
