using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Role
{
    [Key]
    public int RoleId { get; set; }
    public string RoleName { get; set; } = null!;
    public ICollection<User> Users { get; set; } = new List<User>();
}