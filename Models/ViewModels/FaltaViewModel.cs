// Models/ViewModels/FaltaViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SIGHR.Models.ViewModels
{
    /// <summary>
    /// ViewModel que representa os dados necessários para registar uma nova falta.
    /// As horas são agora DateTime.
    /// </summary>
    public class FaltaViewModel
    {
        [Required(ErrorMessage = "A data da falta é obrigatória.")]
        [DataType(DataType.Date)]
        [Display(Name = "Data da Falta")]
        public DateTime DataFalta { get; set; } = DateTime.Today;

        /// <summary>
        /// A hora de início da ausência.
        /// </summary>
        [Required(ErrorMessage = "A hora de início é obrigatória.")]
        [DataType(DataType.Time)] // Usamos DataType.Time para hints da UI
        [Display(Name = "Hora de Início")]
        public DateTime Inicio { get; set; } = DateTime.Today; // ALTERADO DE TimeSpan PARA DateTime

        /// <summary>
        /// A hora de fim da ausência.
        /// </summary>
        [Required(ErrorMessage = "A hora de fim é obrigatória.")]
        [DataType(DataType.Time)] // Usamos DataType.Time para hints da UI
        [Display(Name = "Hora de Fim")]
        public DateTime Fim { get; set; } = DateTime.Today.AddHours(1); // ALTERADO DE TimeSpan PARA DateTime

        [Required(ErrorMessage = "O motivo é obrigatório.")]
        [StringLength(500, ErrorMessage = "O motivo não pode exceder os 500 caracteres.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Motivo")]
        public string Motivo { get; set; } = string.Empty;
    }
}