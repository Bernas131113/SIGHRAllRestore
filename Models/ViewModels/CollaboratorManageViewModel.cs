// Em: Models/ViewModels/CollaboratorManageViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace SIGHR.Models.ViewModels
{
    public class CollaboratorManageViewModel
    {
        // Secção: Dados do Perfil
        [Required(ErrorMessage = "O nome de utilizador é obrigatório.")]
        [Display(Name = "Nome de Utilizador")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "Nome Completo")]
        public string? NomeCompleto { get; set; }

        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "O formato do email é inválido.")]
        public string Email { get; set; } = string.Empty;

        // Secção: Alteração de PIN
        [DataType(DataType.Password)]
        [Display(Name = "PIN Atual")]
        public string? OldPin { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Novo PIN")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "O novo PIN deve conter exatamente 4 números.")]
        public string? NewPin { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar Novo PIN")]
        [Compare("NewPin", ErrorMessage = "O novo PIN e a confirmação não correspondem.")]
        public string? ConfirmNewPin { get; set; }
    }
}