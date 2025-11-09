// Models/ViewModels/FeedbackViewModel.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace SIGHR.Models.ViewModels
{
    public class FeedbackViewModel
    {
        [Required(ErrorMessage = "Tem de selecionar um tipo.")]
        [Display(Name = "Tipo de Submissão")]
        public string TipoSubmissao { get; set; } = string.Empty; // <-- CORRIGIDO

        [Required(ErrorMessage = "O título é obrigatório.")]
        [StringLength(150, ErrorMessage = "O título não pode ter mais de 150 caracteres.")]
        public string Titulo { get; set; } = string.Empty; // <-- CORRIGIDO

        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [DataType(DataType.MultilineText)]
        public string Descricao { get; set; } = string.Empty; // <-- CORRIGIDO

        // Lista para preencher o <select> no formulário
        public List<SelectListItem> TiposDisponiveis { get; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "Bug", Text = "Reportar um Erro (Bug)" },
            new SelectListItem { Value = "Sugestão", Text = "Sugerir uma Melhoria" },
            new SelectListItem { Value = "Outro", Text = "Outro Assunto" }
        };
    }
}