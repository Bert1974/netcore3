using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UserAdminLib.Roles
{
    #region rolemode.Table / role-edit
    public interface IRoleHandler
    {
        /// <summary>
        /// name of role
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// TypeName of a ViewComponent
        /// </summary>
        public string ViewComponent { get; }
    }/// <summary>
     /// added with  builder.Services.AddSingleton by UserAdminLib.UserAdminExtensions
     /// </summary>
    public interface IRoleSingleton
    {
        public IRoleHandler Get(string role);
    }
    #endregion

}
