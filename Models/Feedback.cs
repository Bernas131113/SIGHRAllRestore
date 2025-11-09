// Models/Feedback.cs
using SIGHR.Areas.Identity.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIGHR.Models
{
    public class Feedback
    {
        public long Id { get; set; }

        [Required]
        public required string UtilizadorId { get; set; } // <-- CORRIGIDO

        [Required]
        [Display(Name = "Tipo de Submissão")]
        public required string TipoSubmissao { get; set; } // <-- CORRIGIDO

        [Required]
        [StringLength(150)]
        public required string Titulo { get; set; } // <-- CORRIGIDO

        [Required]
        public required string Descricao { get; set; } // <-- CORRIGIDO

        public DateTime DataRegisto { get; set; } = DateTime.UtcNow;

        [Display(Name = "Estado")]
        public string Estado { get; set; } = "Pendente"; // (Este não precisa, pois tem um valor por defeito)

        [ForeignKey("UtilizadorId")]
        public virtual SIGHRUser? User { get; set; } // (Este não precisa, pois é anulável '?')
    }
}