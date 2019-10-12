
using useradmin.Entities;

namespace useradmin.ViewModels
{
    public class CustomerInfo
    {
        public string Role { get; set; }
        public UserInfo Customer { get; set; }
        public string ReturnURL { get; set; }
    }
}
