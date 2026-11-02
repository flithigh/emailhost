namespace EmailHost.Smtp.Security;

public interface IPasswordHasher
{
    bool VerifyPassword(string password, string passwordHash);
}

public class BCryptPasswordHasher : IPasswordHasher
{
    public bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}

