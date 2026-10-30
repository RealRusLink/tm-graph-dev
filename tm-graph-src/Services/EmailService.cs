using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using tm_graph_src.Options;

namespace tm_graph.Services;

public class EmailService(IOptions<EmailOptions> emailOptions)
{

    private EmailOptions _options = emailOptions.Value;
    
    private MailboxAddress _address = new MailboxAddress("App", emailOptions.Value.Address);

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
                await client.ConnectAsync(host: _options.SmtpServer, port: _options.Port, SecureSocketOptions.StartTls);
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

// using System.ComponentModel.DataAnnotations;
// using System.Net;
// using System.Net.Sockets;
// using MailKit;
// using MailKit.Net.Smtp;
// using MailKit.Security;
// using MimeKit;
// using Microsoft.Extensions.Options;
// using tm_graph_src.Options;
//
//
// namespace tm_graph_src.Services
// {
//     public class EmailService
//     {
//         private readonly EmailOptions _options;
//         private readonly MailboxAddress _address;
//
//         public EmailService(IOptions<EmailOptions> options)
//         {
//             _options = options.Value;
//             _address = new MailboxAddress(_options.Address ?? "App", _options.Address);
//         }
//
//         public async Task SendMessage(string emailTo, string messageText, string messageSubject)
//         {
//             var message = new MimeMessage();
//             message.From.Add(_address);
//             message.To.Add(new MailboxAddress(null, emailTo));
//             message.Subject = messageSubject;
//             message.Body = new BodyBuilder { TextBody = messageText }.ToMessageBody();
//
//             using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
//             using var client = new SmtpClient();
//             client.Timeout = 15000;
//
//             try
//             {
//                 var socket = await ConnectIpv4Async(_options.SmtpServer, _options.Port, cts.Token);
//                 await client.ConnectAsync(socket, _options.SmtpServer, _options.Port,
//                     SecureSocketOptions.Auto, cts.Token);
//                 await client.AuthenticateAsync(_options.Address,
//                     _options.Password.Replace(" ", ""), cts.Token);
//                 await client.SendAsync(message, cts.Token);
//             }
//             finally
//             {
//                 if (client.IsConnected)
//                     await client.DisconnectAsync(true);
//             }
//         }
//
//         private static async Task<Socket> ConnectIpv4Async(string host, int port, CancellationToken ct)
//         {
//             var addresses = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, ct);
//             Exception? last = null;
//
//             foreach (var address in addresses)
//             {
//                 var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//                 try
//                 {
//                     using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
//                     attempt.CancelAfter(TimeSpan.FromSeconds(5));
//                     await socket.ConnectAsync(new IPEndPoint(address, port), attempt.Token);
//                     return socket;
//                 }
//                 catch (Exception ex)
//                 {
//                     socket.Dispose();
//                     last = ex;
//                 }
//             }
//
//             throw new IOException($"Не удалось подключиться к {host}:{port} по IPv4", last);
//         }
//     }
// }