using System.ComponentModel.DataAnnotations;

namespace ABCRetails.Models
{
    public class AddPersonWithImage
    {
        [Required]

        public string? Name {  get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [Display(Name = "Profile Picture")]

        public IFormFile? ProfileImage { get; set; }
    }
}
