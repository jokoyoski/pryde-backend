using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty!;
        public string PasswordHash { get; set; } = string.Empty!;
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneNumberVerified { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public UserStatus Status { get; set; }
        public Profile? Profile { get; set; }
        public KycVerification? KycVerification { get; set; }
        public ICollection<UserRole> UserRoles {  get; set; } = new List<UserRole>();
        public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

    }
}
