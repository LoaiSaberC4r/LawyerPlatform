using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Authorization;
using LawyerPlatform.Api.Contracts.Auth;
using LawyerPlatform.Application.Features.Auth.ChangePassword;
using LawyerPlatform.Application.Features.Auth.ForgotPassword.RequestOtp;
using LawyerPlatform.Application.Features.Auth.ForgotPassword.ResetPassword;
using LawyerPlatform.Application.Features.Auth.ForgotPassword.VerifyOtp;
using LawyerPlatform.Application.Features.Auth.Login;
using LawyerPlatform.Application.Features.Auth.RegisterClient;
using LawyerPlatform.Application.Features.Auth.RegisterLawyer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("clients/register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterClient(RegisterAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterClientCommand(
            request.FullName,
            request.UserName,
            request.Email,
            request.PhoneNumber,
            request.Password), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpPost("lawyers/register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterLawyer(RegisterAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterLawyerCommand(
            request.FullName,
            request.UserName,
            request.Email,
            request.PhoneNumber,
            request.Password), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LoginQuery(request.UserNameOrEmail, request.Password), cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpPost("forgot-password/request-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery-request")]
    [ProducesResponseType(typeof(RequestPasswordResetOtpResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestPasswordResetOtp(
        RequestPasswordResetOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestPasswordResetOtpCommand(request.Email),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status202Accepted, result.Value)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpPost("forgot-password/verify-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery-verify")]
    [ProducesResponseType(typeof(VerifyPasswordResetOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyPasswordResetOtp(
        VerifyPasswordResetOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new VerifyPasswordResetOtpCommand(request.RequestId, request.Otp),
            cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpPost("forgot-password/reset")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery-reset")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ResetPasswordCommand(
                request.RequestId,
                request.ResetToken,
                request.NewPassword,
                request.ConfirmPassword),
            cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpPost("change-password")]
    [Authorize]
    [AllowPasswordChangeRequired]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }
}
