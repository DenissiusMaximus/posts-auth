using System;

namespace auth.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Login { get; set; } = null!;
    
    public string PasswordHash { get; set; } = null!;
}
