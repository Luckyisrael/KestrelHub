using System.Security.Cryptography;
using System.Text;
using KestrelHub.Controller.Data;
using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Services;

public interface ISecretVaultService
{
    Task<Secret> SetSecretAsync(Guid? deploymentId, string key, string value, string environment);
    Task<string?> GetSecretAsync(Guid secretId);
    Task<List<Secret>> GetAllSecretsAsync(Guid? deploymentId = null);
    Task<bool> DeleteSecretAsync(Guid secretId);
    Task<List<SecretAuditLog>> GetAuditLogsAsync(Guid secretId);
}

public class SecretVaultService : ISecretVaultService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly byte[] _masterKey;

    public SecretVaultService(ApplicationDbContext context, ICurrentUserService currentUser, IConfiguration configuration)
    {
        _context = context;
        _currentUser = currentUser;

        var keyEnv = configuration["KestrelHub:MasterKey"]
            ?? Environment.GetEnvironmentVariable("KESTRELHUB_MASTER_KEY");

        if (string.IsNullOrWhiteSpace(keyEnv) || keyEnv.Length < 32)
            throw new InvalidOperationException("KESTRELHUB_MASTER_KEY is required and must be at least 32 characters.");

        _masterKey = Encoding.UTF8.GetBytes(keyEnv[..32]);
    }

    public async Task<Secret> SetSecretAsync(Guid? deploymentId, string key, string value, string environment)
    {
        var (ciphertext, nonce, tag) = Encrypt(value);

        var secret = new Secret
        {
            Id = Guid.NewGuid(),
            DeploymentId = deploymentId,
            Key = key,
            EncryptedValue = Convert.ToBase64String(nonce.Concat(tag).Concat(ciphertext).ToArray()),
            Environment = environment,
            CreatedAt = DateTime.UtcNow
        };

        _context.Secrets.Add(secret);

        _context.SecretAuditLogs.Add(new SecretAuditLog
        {
            Id = Guid.NewGuid(),
            SecretId = secret.Id,
            Action = SecretAction.Created,
            ActorUserId = _currentUser.UserId ?? "system",
            ActorEmail = _currentUser.Email ?? "system",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return secret;
    }

    public async Task<string?> GetSecretAsync(Guid secretId)
    {
        var secret = await _context.Secrets.FindAsync(secretId);
        if (secret is null || secret.IsDeleted)
            return null;

        var value = Decrypt(secret.EncryptedValue);

        secret.LastAccessedAt = DateTime.UtcNow;

        _context.SecretAuditLogs.Add(new SecretAuditLog
        {
            Id = Guid.NewGuid(),
            SecretId = secret.Id,
            Action = SecretAction.Read,
            ActorUserId = _currentUser.UserId ?? "system",
            ActorEmail = _currentUser.Email ?? "system",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return value;
    }

    public async Task<List<Secret>> GetAllSecretsAsync(Guid? deploymentId = null)
    {
        var query = _context.Secrets.Where(s => !s.IsDeleted);

        if (deploymentId.HasValue)
            query = query.Where(s => s.DeploymentId == deploymentId.Value);

        var secrets = await query.ToListAsync();

        // Clear encrypted values — never return plaintext via this method
        foreach (var s in secrets)
            s.EncryptedValue = "••••••••";

        return secrets;
    }

    public async Task<bool> DeleteSecretAsync(Guid secretId)
    {
        var secret = await _context.Secrets.FindAsync(secretId);
        if (secret is null || secret.IsDeleted)
            return false;

        secret.IsDeleted = true;

        _context.SecretAuditLogs.Add(new SecretAuditLog
        {
            Id = Guid.NewGuid(),
            SecretId = secret.Id,
            Action = SecretAction.Deleted,
            ActorUserId = _currentUser.UserId ?? "system",
            ActorEmail = _currentUser.Email ?? "system",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<SecretAuditLog>> GetAuditLogsAsync(Guid secretId)
    {
        return await _context.SecretAuditLogs
            .Where(l => l.SecretId == secretId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    private (byte[] ciphertext, byte[] nonce, byte[] tag) Encrypt(string plaintext)
    {
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_masterKey, 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        return (ciphertext, nonce, tag);
    }

    private string Decrypt(string base64Encrypted)
    {
        var combined = Convert.FromBase64String(base64Encrypted);
        var nonce = combined[..12];
        var tag = combined[12..28];
        var ciphertext = combined[28..];

        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_masterKey, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
