using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoleDb;
using RoleDb.Configuration;
using System.Threading.Tasks;
using useradmin.Data;
using useradmin.Entities;
using UserAdminLib;

namespace useradmin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                CreateRoles(scope).Wait();
            }
       /*     using (var scope = host.Services.CreateScope())
            {
                //  var test = scope.ServiceProvider.GetServices(typeof(RoleManager<IdentityRole>)).ToArray();
                //    using (var users = host.Services.GetRequiredService<UserManager<ApplicationUser>>())
                using (var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>())
                using (var rolesstore = scope.ServiceProvider.GetRequiredService<IRoleStore<IdentityRole>>())
                using (var userstore = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>())
                using (var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                using (var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>())
                {
                   var info = scope.ServiceProvider.GetRequiredService<RoleDBSingleton<ApplicationUser, IdentityRole, ApplicationDbContext>>();
                    var options = scope.ServiceProvider.GetRequiredService<IOptions<RoleDBOptions>>();

                    CheckAddRole(roles, UserAdminLib.Constants.Role).Wait();
                    CheckAddRole(roles, "Admin").Wait();
                //    CheckAddRole(roles, "Customer").Wait();

                    info.SyncRoles(options.Value, rolesstore); // add roles from options

                    info.SyncUsers(context, userstore as RoleDBUserStore<ApplicationUser, IdentityRole, ApplicationDbContext>);

                    var user = users.FindByEmailAsync("bbruggeman1974@gmail.com").GetAwaiter().GetResult();

                    users.AddToRoleAsync(user, UserAdminLib.Constants.Role).Wait();
                }
            }*/

            host.Run();
        }

        private static async Task CreateRoles(IServiceScope scope)
        {
            await scope.ServiceProvider.SyncRoleDb<ApplicationUser, IdentityRole, ApplicationDbContext>(); // add roles from RoleDbOptions
            await scope.ServiceProvider.SyncUserAdmin<ApplicationUser, IdentityRole, ApplicationDbContext>(); // add admin roll if needed

            using (var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>())
            {
                var user = await users.FindByEmailAsync("bbruggeman1974@gmail.com"); // need a admin sometimes

                if (user != null)
                {
                    if (!await users.IsInRoleAsync(user, UserAdminLib.Constants.Role))
                    {
                        await users.AddToRoleAsync(user, UserAdminLib.Constants.Role);
                    }
                }
            }
        }

        private static async Task CheckAddRole(RoleManager<IdentityRole> roles, string role)
        {
            try
            {
                var roleCheck = await roles.RoleExistsAsync(role);
                if (!roleCheck)
                {
                    //create the roles and seed them to the database
                    await roles.CreateAsync(new IdentityRole(role));
                }
            }catch
            { }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
