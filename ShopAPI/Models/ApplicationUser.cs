using Microsoft.AspNetCore.Identity;

namespace ShopAPI.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }
    }
}
