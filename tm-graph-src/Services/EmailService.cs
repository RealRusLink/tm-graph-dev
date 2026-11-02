using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using tm_graph_src.Options;

namespace tm_graph.Services;

public interface IEmailService
{
    Task SendMessage([EmailAddress] string emailTo, string messageText, string messageSubject);
}

public class EmailService(IOptions<EmailOptions> emailOptions) : IEmailService
{

    private readonly EmailOptions _options = emailOptions.Value;

    private readonly MailboxAddress _address = new(emailOptions.Value.FromName, emailOptions.Value.Address);

    public async Task SendMessage([EmailAddress] string emailTo, string messageText, string messageSubject)
    {
        var message = new MimeMessage();
        message.From.Add(_address);
        message.To.Add((new MailboxAddress(null, emailTo)));
        message.Subject = messageSubject;
        message.Body = (new BodyBuilder() { TextBody = messageText }).ToMessageBody();
        using (var client = new SmtpClient())
        {
            try
            {
                await client.ConnectAsync(host: _options.SmtpServer, port: _options.Port, _options.SecureSocketOption);
                await client.AuthenticateAsync(_options.Address, _options.Password);
                await client.SendAsync(message);
            }
            finally
            {
                if (client.IsConnected) await client.DisconnectAsync(true);
            }
        }
    }   
    
    


}
