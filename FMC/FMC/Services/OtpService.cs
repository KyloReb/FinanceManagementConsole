using FMC.Data;
using FMC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FMC.Services;

/// <summary>
/// Generates, stores, and validates OTP verification codes.
/// </summary>
public class OtpService : IOtpService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<OtpService> _logger;

    public OtpService(ApplicationDbContext dbContext, IEmailService emailService, ILogger<OtpService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a secure random 6-digit OTP, saves it to the database with a 10-minute expiry, and emails it to the user.
    /// Implements a 60-second rate limit to prevent email spamming.
    /// </summary>
    /// <param name="userId">The GUID string of the user requesting the OTP.</param>
    /// <param name="email">The email addresses to send the OTP to.</param>
    /// <param name="otpType">The categorical classification for the OTP (e.g. "EmailVerification").</param>
    /// <returns>The generated plaintext 6-digit OTP string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the user requests an OTP within 60 seconds of their previous request.</exception>
    public async Task<string> GenerateAndSendOtpAsync(string userId, string email, string otpType)
    {
        // 1. Check for Rate Limiting (Prevent spamming within 60s)
        var recentOtp = await _dbContext.UserOtpVerifications
            .Where(o => o.UserId == userId && o.OtpType == otpType)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentOtp != null && (DateTime.UtcNow - recentOtp.CreatedAt).TotalSeconds < 60)
        {
            throw new InvalidOperationException("Please wait 60 seconds before requesting another code.");
        }

        // 2. Invalidate any existing active OTPs of this type for the user
        var existingOtps = await _dbContext.UserOtpVerifications
            .Where(o => o.UserId == userId && o.OtpType == otpType && o.IsUsed == false)
            .ToListAsync();

        foreach (var existingOtp in existingOtps)
        {
            existingOtp.IsUsed = true; // Mark old ones as used/invalid
        }

        // 3. Generate a secure 6-digit random code
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();

        // 4. Save to database with 10-minute expiration
        var newOtp = new UserOtpVerification
        {
            UserId = userId,
            OtpCode = code,
            OtpType = otpType,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            FailedAttempts = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UserOtpVerifications.Add(newOtp);
        var saveResult = await _dbContext.SaveChangesAsync();
        _logger.LogInformation("DATABASE SAVED: {Rows} rows. Created OTP {Code} for user {UserId} expiring at {ExpiresAt}", saveResult, code, userId, newOtp.ExpiresAt);

        // 4. Send the code via Email
        var subject = "Your Secure Verification Code";
        var body = $@"
            <div style='font-family: Arial, sans-serif; text-align: center; padding: 20px;'>
                <h2>Finance Management Console Verification</h2>
                <p>Your verification code is:</p>
                <h1 style='color: #4f7cff; font-size: 36px; letter-spacing: 5px;'>{code}</h1>
                <p>This code will expire in 10 minutes. Do not share this code with anyone.</p>
            </div>";

        await _emailService.SendEmailAsync(email, subject, body, isHtml: true);

        _logger.LogInformation("Generated {OtpType} OTP for user {UserId}. DEVELOPER OTP CODE: {Code}", otpType, userId, code);

        return code;
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyOtpAsync(string userId, string code, string otpType)
    {
        var otpRecord = await _dbContext.UserOtpVerifications
            .Where(o => o.UserId == userId && 
                        o.OtpType == otpType && 
                        o.IsUsed == false)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        // No active OTP or expired
        if (otpRecord == null)
        {
            var allUserOtps = await _dbContext.UserOtpVerifications.Where(o => o.UserId == userId).ToListAsync();
            _logger.LogWarning("Failed OTP verification: OTP record NOT FOUND for user {UserId}. Found {Count} total historical OTPs for this user. Types: {Types}, IsUsed: {UsedStats}", 
                userId, allUserOtps.Count, string.Join(",", allUserOtps.Select(o => o.OtpType)), string.Join(",", allUserOtps.Select(o => o.IsUsed.ToString())));
            return false;
        }

        if (otpRecord.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Failed OTP verification: OTP EXPIRED for user {UserId}. Code was created at {CreatedAt}, expired at {ExpiresAt}, Current UTC is {UtcNow}", 
                userId, otpRecord.CreatedAt, otpRecord.ExpiresAt, DateTime.UtcNow);
            return false;
        }

        // Reached max attempts
        if (otpRecord.FailedAttempts >= 5)
        {
            otpRecord.IsUsed = true; // Hard invalidate the code
            await _dbContext.SaveChangesAsync();
            _logger.LogWarning("OTP max attempts reached for user {UserId} - Type: {OtpType}", userId, otpType);
            throw new InvalidOperationException("Maximum verification attempts exceeded. Please request a new code.");
        }

        // Incorrect code
        if (otpRecord.OtpCode != code)
        {
            otpRecord.FailedAttempts++;
            await _dbContext.SaveChangesAsync();
            _logger.LogWarning("Incorrect OTP code entered for user {UserId} - Attempt {Attempt}/5", userId, otpRecord.FailedAttempts);
            return false;
        }

        // Valid code
        otpRecord.IsUsed = true;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Successful {OtpType} OTP verification for user {UserId}", otpType, userId);
        return true;
    }
}
