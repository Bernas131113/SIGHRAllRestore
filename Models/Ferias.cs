// Em: Models/Ferias.cs
using SIGHR.Areas.Identity.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIGHR.Models
{
    public class Ferias
    {
        public long Id { get; set; }

        [Required]
        public required string UtilizadorId { get; set; }

        [Required]
        public DateTime DataInicio { get; set; }

        [Required]
        public DateTime DataFim { get; set; }

        [Required]
        public int DiasGastos { get; set; }

        // Tipo para diferenciar se foi marcada pelo user ou pelo admin para todos
        public string? Tipo { get; set; }

        public DateTime DataRegisto { get; set; } = DateTime.UtcNow;

        [ForeignKey("UtilizadorId")]
        public virtual SIGHRUser? User { get; set; }
    }
}