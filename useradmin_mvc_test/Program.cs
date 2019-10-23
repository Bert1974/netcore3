using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using useradmin_mvc_test.Data;
using useradmin_mvc_test.Models;

namespace useradmin_mvc_test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host=CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                Initialize(scope).Wait();
            }
            host.Run();
        }

        private static async Task Initialize(IServiceScope scope)
        {
            //  var test = scope.ServiceProvider.GetServices(typeof(RoleManager<IdentityRole>)).ToArray();
            //    using (var users = host.Services.GetRequiredService<UserManager<ApplicationUser>>())
            var userstore = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = userstore.FindByNameAsync("bbruggeman1974@gmail.com", default).GetAwaiter().GetResult();

           if (user != null)
            {
                user.IsUseradmin = true;

                context.Update(user);
                context.SaveChanges();

            /*    if (!await users.IsInRoleAsync(user, UserAdminLib.Constants.Role))
                {
                    await users.AddToRoleAsync(user, UserAdminLib.Constants.Role);
                }*/
            }
            //      var claim = new Claim(ClaimTypes.Role, useradminlib.Constants.Role);

            //       (userstore as Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserOnlyStore<IdentityUser>).AddClaimsAsync(user, new Claim[] { claim }, default).Wait();

            //     var info = scope.ServiceProvider.GetRequiredService<MyRoleSingleton<ApplicationUser, IdentityRole, ApplicationDbContext>>();
            //    var options = scope.ServiceProvider.GetRequiredService<IOptions<MyRoleManagerOptions>>();

            //      CheckAddRole(roles, useradminlib.Constants.Role).Wait();
            //     CheckAddRole(roles, "Admin").Wait();
            //    CheckAddRole(roles, "Customer").Wait();

            //        info.SyncRoles(options.Value, rolesstore); // add roles from options

            //      info.SyncUsers(context, userstore as userstore<ApplicationUser, IdentityRole, ApplicationDbContext>);

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
