using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using UserAdminLib.Configuration;

namespace UserAdminLib
{
    #region extensions

    public static class UserAdminExtensions
    {
        #region private classes
        private class MyUserClaimsPrincipalFactory<TUser> : UserClaimsPrincipalFactory<TUser>
            where TUser : IdentityUser<string>
        {
            private readonly Configuration.UserAdminOptions _options;

            public MyUserClaimsPrincipalFactory(UserManager<TUser> userManager, IOptions<IdentityOptions> optionsAccessor, IOptions<Configuration.UserAdminOptions> options)
                    : base(userManager, optionsAccessor)
            {
                _options = options.Value;
            }

            protected override async Task<ClaimsIdentity> GenerateClaimsAsync(TUser user)
            {
                var identity = await base.GenerateClaimsAsync(user);

                if (_options.Field != null)
                {
                    if ((bool)typeof(TUser).GetProperty(_options.Field).GetValue(user, new object[0]))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, Constants.Role));
                    }
                }
                return identity;
            }
        }
        private class MyRolesClaimsPrincipalFactory<TUser, TRole> : UserClaimsPrincipalFactory<TUser, TRole>
            where TUser : IdentityUser<string>
            where TRole : IdentityRole<string>
        {
            private readonly Configuration.UserAdminOptions _options;

            public MyRolesClaimsPrincipalFactory(UserManager<TUser> userManager, RoleManager<TRole> rolesManager, IOptions<IdentityOptions> optionsAccessor, IOptions<Configuration.UserAdminOptions> options)
                    : base(userManager, rolesManager, optionsAccessor)
            {
                _options = options.Value;
            }

            protected override async Task<ClaimsIdentity> GenerateClaimsAsync(TUser user)
            {
                var identity = await base.GenerateClaimsAsync(user);

                if (_options.Field != null)
                {
                    if ((bool)typeof(TUser).GetProperty(_options.Field).GetValue(user, new object[0]))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, Constants.Role));
                    }
                }
                return identity;
            }
        }
        private class UseradminControllerFeatureProvider<TUser, TContext> : IApplicationFeatureProvider<ControllerFeature>
            where TContext : DbContext
            where TUser : IdentityUser<string>
        {
            public Type Type => typeof(Controllers.UserAdminController<,>).MakeGenericType(typeof(TUser), typeof(TContext));

            public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            {
                feature.Controllers.Add(Type.GetTypeInfo());
            }
        }
        private class UseradminControllerFeatureProvider<TUser, TRole, TContext> : IApplicationFeatureProvider<ControllerFeature>
            where TContext : DbContext
            where TUser : IdentityUser<string>
            where TRole : IdentityRole<string>
        {
            public Type Type => typeof(Controllers.UserAdminRolesController<,,>).MakeGenericType(typeof(TUser), typeof(TRole), typeof(TContext));

            public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            {
                feature.Controllers.Add(Type.GetTypeInfo());
            }
        }
        #endregion

        public static async Task SyncUserAdmin<TUser, TRole, TContext>(this IServiceProvider services) where TUser : IdentityUser<string>, new() where TRole : IdentityRole<string> where TContext : DbContext
        {
            var options = services.GetRequiredService<IOptions<UserAdminOptions>>().Value;

            if (options.Field == null)
            {
                var roles = services.GetRequiredService<RoleManager<TRole>>();

                if (await roles.FindByNameAsync(Constants.Role) ==null)
                {
                    var role = (TRole)Activator.CreateInstance(typeof(TRole), new object[] { Constants.Role });
                    await roles.CreateAsync(role);
                }
            }
        }
        /// <summary>
        /// Make the UserAdminController available to MVC as Controller
        /// </summary>
        /// <typeparam name="TUser"></typeparam>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IMvcBuilder ConfigureUserAdmin<TUser, TContext>(this IMvcBuilder builder)
            where TContext : DbContext
             where TUser : IdentityUser<string>
        {
            var p = new UseradminControllerFeatureProvider<TUser, TContext>();
            GenericControllerNameConvention.RegisterController(p.Type);
            return builder.ConfigureApplicationPartManager(apm => apm.FeatureProviders.Add(p));
        }
        /// <summary>
        /// Make the UserAdminController available to MVC as Controller
        /// </summary>
        /// <typeparam name="TUser"></typeparam>
        /// <typeparam name="TRole"></typeparam>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IMvcBuilder ConfigureUserAdmin<TUser, TRole, TContext>(this IMvcBuilder builder)
            where TContext : DbContext
            where TUser : IdentityUser<string>
            where TRole : IdentityRole<string>
        {
            var p= new UseradminControllerFeatureProvider<TUser, TRole, TContext>();
            GenericControllerNameConvention.RegisterController(p.Type);
            return builder.ConfigureApplicationPartManager(apm => apm.FeatureProviders.Add(p));
        }
        /// <summary>
        /// required, register cookie for useradmin authenticationscheme
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configurefunc"></param>
        /// <returns></returns>
        public static AuthenticationBuilder AddUserAdminAuthenticationCookie(this AuthenticationBuilder builder, Action<CookieAuthenticationOptions> configurefunc = null)
        {
            return builder.AddCookie(Constants.Scheme,
                    options =>
                    {
                        options.Cookie.HttpOnly = true;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // https
                        options.Cookie.SameSite = SameSiteMode.Strict;
                        options.ForwardDefault = "Identity.Application";

                        configurefunc?.Invoke(options);
                    });
        }
        /// <summary>
        /// required, registers policy, used to athorize acces to the useradmin-webpages
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configurefunc"></param>
        /// <returns></returns>
        public static IServiceCollection AddUserAdminPolicy(this IServiceCollection services, Action<AuthorizationPolicyBuilder> configurefunc = null)
        {
            var hasroles = services.Where(_s => _s.ServiceType.FullName.StartsWith("Microsoft.AspNetCore.Identity.RoleManager")).Any();
            // api user claim policy
            return services.AddAuthorization(options =>
            {
                options.AddPolicy(Constants.Policy,
                    policy =>
                    {
                        policy.AuthenticationSchemes.Add(Constants.Scheme);
                        policy.RequireAuthenticatedUser();
                     //   policy.RequireRole(Constants.Role);

                            configurefunc?.Invoke(policy);
                    });
            });
        }
        /// <summary>
        /// adds the static files for useradmin, and prepare route, call before UseEndpoints
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseUseradmin(this IApplicationBuilder app)
        {
            var options = app.ApplicationServices.GetRequiredService<IOptions<UserAdminLib.Configuration.UserAdminOptions>>();

            GenericControllerNameConvention.SetURL(null, options.Value.Url);

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new ManifestEmbeddedFileProvider(typeof(UserAdminExtensions).Assembly, "wwwroot"),
                RequestPath = "/useradmin"
            });
            return app;
        }
        /// <summary>
        /// use if UserAdminOptions.Field non null, this will add the handler to add the Role Claim
        /// </summary>
        /// <typeparam name="TUser"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IdentityBuilder RegisterUserAdminClaims<TUser>(this IdentityBuilder builder)
            where TUser : IdentityUser<string>
        {
            return builder.AddClaimsPrincipalFactory<MyUserClaimsPrincipalFactory<TUser>>();
        }
        /// <summary>
        /// use if UserAdminOptions.Field non null, this will add the handler to add the Role Claim
        /// </summary>
        /// <typeparam name="TUser"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IdentityBuilder RegisterUserAdminClaims<TUser,TRole>(this IdentityBuilder builder)
            where TUser : IdentityUser<string>
            where TRole : IdentityRole<string>
        {
            return builder.AddClaimsPrincipalFactory<MyRolesClaimsPrincipalFactory<TUser, TRole>>();
        }
    }
    #endregion

    #region internals
    internal static class Extensions
    {
        public static ViewModels.UserInfo ToUserInfo<TKey>(this IdentityUser<TKey> u)
            where TKey : IEquatable<TKey>
        {
            return new ViewModels.UserInfo()
            {
                Id = u.Id.ToString(),
                UserName = u.UserName,
                Email = u.Email,
                AccessFailedCount = u.AccessFailedCount,
                EmailConfirmed = u.EmailConfirmed,
                LockoutEnabled = u.LockoutEnabled,
                LockoutEnd = u.LockoutEnd,
                PhoneNumber = u.PhoneNumber,
                PhoneNumberConfirmed = u.PhoneNumberConfirmed,
                TwoFactorEnabled = u.TwoFactorEnabled
            };
        }
        public static bool IsDerived(this Type type, Type tofind)
        {
            for (; type != null && type!= tofind&& type.GetGenericTypeDefinition() != tofind; type = type.BaseType) { }
            return type != null;
        }
    }
    #endregion
}
