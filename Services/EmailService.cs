// Services/EmailService.cs
using SIGHR.Models;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic; // <-- ADICIONADO
using System.Linq; // <-- ADICIONADO

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
            _apiKey = config["EmailSender:ApiKey"] ?? throw new InvalidOperationException("EmailSender:ApiKey não está configurada.");
            _senderAddress = config["EmailSender:Address"] ?? throw new InvalidOperationException("EmailSender:Address não está configurada.");
            _senderName = "SIGHR Notificações";
            _logger = logger;
        }

        // ================== ALTERAÇÃO 1: Assinatura do método ==================
        public async Task SendEncomendaNotificationAsync(Encomenda encomenda, List<string> recipientEmails)
        {
            // 1. Cria o cliente do SendGrid
            var client = new SendGridClient(_apiKey);

            // 2. Define o remetente
            var from = new EmailAddress(_senderAddress, _senderName);

            // ================== ALTERAÇÃO 2: Criar a lista de destinatários ==================
            // Converte a lista de strings (e-mails) numa lista de objetos EmailAddress
            var tos = recipientEmails.Select(email => new EmailAddress(email)).ToList();

            // Verifica se a lista não está vazia
            if (!tos.Any())
            {
                _logger.LogWarning("Tentativa de envio de e-mail de encomenda sem destinatários.");
                return;
            }
            // ================== FIM DA ALTERAÇÃO 2 ==================

            // 4. Cria o corpo do e-mail (HTML)
            var subject = $"Nova Encomenda Registada (Obra: {encomenda.DescricaoObra ?? "N/D"})";
            var htmlContent = BuildEmailBody(encomenda);
            var plainTextContent = "Uma nova encomenda foi registada.";

            // ================== ALTERAÇÃO 3: Usar o método para múltiplos destinatários ==================
            // Isto envia UMA mensagem para múltiplos destinatários (normalmente em BCC)
            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(from, tos, subject, plainTextContent, htmlContent);
            // ================== FIM DA ALTERAÇÃO 3 ==================

            try
            {
                // 6. Envia o e-mail
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("E-mail de encomenda enviado com sucesso para {RecipientCount} destinatários.", tos.Count);
                }
                else
                {
                    _logger.LogWarning("Falha ao enviar e-mail pelo SendGrid. Status: {StatusCode}. Body: {Body}",
                        response.StatusCode,
                        await response.Body.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao enviar e-mail da encomenda.");
            }
        }

        // Esta função auxiliar continua igual
        private string BuildEmailBody(Encomenda encomenda)
        {
            var sb = new StringBuilder();
            sb.Append("<div style='font-family: Arial, sans-serif; line-height: 1.6;'>");
            sb.Append("<h2>Nova Encomenda Recebida</h2>");
            sb.Append($"<p>Uma nova encomenda foi registada no sistema pelo colaborador <strong>{encomenda.User?.NomeCompleto ?? "N/D"}</strong>.</p>");
            sb.Append($"<p><strong>Obra/Descrição:</strong> {encomenda.DescricaoObra ?? "Não especificada"}</p>");
            sb.Append($"<p><strong>Data da Encomenda:</strong> {encomenda.Data.ToLocalTime():dd/MM/yyyy}</p>");
            sb.Append("<h3>Itens da Encomenda:</h3>");
            sb.Append("<table border='1' cellpadding='10' style='border-collapse: collapse; width: 100%;'>");
            sb.Append("<thead><tr style='background-color: #f4f4f4;'><th>Material</th><th>Quantidade</th></tr></thead>");
            sb.Append("<tbody>");

            if (encomenda.Requisicoes != null)
            {
                foreach (var item in encomenda.Requisicoes)
                {
                    sb.Append($"<tr><td>{item.Material?.Descricao ?? "N/D"}</td><td>{item.Quantidade}</td></tr>");
                }
            }

            sb.Append("</tbody></table>");
            sb.Append("<p style='margin-top: 20px; font-size: 0.9em; color: #777;'>Este é um e-mail automático enviado pelo sistema SIGHR.</p>");
            sb.Append("</div>");
            return sb.ToString();
        }
    }
}