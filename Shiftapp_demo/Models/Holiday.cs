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
        public int holiday_id {  get; set; }
        public DateTime date { get; set; }
        public string name { get; set; }
    }
}