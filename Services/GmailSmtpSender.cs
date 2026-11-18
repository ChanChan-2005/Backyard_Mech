using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backyard_Mech.Services;

public sealed class GmailSmtpSender
{
    public const string SenderAddress = "bakyardmechofficial@gmail.com";
    private readonly IConfiguration _configuration;

    public GmailSmtpSender(IConfiguration configuration) => _configuration = configuration;

    public sealed record Status(string SenderAddress, bool IsConfigured);

    private string? AppPassword => _configuration["GmailSmtp:AppPassword"]?.Replace(" ", "").Trim();

    public Status GetStatus() => new(SenderAddress, !string.IsNullOrWhiteSpace(AppPassword));

    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        var appPassword = AppPassword;
        if (string.IsNullOrWhiteSpace(appPassword))
            throw new InvalidOperationException("Configure the Gmail app password.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Backyard Mech", SenderAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient { Timeout = 30000 };
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(SenderAddress, appPassword, cancellationToken);
        await client.SendAsync(new MailKit.SendRequest(message), cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
