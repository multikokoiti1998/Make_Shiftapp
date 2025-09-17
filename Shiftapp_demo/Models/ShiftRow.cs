using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Models
{
    public class ShiftRow
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public DateTime Date { get; init; }
        public string ShiftSymbol { get; init; } = "";   // 当直/日勤 など

        public int Role {  get; init; }
    }
}
