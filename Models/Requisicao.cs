using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIGHR.Models
{
    public class Requisicao
    {
        public long MaterialId { get; set; }
        public long EncomendaId { get; set; }

        // ================== ALTERAÇÃO: de 'long' para 'double' ==================
        [Required(ErrorMessage = "A quantidade é obrigatória.")]
        public double Quantidade { get; set; }
        // ========================================================================

        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }

        [ForeignKey("EncomendaId")]
        public virtual Encomenda? Encomenda { get; set; }
    }
}