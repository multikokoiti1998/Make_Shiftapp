using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shiftapp_demo.Models
{
    public class Holiday
    {
        public int request_id { get; set; }
        public int employee_id { get; set; }
        public DateTime request_date { get; set; }
        public int leave_type { get; set; }
    }
}