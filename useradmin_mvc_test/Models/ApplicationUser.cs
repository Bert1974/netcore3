using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace useradmin_mvc_test.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsUseradmin { get; set; }
    }
}
