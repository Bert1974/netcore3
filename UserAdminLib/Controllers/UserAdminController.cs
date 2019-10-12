using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserAdminLib.Configuration;
using UserAdminLib.ViewModels;

namespace UserAdminLib
{
    // Used to set the controller name for routing purposes. Without this convention the
    // names is 'GenericController`1[Widget]' rather than 'Widget'.
    //
    // Conventions can be applied as attributes or added to MvcOptions.Conventions.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class GenericControllerNameConvention : Attribute, IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            var type = typeof(Controllers.UserAdminBaseController<,,,>);
            if (!controller.ControllerType.IsDerived(type))
            {
                return;
            }
            controller.ControllerName = lookup[controller.ControllerType];
        }

        private static Dictionary<Type,string> lookup = new Dictionary<Type, string>();

        internal static void RegisterController(Type controllertype)
        {
            if (lookup.Count!=0)
            {
                throw new NotImplementedException();
            }
            lookup[controllertype] = null;
        }

        internal static void SetURL(Type controllertype, string url)
        {
            if (lookup.Count != 1)
            {
                throw new NotImplementedException();
            }
            lookup[lookup.Keys.Single()] = url;
        }
    }
}
namespace UserAdminLib.Controllers
{
    [Route("[controller]")]
    [GenericControllerNameConvention()]
    [Authorize(Policy = Constants.Policy)]
    public class UserAdminBaseController<TUser, TRole, TKey, TContext> : Controller
        where TUser : IdentityUser<TKey>
        where TRole : IdentityRole<TKey>
        where TKey : IEquatable<TKey>
        where TContext : DbContext
    {
        protected readonly Configuration.UserAdminOptions _options;
        protected readonly UserManager<TUser> _users;
        protected readonly TContext _context;

        public UserAdminBaseController(TContext context, IOptions<Configuration.UserAdminOptions> options, UserManager<TUser> users)
        {
            _options = options.Value;
            _users = users;
            _context = context;
        }
        [Route("")]
        public IActionResult Index()
        {
            return RedirectToAction("Search");
        }
        [HttpGet]
        [Route("Search")]
        public async Task<IActionResult> Search()
        {
            var user = await _users.FindByNameAsync(User.Identity.Name);
            return RedirectToAction("Details", new { id = user.Id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Search")]
        public async Task<IActionResult> Search(string username, int? topindex)
        {
            var userinfo = new UserInfo[0];

            if (!string.IsNullOrWhiteSpace(username))
            {
                var users = _users.Users.OrderBy(_u => _u.UserName).Where(_u => (_u.UserName.Contains(username) || (_u.Email.Contains(username)))).ToArray();
                userinfo= CheckUsers(users);
            }
            return View("~/Views/UserAdminController/Search.cshtml", await GetModel<UserInfo[]>(userinfo));
        }
        [Route("Details")]
        public async Task<IActionResult> Details(string id)
        {
            TKey searchfor = (TKey)(object)id;
            var user = (await _users.FindByIdAsync(id));// _users.Users.AsEnumerable().SingleOrDefault(_u => _u.Id.Equals(searchfor)).ToUserInfo();
            var userinfo = await CheckUser(user);
            var model = await GetModel<UserInfo>(userinfo);
            return View("~/Views/UserAdminController/Details.cshtml", model);
        }

        [Route("Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("~/Views/UserAdminController/Error.cshtml");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("useradmin/Set")]
        public async Task<IActionResult> Set(UserInfo userinfo)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Error");
            }
            var user = await _users.FindByIdAsync(userinfo.Id);

            if (user == null)
            {
                return RedirectToAction("Error");
            }
            if (userinfo.Email != null)
            {
                var token = await _users.GenerateChangeEmailTokenAsync(user, userinfo.Email);
                var result = await _users.ChangeEmailAsync(user, userinfo.Email, token);
            }
            if (HttpContext.Request.Form.ContainsKey("submit"))
            {
                string getvalue(string _k) => HttpContext.Request.Form.ContainsKey(_k) ? HttpContext.Request.Form[_k].SingleOrDefault() : null;

                var cmd = HttpContext.Request.Form["submit"].Single();

                if (cmd == "save_admin")
                {
                    typeof(TUser).GetProperty(_options.Field).SetValue(user, getvalue("doadmin") != null, new object[0]);

                    _context.Entry(user).State = EntityState.Modified;

                    await _context.SaveChangesAsync();
                }
                else if (!await _Set(cmd, user,getvalue))
                {
                    return RedirectToAction("Error");
                }
            }
            return RedirectToAction("Details", new { id = user.Id });
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async Task<bool> _Set(string cmd, TUser user, Func<string, string> getvalue)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            switch (cmd)
            {
            }
            return false;
        }
        private async Task<ViewModels.RolesWithData<T>> GetModel<T>(T data)
        {
            var model = new ViewModels.RolesWithData<T> { Data = data };
            await CheckModel(model);
            return model;
        }

        protected UserInfo[] CheckUsers(TUser[] users)
        {
            return users.Select(async _u => await CheckUser(_u)).Select(_t => _t.Result).ToArray();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async Task<UserInfo> CheckUser(TUser user)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var result = user.ToUserInfo();

            if (_options.Field != null)
            {
                result.IsAdmin = (bool)typeof(TUser).GetProperty(_options.Field).GetValue(user, new object[0]);
            }
            else
            {
                result.IsAdmin = null;
            }
            return result;
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async Task CheckModel<TData>(ViewModels.RolesWithData<TData> model)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
        }

    }
    public class UserAdminController<TUser, TContext> : UserAdminBaseController<TUser, IdentityRole, string, TContext>
        where TContext : DbContext
        where TUser : IdentityUser<string>
    {
        public UserAdminController(TContext context, IOptions<Configuration.UserAdminOptions> options, UserManager<TUser> users)
            : base(context, options, users)
        {
        }
    }
    public class UserAdminRolesController<TUser, TRole, TContext> : UserAdminBaseController<TUser, TRole, string, TContext>
        where TUser : IdentityUser<string>
        where TRole : IdentityRole<string>
        where TContext : IdentityDbContext<TUser, TRole, string, IdentityUserClaim<string>, IdentityUserRole<string>, IdentityUserLogin<string>, IdentityRoleClaim<string>, IdentityUserToken<string>>
    {
        private readonly RoleManager<TRole> _roles;
        private readonly Roles.IRoleSingleton _info;

        public UserAdminRolesController(TContext context, IOptions<Configuration.UserAdminOptions> options, UserManager<TUser> users,RoleManager<TRole> roles, Roles.IRoleSingleton info)
            : base(context, options, users)
        {
            this._roles = roles;
            this._info = info;
        }
        protected async override Task<UserInfo> CheckUser(TUser user)
        {
            var result = await base.CheckUser(user);

            var u = await _users.FindByIdAsync(user.Id);

            result.Roles = (await _users.GetRolesAsync(u)).OrderBy(_r => _r).ToArray();

            result.RoleHandlers = result.Roles.Select(_r => _info.Get(_roles.NormalizeKey(_r))).Where(_h => _h != null).ToArray();

            return result;
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Task CheckModel<TData>(ViewModels.RolesWithData<TData> model)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            model.Roles = _roles.Roles.OrderBy(_r => _r.Name).Select(_r => _r.Name).ToArray();
        }
        protected override async Task<bool> _Set(string cmd, TUser user,  Func<string, string> getvalue)
        {
            switch (cmd)
            {
                case "set_roles":
                    {
                        try
                        {
                             foreach (var role in _roles.Roles.ToArray())
                             {
                                 if (await _users.IsInRoleAsync(user, role.Name))
                                 {
                                     if (getvalue($"role_{role.Name}") == null)
                                     {
                                         await _users.RemoveFromRoleAsync(user, role.Name);
                                     }
                                 }
                                 else
                                 {
                                     if (getvalue($"role_{role.Name}") != null)
                                     {
                                         await _users.AddToRoleAsync(user, role.Name);
                                     }
                                 }
                             }
                        }
                        catch (Exception e)
                        {
                            throw;
                        }
                    }
                    return true;
            }
            return await base._Set(cmd, user, getvalue);
        }
    }
}
