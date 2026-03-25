namespace SmartExpense.Core.Constants;

public static class IdentityRoleConstants
{
    public const string Admin = "Admin";
    public const string User = "User";
    public static readonly Guid AdminRoleGuid = new("374d637f-5ce8-4982-b351-b4ed3a299a0e");
    public static readonly Guid UserRoleGuid = new("c5c2df52-caa8-4565-a7be-ceab96d64a75");
}