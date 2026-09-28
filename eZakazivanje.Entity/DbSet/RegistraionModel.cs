using System;
using System.ComponentModel.DataAnnotations;

namespace eZakazivanje.Entity.DbSet;

public class RegistraionModel
{
        [Required(ErrorMessage = "User Name is required")]
        public string? Username { get; set; }
        [Required(ErrorMessage = "Name is required")]
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? BusinessName { get; set; }
        public string? BusinessDescription { get; set; }

        [EmailAddress]
        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
}
