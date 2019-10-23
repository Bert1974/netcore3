using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UserAdminLib.Configuration
{
    #region options
    public class UserAdminOptions
    {
        /// <summary>
        /// if non null, name of bool-property on TUser, uased to check if authorized
        /// </summary>
        public string Field { get; set; } = null; // if non null, name of bool-property on TUser

        /// <summary>
        /// URL the user administration library will be available under
        /// </summary>
        public string Url { get; set; } = "UserAdmin";

        public string wwwdir { get; set; } = "/useradmin";
    }
    #endregion

}
