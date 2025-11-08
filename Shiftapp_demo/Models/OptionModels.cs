using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Models
{
    public class OptionItem
    {
        public string Code { get; set; } = "";   // DB保存や内部処理用
        public string Label { get; set; } = "";  // UI表示用

        public OptionItem() { }

        public OptionItem(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public override string ToString() => Label;
    }
}
