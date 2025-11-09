using SIGHR.Models;
using System.Collections.Generic; // <-- ADICIONADO
using System.Threading.Tasks;

namespace SIGHR.Services
{
    public interface IEmailService
    {
        // ================== ALTERADO DE 'string' PARA 'List<string>' ==================
        Task SendEncomendaNotificationAsync(Encomenda encomenda, List<string> recipientEmails);
        // ================== FIM DA ALTERAÇÃO ==================
    }
}