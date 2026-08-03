namespace LawyerPlatform.Application.Abstractions.Authentication;

public interface IAccountIdentifierNormalizer
{
    string NormalizeUserName(string userName);
    string NormalizeEmail(string email);
}
