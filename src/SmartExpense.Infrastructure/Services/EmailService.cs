using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Models;

namespace SmartExpense.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("SendGrid API key is missing. Email skipped for {ToEmail}", toEmail);
            return;
        }

        var client = new SendGridClient(_options.ApiKey);

        var from = new EmailAddress(_options.FromEmail, _options.FromName);
        var to = new EmailAddress(toEmail);

        var msg = MailHelper.CreateSingleEmail(
            from,
            to,
            subject,
            plainTextContent: body,
            htmlContent: body
        );

        var response = await client.SendEmailAsync(msg);

        if ((int)response.StatusCode >= 400)
        {
            var responseBody = await response.Body.ReadAsStringAsync();

            _logger.LogError(
                "SendGrid failed. Status: {Status}, Body: {Body}",
                response.StatusCode,
                responseBody);

            throw new Exception($"SendGrid failed: {response.StatusCode}");
        }

        _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
    }
}