using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class UserRole : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public User User { get; set; } = default!;
        public Roles Role { get; set; } = default!;
    }
}
