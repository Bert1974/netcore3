using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoleDb.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using RoleDb.internals;

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
            public bool IsDefault { get; set; } = false;
            public string CustomerRoleHandleType { get; set; } = null;
        }
    }
    namespace Roles
    {
        public interface IRoleHandler<TUser, TRole, TContext> : UserAdminLib.Roles.IRoleHandler where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            TRole Role { get; }
            bool IsDefault { get; }

            Task AddRoleASync(TContext context, TUser user);
            Task RemoveRoleAsync(TContext context, TUser user);
            Task<bool> IsInRoleASync(TContext context, TUser user);
            Task<IList<TUser>> GetUsersAsync(TContext context, IUserStore<TUser> userstore, CancellationToken cancellationToken, IServiceProvider services);
        }

        public abstract class BaseRoleHandler<TUser, TRole, TContext> : IRoleHandler<TUser, TRole, TContext> where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            public TRole Role { get; private set; }
            public bool IsDefault { get; }

            public string Name => this.Role.Name;

            public abstract string ViewComponent { get; }

            public BaseRoleHandler(Configuration.RoleDBRole roleinfo, TRole role)
            {
                this.Role = role;
                this.IsDefault = roleinfo.IsDefault;
            }

            public abstract Task AddRoleASync(TContext context, TUser user);
            public abstract Task RemoveRoleAsync(TContext context, TUser user);
            public abstract Task<bool> IsInRoleASync(TContext context, TUser user);
            public abstract Task<IList<TUser>> GetUsersAsync(TContext context, IUserStore<TUser> userstore, CancellationToken cancellationToken, IServiceProvider services);

            protected Task<IList<TUser>> GetUsers(IUserStore<TUser> userstore, IQueryable<string> ids)
            {
                return Task.FromResult((userstore as IQueryableUserStore<TUser>).Users.Where(_u => ids.Contains(_u.Id)).ToList() as IList<TUser>);
            }

        }
    }

    /*  public interface IRoldeDBCOntext<TRole> where TRole : IdentityRole<string>
      {
          DbSet<TRole> RoleDbRoles { get; set; }
      }*/

    public class RoleDBSingleton<TUser, TRole, TContext> : UserAdminLib.Roles.IRoleSingleton<TRole> where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
    {
        private readonly Dictionary<TRole, Roles.IRoleHandler<TUser, TRole, TContext>> allroles = new Dictionary<TRole, Roles.IRoleHandler<TUser, TRole, TContext>>();

        public async Task SyncRoles(TContext context, Configuration.RoleDBOptions options, RoleManager<TRole> roles)
        {
            if (allroles.Count == 0 && options.Roles.Length > 0)
            {
                foreach (var o in options.Roles)
                {
                    var role = await CreateGetRoleASync(roles, o);
                    var info = Create(context, o, role);
                    this.allroles[role] = info;
                }
            }
        }
        private Roles.IRoleHandler<TUser, TRole, TContext> Create(TContext context, Configuration.RoleDBRole roleinfo, TRole role)
        {
            if (roleinfo.CustomerRoleHandleType != null)
            {
                var type = Type.GetType(roleinfo.CustomerRoleHandleType);
                return (Roles.IRoleHandler<TUser, TRole, TContext>)Activator.CreateInstance(type, new object[] { roleinfo, role });
            }
            return null;
        }
        private async Task<TRole> CreateGetRoleASync(RoleManager<TRole> roles, RoleDBRole o)
        {
            var name = o.Name;
            var role = await roles.FindByNameAsync(name.ToUpper());

            if (role == null)
            {
                role = (TRole)Activator.CreateInstance(typeof(TRole), new object[] { name });

                if (await roles.CreateAsync(role) != IdentityResult.Success)
                {
                    throw new Exception();
                }
            }
            return role;
        }

        /*   public void VerifyRole(TRole role)
           {
               var kv = this.allroles.SingleOrDefault(_r => _r.Key.NormalizedName == role.NormalizedName);

               if (kv.Key != null)
               {
                   if (!object.ReferenceEquals(kv.Key, role))
                   {
                       throw new NotImplementedException();
                   }
               }
               else
               {
                   throw new NotImplementedException();
               }
           }*/


        /*   private async Task<TRole> CreateGetRoleASync(TContext context, RoleDBRole roleinfo)
           {
               var result = await context.RoleDbRoles.SingleOrDefaultAsync(_r => _r.Name == roleinfo.Name);

               if (result == null)
               {
                   result = (TRole)Activator.CreateInstance(typeof(TRole), new object[] { roleinfo.Name });
                   result.NormalizedName = result.Name.ToUpper();
                   //  await context.RoleDbRoles.AddAsync(result);
                   //  await context.SaveChangesAsync();
               }
               return result;
           }*/

        internal Roles.IRoleHandler<TUser, TRole, TContext> GetRoleHandler(string normalizedRoleName)
        {
            var key = allroles.SingleOrDefault(_k => _k.Key.NormalizedName == normalizedRoleName);
            if (key.Key != null)
            {
                return key.Value;
            }
            return null;
        }
        internal bool CheckRole(TRole role)
        {
            return allroles.Values.Any(_h => _h != null && _h.Role.Id == role.Id) ||
                   (GetRoleHandler(role.NormalizedName) != null);
        }

        public async Task SyncUsers(TContext context, RoleDBUserStore<TUser, TRole, TContext> userstore)
        {
            foreach (var user in userstore.Users)
            {
                await SyncUser(context, userstore, user);
            }
        }
        public async Task SyncUser(TContext context, RoleDBUserStore<TUser, TRole, TContext> userstore, TUser user)
        {
            foreach (var role in allroles)
            {
                if (role.Value != null)
                {
                    if (await role.Value.IsInRoleASync(context, user))
                    {
                        if (!await userstore.IsInRoleAsync(user, role.Key.NormalizedName))
                        {
                            await userstore.AddToRoleAsync(user, role.Key.NormalizedName);
                        }
                    }
                    else if (await userstore.IsInRoleAsync(user, role.Key.NormalizedName))
                    {
                        await userstore.RemoveFromRoleAsync(user, role.Key.NormalizedName);

                    }
                }
            }
        }
        UserAdminLib.Roles.IRoleHandler UserAdminLib.Roles.IRoleSingleton<TRole>.Get(string normalizedRoleName)
        {
            return GetRoleHandler(normalizedRoleName);
        }

        internal async Task RemoveRolesAsync(TUser user, TContext context)
        {
            foreach (var h in allroles.Values.Where(_h => _h != null))
            {
                await h.RemoveRoleAsync(context, user);
            }
        }

        internal async Task CreateDefaultRulesAsync(RoleDBUserStore<TUser, TRole, TContext> users, TUser user)
        {
            foreach (var h in allroles.Values.Where(_h => _h?.IsDefault ?? false))
            {
                await users.AddToRoleAsync(user, h.Role.NormalizedName);
            }
        }

        internal async Task<IEnumerable<string>> GetRolesAsync(TContext context, TUser user)
        {
            List<string> result = new List<string>();
            foreach (var h in allroles.Values.Where(_h=>_h!=null))
            {
                if (await h.IsInRoleASync(context, user))
                {
                    result.Add(h.Role.Name);
                }
            }
            return result;
        }
    }
    namespace internals
    {
        public class RoleDbRoleStore<TUser, TRole, TContext> : RoleStore<TRole> where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            private readonly RoleDBSingleton<TUser, TRole, TContext> _info;

            public RoleDbRoleStore(RoleDBSingleton<TUser, TRole, TContext> info, TContext context, IdentityErrorDescriber describer = null)
                : base(context, describer)
            {
                _info = info;
            }
            public override async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken = default)
            {
                if (_info.GetRoleHandler(role.NormalizedName) != null)
                {
                    throw new NotImplementedException();
                }
                return await base.DeleteAsync(role, cancellationToken);
            }
            public override async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken = default)
            {
                if (_info.CheckRole(role))
                {
                    throw new NotImplementedException();
                }
                return await base.UpdateAsync(role, cancellationToken);
            }
            public override async Task SetNormalizedRoleNameAsync(TRole role, string normalizedName, CancellationToken cancellationToken = default)
            {
                if (_info.CheckRole(role))// && role.NormalizedName != normalizedName)
                {
                    throw new NotImplementedException();
                }
                await base.SetNormalizedRoleNameAsync(role, normalizedName, cancellationToken);
            }
            public override async Task SetRoleNameAsync(TRole role, string roleName, CancellationToken cancellationToken = default)
            {
                if (_info.CheckRole(role))//||( _info.GetRoleHandler((await GetNormalizedRoleNameAsync(role,cancellationToken)))?.Name ?? roleName) != roleName)
                {
                    throw new NotImplementedException();
                }
                await base.SetNormalizedRoleNameAsync(role, roleName, cancellationToken);
            }
        }
        /*
        public class RoleDbRoleManager<TUser, TRole, TContext> : RoleManager<TRole> where TUser : IdentityUser<string> where TContext : DbContext where TRole : IdentityRole<string>
        {
            public RoleDbRoleManager(IRoleStore<TRole> store, IEnumerable<IRoleValidator<TRole>> roleValidators, ILookupNormalizer keyNormalizer, IdentityErrorDescriber errors, ILogger<RoleManager<TRole>> logger)
                : base(store,roleValidators,keyNormalizer,errors,logger)
            {

            }
            public override Task<IdentityResult> CreateAsync(TRole role)
            {
                return base.CreateAsync(role);
            }
            public override Task<IdentityResult> DeleteAsync(TRole role)
            {
                return base.DeleteAsync(role);
            }
            public override Task<TRole> FindByIdAsync(string roleId)
            {
                return base.FindByIdAsync(roleId);
            }
            public override Task<TRole> FindByNameAsync(string roleName)
            {
                return base.FindByNameAsync(roleName);
            }
            public override Task<bool> RoleExistsAsync(string roleName)
            {
                return base.RoleExistsAsync(roleName);
            }
            public override Task<IdentityResult> SetRoleNameAsync(TRole role, string name)
            {
                return base.SetRoleNameAsync(role,name);
            }
            public override Task<IdentityResult> UpdateAsync(TRole role)
            {
                return base.UpdateAsync(role);
            }
            protected override async Task<IdentityResult> UpdateRoleAsync(TRole role)
            {
                return await base.UpdateRoleAsync(role);
            }
        }*/
        /*   public class RoleDbRolesStore<TUser, TRole, TContext> : RoleStore<TRole> where TUser : IdentityUser<string> where TContext : DbContext where TRole : IdentityRole<string>
           {
               public RoleDbRolesStore(RoleDBSingleton<TUser, TRole, TContext> info, TContext context, IdentityErrorDescriber describer = null)
               : base(context, describer)
           {
           }
           public virtual Task<TRole> FindByIdAsync(string id, CancellationToken cancellationToken = default);
           public virtual Task<TRole> FindByNameAsync(string normalizedName, CancellationToken cancellationToken = default);

           }
       }*/
        public class RoleDbRoleRoleManager<TUser, TRole, TContext> : RoleManager<TRole> where TContext : DbContext where TRole : IdentityRole<string> where TUser : IdentityUser<string>
        {
            private readonly RoleDBSingleton<TUser, TRole, TContext> _info;

            public RoleDbRoleRoleManager(RoleDBSingleton<TUser, TRole, TContext> info, IRoleStore<TRole> store, IEnumerable<IRoleValidator<TRole>> roleValidators, ILookupNormalizer keyNormalizer, IdentityErrorDescriber errors, ILogger<RoleManager<TRole>> logger)
                : base(store, roleValidators, keyNormalizer, errors, logger)
            {
                _info = info;
            }
            public override Task<bool> RoleExistsAsync(string roleName)
            {
                return base.RoleExistsAsync(roleName);
            }
            public override Task<IdentityResult> UpdateAsync(TRole role)
            {
                return base.UpdateAsync(role);
            }
            protected override Task<IdentityResult> ValidateRoleAsync(TRole role)
            {
                return base.ValidateRoleAsync(role);
            }
            public override Task<IdentityResult> AddClaimAsync(TRole role, Claim claim)
            {
                return base.AddClaimAsync(role, claim);
            }
            public override Task<IdentityResult> RemoveClaimAsync(TRole role, Claim claim)
            {
                return base.RemoveClaimAsync(role, claim);
            }
            public override async Task<IdentityResult> CreateAsync(TRole role)
            {
                return await base.CreateAsync(role);
            }
            public override Task<IdentityResult> DeleteAsync(TRole role)
            {
                return base.DeleteAsync(role);
            }
        }

        /*  public class RoleDBUserManager<TUser, TRole, TContext> : UserManager<TUser> where TUser : IdentityUser<string> where TContext : DbContext, IRoldeDBCOntext<TRole> where TRole : IdentityRole<string>
          {
              private readonly RoleDBSingleton<TUser, TRole, TContext> _info;
              private readonly TContext _context;

              public RoleDBUserManager(RoleDBSingleton<TUser, TRole, TContext> info,IUserStore<TUser> store, TContext context, IOptions<IdentityOptions> optionsAccessor, IPasswordHasher<TUser> passwordHasher, IEnumerable<IUserValidator<TUser>> userValidators, IEnumerable<IPasswordValidator<TUser>> passwordValidators, ILookupNormalizer keyNormalizer, IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<TUser>> logger)
                  : base(store,optionsAccessor,passwordHasher,userValidators,passwordValidators,keyNormalizer,errors,services,logger)
              {
                  _info = info;
                  _context = context;
              }
              public override Task<IdentityResult> RemoveFromRoleAsync(TUser user, string role)
              {
                  return base.RemoveFromRoleAsync(user, role);
              }
              public override Task<IdentityResult> AddToRolesAsync(TUser user, IEnumerable<string> roles)
              {
                  return base.AddToRolesAsync(user, roles);
              }
              public override Task<IdentityResult> AddToRoleAsync(TUser user, string role)
              {
                  return base.AddToRoleAsync(user, role);
              }
              public override Task<IList<string>> GetRolesAsync(TUser user)
              {
                  return base.GetRolesAsync(user);
              }
              public override Task<IList<TUser>> GetUsersInRoleAsync(string roleName)
              {
                  return base.GetUsersInRoleAsync(roleName);
              }
              public override async Task<bool> IsInRoleAsync(TUser user, string role)
              {
                  var def = this._info.GetRoleHandler(role.ToUpper());
                  if (def != null)
                  {
                      return await def.IsInRoleASync(_context, user);
                  }
                  return await base.IsInRoleAsync(user, role);
              }
              public override Task<IdentityResult> RemoveFromRolesAsync(TUser user, IEnumerable<string> roles)
              {
                  return base.RemoveFromRolesAsync(user, roles);
              }
              public override bool SupportsUserRole => base.SupportsUserRole;
          }*/
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
            public override async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
            {
                var r = await base.CreateAsync(user, cancellationToken);

                if (r == IdentityResult.Success)
                {
                    await _info.CreateDefaultRulesAsync(this, user);
                    await _context.SaveChangesAsync();
                }
                return r;
            }
            public override async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
            {
                await this._info.RemoveRolesAsync(user, _context);
                return await base.DeleteAsync(user, cancellationToken);
            }
            public override async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
            {
                await base.AddToRoleAsync(user, normalizedRoleName, cancellationToken);

                Trace.Assert(await base.IsInRoleAsync(user, normalizedRoleName, cancellationToken));

                var def = this._info.GetRoleHandler(normalizedRoleName);
                if (def != null)
                {
                    await def.AddRoleASync(_context, user);
                }
            }
            public override async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken = default)
            {
                List<string> result = new List<string>();
                result.AddRange(await base.GetRolesAsync(user, cancellationToken));
                //      result.AddRange(await _info.GetRolesAsync(_context,user));
                // await _info.SyncUser(_context, this, user, _services);
                return result;// await base.GetRolesAsync(user, cancellationToken);
            }
            public override async Task<IList<TUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
            {
                /*   var def = this._info.GetRoleHandler(normalizedRoleName);
                   if (def != null)
                   {
                       return await def.GetUsersAsync(_context, this, cancellationToken, _services);
                   }*/
                return await base.GetUsersInRoleAsync(normalizedRoleName, cancellationToken);
            }
            public override async Task<bool> IsInRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
            {
                /*      var def = this._info.GetRoleHandler(normalizedRoleName);
                      if (def != null)
                      {
                          return await def.IsInRoleASync(_context, user);
                      }*/
                return await base.IsInRoleAsync(user, normalizedRoleName, cancellationToken);
            }
            public override async Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
            {
                var def = this._info.GetRoleHandler(normalizedRoleName);
                if (def != null)
                {
                    await def.RemoveRoleAsync(_context, user);
                }
                await base.RemoveFromRoleAsync(user, normalizedRoleName, cancellationToken);
            }
            protected override async Task<TRole> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
            {
                /*   var def = this._info.GetRoleHandler(normalizedRoleName);
                   if (def != null)
                   {
                       return def.Role;
                   }*/
                return await base.FindRoleAsync(normalizedRoleName, cancellationToken);
            }
            protected override async Task<IdentityUserRole<string>> FindUserRoleAsync(string userId, string roleId, CancellationToken cancellationToken)
            {
                var result = await base.FindUserRoleAsync(userId, roleId, cancellationToken);

                if (result == null)
                {

                }
                return result;
            }
        }
    }
    public static class Extensions
    {
        public static IdentityBuilder AddRoleDBEntityFrameworkStores<TUser, TRole, TContext>(this IdentityBuilder builder)
            where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            builder.Services.AddSingleton<RoleDBSingleton<TUser, TRole, TContext>>();
            builder.Services.AddSingleton<UserAdminLib.Roles.IRoleSingleton<TRole>>(x => x.GetService<RoleDBSingleton<TUser, TRole, TContext>>());

           // builder.AddRoleManager<RoleDbRoleManager<TUser,TRole,TContext>>();
            builder.AddUserStore<RoleDBUserStore<TUser, TRole, TContext>>();
            builder.AddRoleStore<RoleDbRoleStore<TUser,TRole, TContext>>();
         //   builder.AddRoleManager<RoleDbRoleRoleManager<TUser, TRole, TContext>>();
      //      builder.AddRoleStore<RoleStore<TRole, TContext, string>>();

            return builder;
        }
        public async static Task SyncRoleDb<TContext>(this IServiceProvider service) where TContext : DbContext
        {
            await SyncRoleDb<IdentityUser, TContext>(service);
        }
        public async  static Task SyncRoleDb<TUser, TContext>(this IServiceProvider service) where TUser : IdentityUser<string> where TContext : DbContext
        {
            await SyncRoleDb<IdentityUser, IdentityRole, TContext>(service);
        }
        public async static Task SyncRoleDb<TUser, TRole, TContext>(this IServiceProvider services) where TUser : IdentityUser<string> where TRole : IdentityRole<string> where TContext : DbContext
        {
            var info = services.GetRequiredService<RoleDb.RoleDBSingleton<TUser, TRole, TContext>>();
            var options = services.GetRequiredService<IOptions<RoleDBOptions>>();
            var rolestore = services.GetRequiredService<IRoleStore<TRole>>();
            var userstore = services.GetRequiredService<IUserStore<TUser>>();
            var roles = services.GetRequiredService<RoleManager<TRole>>();
            var users = services.GetRequiredService<UserManager<TUser>>();
            var context = services.GetRequiredService<TContext>();

            await info.SyncRoles(context, options.Value, roles); // add roles from options
        //    info.SyncUsers(context, userstore as RoleDBUserStore<TUser, TRole, TContext>);
        }
    }
}
