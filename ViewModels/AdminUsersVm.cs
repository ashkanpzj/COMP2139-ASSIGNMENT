using System.Collections.Generic;

namespace Assignment1.ViewModels;

public record AdminUsersVm(IEnumerable<AdminUserRow> Users);

public record AdminUserRow(
    string Id,
    string Email,
    bool EmailConfirmed,
    string FullName,
    string? PhoneNumber,
    string[] Roles);





