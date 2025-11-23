using SIGHR.Models;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace SIGHR.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _senderAddress;
        private readonly string _appPassword;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _senderAddress = config["EmailSender:Address"] ?? throw new InvalidOperationException("EmailSender:Address não configurado.");
            _appPassword = config["EmailSender:AppPassword"] ?? throw new InvalidOperationException("EmailSender:AppPassword não configurado.");
            _logger = logger;
        }

        public async Task SendEncomendaNotificationAsync(Encomenda encomenda, List<string> recipientEmails)
        {
            if (recipientEmails == null || !recipientEmails.Any()) return;

            try
            {
                // Configuração do Servidor do Gmail
                using (var client = new SmtpClient("smtp.gmail.com", 587))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_senderAddress, _appPassword);

                    // Criar a mensagem
                    var mailMessage = new MailMessage();
                    mailMessage.From = new MailAddress(_senderAddress, "SIGHR Notificações");
                    mailMessage.Subject = $"Nova Encomenda: {encomenda.DescricaoObra ?? "Sem Obra"}";
                    mailMessage.Body = BuildEmailBody(encomenda);
                    mailMessage.IsBodyHtml = true;

                    // Adicionar destinatários
                    foreach (var email in recipientEmails)
                    {
                        mailMessage.To.Add(email);
                    }

                    // Enviar
                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"E-mail enviado via Gmail SMTP para {recipientEmails.Count} destinatários.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar e-mail via Gmail SMTP. Verifica a App Password.");
            }
        }

        private string BuildEmailBody(Encomenda encomenda)
        {
            var sb = new StringBuilder();
            sb.Append("<div style='font-family: Arial, sans-serif; color: #333;'>");
            sb.Append($"<h2>Nova Encomenda de {encomenda.User?.NomeCompleto ?? "Colaborador"}</h2>");
            sb.Append($"<p><strong>Obra:</strong> {encomenda.DescricaoObra ?? "N/D"}</p>");
            sb.Append($"<p><strong>Data:</strong> {encomenda.Data.ToLocalTime():dd/MM/yyyy}</p>");
            sb.Append("<hr><table style='width:100%; border-collapse: collapse; text-align: left;'>");
            sb.Append("<tr style='background:#f4f4f4;'><th>Material</th><th>Qtd</th></tr>");

            if (encomenda.Requisicoes != null)
            {
                foreach (var item in encomenda.Requisicoes)
                {
                    sb.Append($"<tr><td style='border-bottom:1px solid #ddd; padding:8px;'>{item.Material?.Descricao}</td>");
                    sb.Append($"<td style='border-bottom:1px solid #ddd; padding:8px;'>{item.Quantidade}</td></tr>");
                }
            }
            sb.Append("</table></div>");
            return sb.ToString();
        }
    }
}