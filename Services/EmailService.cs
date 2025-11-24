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
            // Lê a chave SG....
            _apiKey = config["EmailSender:ApiKey"] ?? throw new InvalidOperationException("EmailSender:ApiKey não está configurada.");
            _senderAddress = config["EmailSender:Address"] ?? throw new InvalidOperationException("EmailSender:Address não está configurada.");
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

            // Converter lista de strings para lista de EmailAddress
            var tos = recipientEmails.Select(e => new EmailAddress(e)).ToList();

            // ESTE MÉTODO É O SEGREDO:
            // Envia um e-mail individual para cada pessoa da lista.
            // Ninguém vê o e-mail dos outros.
            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(from, tos, subject, plainTextContent, htmlContent);

            try
            {
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("E-mail enviado via SendGrid com sucesso.");
                }
                else
                {
                    // Isto vai aparecer nos logs do Render se falhar
                    var body = await response.Body.ReadAsStringAsync();
                    _logger.LogError($"SendGrid falhou. Status: {response.StatusCode}, Erro: {body}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção crítica no SendGrid.");
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