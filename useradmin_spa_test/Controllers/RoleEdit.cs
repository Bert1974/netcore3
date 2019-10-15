using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleDb;
using RoleDb.Configuration;
using RoleDb.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using useradmin.Data;
using useradmin.Entities;
using useradmin.ViewModels;

namespace useradmin
{
    [ViewComponent(Name = "CustomerEdit")]
    public class CustomerEditViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public CustomerEditViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }
        public IViewComponentResult Invoke(string id, CustomerRoleHandler handler, string returnurl)
        {
            return View(new CustomerInfo() { Role = handler.Role.Name, Customer = _context.UserInfoTable.Single(_u => _u.ApplicationUserId == id), ReturnURL = returnurl });
        }
    }

    [Authorize(policy: UserAdminLib.Constants.Policy)]
    [Route("/api/roleedit/[action]")]
    public class RoleEditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoleEditController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCustomer(UserInfo data,string ReturnURL)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.UserInfoTable.SingleOrDefaultAsync(_u => _u.Id == data.Id);

                if (user != null)
                {
                    user.Location = data.Location;

                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
            }
            return new RedirectResult(ReturnURL,false);
        }
    }

    public class CustomerRoleHandler : BaseRoleHandler<ApplicationUser, IdentityRole, ApplicationDbContext>
    {
        public override string ViewComponent => "CustomerEdit";

        public CustomerRoleHandler(RoleDBRole roleinfo, IdentityRole role) : base(roleinfo, role)
        {
        }
        public override async Task<bool> IsInRoleASync(ApplicationDbContext db, ApplicationUser user)
        {
            return await db.UserInfoTable.AnyAsync(_u => _u.ApplicationUserId == user.Id);
        }
        public override async Task<IList<ApplicationUser>> GetUsersAsync(ApplicationDbContext context, IUserStore<ApplicationUser> userstore, CancellationToken cancellationToken, IServiceProvider services)
        {
            return await base.GetUsers(userstore, context.UserInfoTable.Select(_u => _u.ApplicationUserId));
        }
        public override async Task AddRoleASync(ApplicationDbContext context, ApplicationUser user)
        {
            var info = await context.UserInfoTable.SingleOrDefaultAsync(_u => _u.ApplicationUserId == user.Id);

            if (info == null)
            {
                info = new UserInfo() { ApplicationUser = user, Location = "" };
                await context.UserInfoTable.AddAsync(info);
                await context.SaveChangesAsync();
            }
        }
        public override async Task RemoveRoleAsync(ApplicationDbContext context, ApplicationUser user)
        {
            var info = await context.UserInfoTable.SingleOrDefaultAsync(_u => _u.ApplicationUserId == user.Id);

            if (info != null)
            {
                context.UserInfoTable.Remove(info);
                await context.SaveChangesAsync();
            }
        }
    }

}
