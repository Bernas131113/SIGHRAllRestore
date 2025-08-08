// Em: Areas/Identity/Data/SIGHRUser.cs
using Microsoft.AspNetCore.Identity;
using SIGHR.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema; // Adicionar este using

namespace SIGHR.Areas.Identity.Data
{
    public class SIGHRUser : IdentityUser
    {
        public string? PinnedHash { get; set; }
        public string? Tipo { get; set; }
        public string? NomeCompleto { get; set; }

        /// <summary>
        /// Guarda o descritor facial do utilizador como um array de bytes.
        /// Gerado pela face-api.js no cliente e enviado para o servidor.
        /// </summary>
        [Column(TypeName = "bytea")] // Tipo específico para PostgreSQL, otimizado para byte array
        public byte[]? FacialProfile { get; set; }


        // Propriedades de Navegação
        public virtual ICollection<Horario> Horarios { get; set; } = new List<Horario>();
        public virtual ICollection<Falta> Faltas { get; set; } = new List<Falta>();
        public virtual ICollection<Encomenda> Encomendas { get; set; } = new List<Encomenda>();
    }
}
