using BuildingBlock.Application.Abstraction;
using LawyerPlatform.Application.Features.Auth.Common;

namespace LawyerPlatform.Application.Features.Auth.Login;

public sealed record LoginQuery(string UserNameOrEmail, string Password) : IQuery<AuthTokenResponse>;
