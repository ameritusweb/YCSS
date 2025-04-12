using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Models
{
    public class StylePropertyDefinition
    {
        public string Property { get; set; }
        public string Value { get; set; }
        public bool Important { get; set; }
        public string Comment { get; set; }
    }
}
