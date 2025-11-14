using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Biblioteka.Models
{
    public class UserRoleViewModel
    {
        public User User { get; set; } = new User();
        // Dostarczamy prosty SelectList w modelu, aby uniknąć redundancji
        public SelectList AvailableRoles { get; set; } = new SelectList(new List<string> { "user", "worker", "admin" });
    }
}