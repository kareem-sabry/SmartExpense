using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Models;

namespace SmartExpense.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> emailOptions, ILogger<EmailService> logger)
    {
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        // Guard: if SMTP is not configured (e.g. Railway without EmailOptions vars set),
        // log a clear warning and return instead of throwing an obscure ArgumentException.
        if (string.IsNullOrWhiteSpace(_emailOptions.SmtpHost) ||
            string.IsNullOrWhiteSpace(_emailOptions.FromEmail))
        {
            _logger.LogWarning(
                "Email to {ToEmail} was skipped — SMTP is not configured. " +
                "Set EmailOptions__SmtpHost, EmailOptions__FromEmail, EmailOptions__SmtpUsername, " +
                "EmailOptions__SmtpPassword in Railway variables to enable email delivery.",
                toEmail);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromEmail, _emailOptions.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var smtp = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.SmtpPort)
        {
            Credentials = new NetworkCredential(_emailOptions.SmtpUsername, _emailOptions.SmtpPassword),
            EnableSsl = true
        };

        await smtp.SendMailAsync(message);
        _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
    }
}