using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace useradmin.Entities
{
    public class ApplicationUser : IdentityUser
    {
    }
    public class UserInfo
    {
        public int Id { get; set; }
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }

        public string Location { get; set; }
    }
}
