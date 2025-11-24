using SIGHR.Models;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace SIGHR.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _apiKey;
        private readonly string _senderAddress;
        private readonly string _senderName;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            // Lê a chave do SendGrid
            _apiKey = config["EmailSender:ApiKey"] ?? throw new InvalidOperationException("EmailSender:ApiKey não configurada.");
            _senderAddress = config["EmailSender:Address"] ?? throw new InvalidOperationException("EmailSender:Address não configurada.");
            _senderName = "SIGHR Notificações";
            _logger = logger;
        }

        public async Task SendEncomendaNotificationAsync(Encomenda encomenda, List<string> recipientEmails)
        {
            if (recipientEmails == null || !recipientEmails.Any()) return;

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_senderAddress, _senderName);
            var subject = $"Nova Encomenda: {encomenda.DescricaoObra ?? "Sem Obra"}";
            var htmlContent = BuildEmailBody(encomenda);
            var plainTextContent = "Uma nova encomenda foi registada.";

            // Converte as strings para objetos EmailAddress
            var tos = recipientEmails.Select(e => new EmailAddress(e)).ToList();

            // Envia para todos de uma vez (individualmente)
            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(from, tos, subject, plainTextContent, htmlContent);

            try
            {
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SendGrid: E-mail enviado com sucesso.");
                }
                else
                {
                    var body = await response.Body.ReadAsStringAsync();
                    _logger.LogError($"SendGrid Falhou: {response.StatusCode} - {body}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico no envio SendGrid.");
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