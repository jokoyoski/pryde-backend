
using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class UserRole : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public User User { get; set; } = default!;
        public Role Role { get; set; } = default!;
    }
}
