namespace Pryde.Domain.Constants;

public static class RoleNames
{
    public const string Passenger = "Passenger";
    public const string Driver = "Driver";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string AdminOrSuperAdmin = Admin + "," + SuperAdmin;
}
