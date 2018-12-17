using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ldapcp
{
    public class ClaimEntity
    {
        public string ClaimType
        {
            get;
            set;
        }

        public string Value
        {
            get;
            set;
        }

        public string ValueType
        {
            get;
            set;
        }
    }
}
