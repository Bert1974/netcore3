using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RoleDb
{
    namespace Configuration
    {
        public class RoleDBOptions
        {
            public RoleDBRole[] Roles { get; set; }
        }
        public class RoleDBRole
        {
            public string Name { get; set; }
            public string CustomerRoleHandleType { get; set; }
        }
    }
    namespace Roles
    {
        public interface IRoleHandler<TUser, TRole, TContext> : UserAdminLib.Roles.IRoleHandler where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            TRole Role { get; }

            Task AddRole(TContext context, TUser user);
            Task RemoveRole(TContext context, TUser user);
            Task<bool> CheckRole(TContext context, TUser user);
            Task<IList<TUser>> GetUsers(TContext context, IUserStore<TUser> userstore, CancellationToken cancellationToken, IServiceProvider services);
        }

        public abstract class BaseRoleHandler<TUser, TRole, TContext> : IRoleHandler<TUser, TRole, TContext> where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            public TRole Role { get; }

            string UserAdminLib.Roles.IRoleHandler.Name => this.Role.Name;
            public abstract string ViewComponent { get; }

            public BaseRoleHandler(TRole role)
            {
                this.Role = role;
            }

            public abstract Task AddRole(TContext context, TUser user);
            public abstract Task RemoveRole(TContext context, TUser user);
            public abstract Task<bool> CheckRole(TContext context, TUser user);
            public abstract Task<IList<TUser>> GetUsers(TContext context, IUserStore<TUser> userstore, CancellationToken cancellationToken, IServiceProvider services);

            protected Task<IList<TUser>> GetUsers(IUserStore<TUser> userstore, IQueryable<string> ids)
            {
                return Task.FromResult((userstore as IQueryableUserStore<TUser>).Users.Where(_u => ids.Contains(_u.Id)).ToList() as IList<TUser>);
            }
        }
    }

    public class RoleDBSingleton<TUser, TRole, TContext> : UserAdminLib.Roles.IRoleSingleton where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
    {
        private readonly Dictionary<TRole, Roles.IRoleHandler<TUser, TRole, TContext>> allroles = new Dictionary<TRole, Roles.IRoleHandler<TUser, TRole, TContext>>();

        public void SyncRoles(Configuration.RoleDBOptions _options, IRoleStore<TRole> rolestore)
        {
            lock (this)
            {
                if (allroles.Count == 0 && _options.Roles.Length > 0)
                {
                    var roles = (IQueryableRoleStore<TRole>)rolestore;

                    foreach (var role in _options.Roles.Where(_r => !roles.Roles.Any(_r2 => _r2.Name == _r.Name)).ToArray())
                    {
                        var newrole = (TRole)Activator.CreateInstance(typeof(TRole), new object[] { role });
                        roles.CreateAsync(newrole, default).Wait();
                    }
                    foreach (var role in _options.Roles)
                    {
                        var identity = roles.Roles.Single(_r => _r.Name == role.Name);
                        this.allroles[identity] = Create(role, identity);
                    }
                }
            }
        }

        private Roles.IRoleHandler<TUser, TRole, TContext> Create(Configuration.RoleDBRole roleinfo, TRole role)
        {
            var type = Type.GetType(roleinfo.CustomerRoleHandleType);
            return (Roles.IRoleHandler<TUser, TRole, TContext>)Activator.CreateInstance(type, new object[] { role });
        }

        internal Roles.IRoleHandler<TUser, TRole, TContext> GetRoleHandler(string normalizedRoleName)
        {
            var key = allroles.SingleOrDefault(_k => _k.Key.NormalizedName == normalizedRoleName);
            if (key.Key != null)
            {
                return key.Value;
            }
            return null;
        }

        public void SyncUsers(TContext context, RoleDBUserStore<TUser, TRole, TContext> userstore)
        {
            foreach (var user in userstore.Users)
            {
                SyncUser(context, userstore, user);
            }
        }
        public void SyncUser(TContext context, RoleDBUserStore<TUser, TRole, TContext> userstore, TUser user)
        {
            foreach (var role in allroles)
            {
                if (role.Value.CheckRole(context, user).GetAwaiter().GetResult())
                {
                    userstore.AddToRoleAsync(user, role.Key.NormalizedName).Wait();
                }
                else
                {
                    userstore.RemoveFromRoleAsync(user, role.Key.NormalizedName).Wait();
                }
            }
        }

        UserAdminLib.Roles.IRoleHandler UserAdminLib.Roles.IRoleSingleton.Get(string normalizedRoleName)
        {
            return GetRoleHandler(normalizedRoleName);
        }
    }

    public class RoleDBUserStore<TUser, TRole, TContext> : Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<TUser, TRole, TContext> where TUser : IdentityUser<string> where TContext : DbContext where TRole : IdentityRole<string>
    {
        private readonly RoleDBSingleton<TUser, TRole, TContext> _info;
        private readonly IServiceProvider _services;
        private readonly TContext _context;

        public RoleDBUserStore(RoleDBSingleton<TUser, TRole, TContext> info, TContext context, IServiceProvider services, IdentityErrorDescriber describer = null)
            : base(context, describer)
        {
            this._info = info;
            this._services = services;
            this._context = context;
        }
        public override async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            var def = this._info.GetRoleHandler(normalizedRoleName);
            if (def != null)
            {
                await def.AddRole(_context, user);
            }
            await base.AddToRoleAsync(user, normalizedRoleName, cancellationToken);
        }
        public override async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken = default)
        {
           // await _info.SyncUser(_context, this, user, _services);
            return await base.GetRolesAsync(user, cancellationToken);
        }
        public override async Task<IList<TUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            var def = this._info.GetRoleHandler(normalizedRoleName);
            if (def != null)
            {
                return await def.GetUsers(_context, this, cancellationToken, _services);
            }
            return await base.GetUsersInRoleAsync(normalizedRoleName, cancellationToken);
        }
        public override async Task<bool> IsInRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
        //    var def = this._info.GetRoleHandler(normalizedRoleName);
            return await base.IsInRoleAsync(user, normalizedRoleName, cancellationToken);
        }
        public override async Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            var def = this._info.GetRoleHandler(normalizedRoleName);
            if (def != null)
            {
                await def.RemoveRole(_context, user);
            }
            await base.RemoveFromRoleAsync(user, normalizedRoleName, cancellationToken);
        }
        protected override async Task<TRole> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            var def = this._info.GetRoleHandler(normalizedRoleName);
            if (def != null)
            {
                return def.Role;
            }
            return await base.FindRoleAsync(normalizedRoleName, cancellationToken);
        }
        protected override Task<IdentityUserRole<string>> FindUserRoleAsync(string userId, string roleId, CancellationToken cancellationToken)
        {
            return base.FindUserRoleAsync(userId, roleId, cancellationToken);
        }
    }
    public static class Extensions
    {
        public static IdentityBuilder AddRoleDBEntityFrameworkStores<TUser, TRole, TContext>(this IdentityBuilder builder) where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            builder.Services.AddSingleton<RoleDBSingleton<TUser, TRole, TContext>>();
            builder.Services.AddSingleton<UserAdminLib.Roles.IRoleSingleton>(x => x.GetService<RoleDBSingleton<TUser, TRole, TContext>>());

            builder.AddUserStore<RoleDBUserStore<TUser, TRole, TContext>>();
            builder.AddRoleStore<RoleStore<TRole, TContext, string>>();

            return builder;
        }
    }
}
