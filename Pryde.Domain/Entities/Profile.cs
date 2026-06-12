using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class Profile : BaseEntity
    {
        public Guid UserId { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string? ProfilePhotoUrl { get; set; }

        public User User { get; set; } = null!;
    }
}
