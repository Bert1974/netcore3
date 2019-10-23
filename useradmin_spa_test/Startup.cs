using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoleDb;
using RoleDb.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using useradmin.Data;
using useradmin.Entities;
using UserAdminLib;
using UserAdminLib.Configuration;

namespace useradmin
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));

            services.Configure<RoleDBOptions>(options =>
            {
                options.Roles = new RoleDBRole[] { new RoleDBRole() { Name = "Customer", CustomerRoleHandleType = typeof(CustomerRoleHandler).AssemblyQualifiedName } };
            });

            /*   services.Configure<CookiePolicyOptions>(options =>
                {
                    options.CheckConsentNeeded = context => true;
                    options.MinimumSameSitePolicy = SameSiteMode.None;
                    options.ConsentCookie.Name = "Cookie";
                });*/
            services.Configure<UserAdminLib.Configuration.UserAdminOptions>(options =>
                {
                    options.wwwdir = "/Identity";
                });

            //    var test1 =services.Where(_d => _d.ServiceType == typeof(UserManager<ApplicationUser>) || _d.ImplementationType == typeof(UserManager<ApplicationUser>)).ToArray();

            //IdentityBuilder builder = new IdentityBuilder(typeof(ApplicationUser), typeof(IdentityRole), services);
            var builder = services.AddIdentity<ApplicationUser, IdentityRole>()
                .RegisterUserAdminClaims<ApplicationUser,IdentityRole>();

        //    var test = UserClaimsPrincipalFactory <;

            builder = builder.AddRoleDBEntityFrameworkStores<ApplicationUser, IdentityRole,ApplicationDbContext>()
                    //    .AddRoles<IdentityRole>()
                  //  .AddEntityFrameworkStores<ApplicationDbContext,>()
                  //  .ReplaceMyRoleStore<ApplicationUser, IdentityRole, ApplicationDbContext>()
                  .AddDefaultUI()
                  .AddDefaultTokenProviders()
                 ;

            //       var test = builder.Services.SingleOrDefault(_d => _d.ServiceType.HasInterface(typeof(IRoleStore<IdentityRole>)) || _d.ImplementationType.HasInterface(typeof(IRoleStore<IdentityRole>)));

            services.AddIdentityServer(_opt =>
                {
                })
                    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>();

            services.AddAuthentication(_opt =>
            {
                _opt.DefaultScheme = IdentityConstants.ApplicationScheme;
            })
                .AddIdentityServerJwt()
                .AddUserAdminAuthenticationCookie(); // register cookie for for Identity.Application

            // api user claim policy
            services.AddUserAdminPolicy(); // AddAuthorization->AddPolicy

            services.AddControllersWithViews().ConfigureUserAdmin<ApplicationUser, IdentityRole, ApplicationDbContext>(); // adds controller with generics
            services.AddRazorPages();

            //services.AddSession();

            // In production, the Angular files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/dist";
            });
          //  var test = builder.Services.Where(_d => _d.ServiceType == typeof(UserClaimsPrincipalFactory<ApplicationUser>) || _d.ImplementationType == typeof(UserClaimsPrincipalFactory<ApplicationUser>)).ToArray();
        }
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            if (!env.IsDevelopment())
            {
                app.UseSpaStaticFiles();
            }

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseIdentityServer();

     //       app.UseUseradmin(); // adds static files, using through options.wwwdir

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });

            app.UseSpa(spa =>
            {
                // To learn more about options for serving an Angular SPA from ASP.NET Core,
                // see https://go.microsoft.com/fwlink/?linkid=864501

                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseAngularCliServer(npmScript: "start");
                }
            });
        }
    }
}
