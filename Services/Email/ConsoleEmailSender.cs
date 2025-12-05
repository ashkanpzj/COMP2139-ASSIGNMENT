using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace Assignment1.Services.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.SenderAddress))
            throw new InvalidOperationException("Email sender address is not configured.");

        using var message = new MailMessage
        {
            From = new MailAddress(_options.SenderAddress, _options.SenderName ?? _options.SenderAddress),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };

        message.To.Add(email);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            Credentials = new NetworkCredential(
                string.IsNullOrWhiteSpace(_options.Username) ? _options.SenderAddress : _options.Username,
                _options.Password)
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent to {Email}", email);
    }
}

