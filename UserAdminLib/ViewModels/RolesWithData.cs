using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UserAdminLib.ViewModels
{
    public class RolesWithData<T>
    {
        public string[] Roles { get; set; }
        public T Data { get; set; }
    }
}
