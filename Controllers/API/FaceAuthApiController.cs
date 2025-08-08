using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Areas.Identity.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication; // <<< ADICIONA ESTE USING NO TOPO

namespace SIGHR.Controllers.Api
{
    public class FaceRegistrationRequest { public string? FacialProfileBase64 { get; set; } }
    public class FaceVerificationRequest { public string? LiveFaceDescriptorBase64 { get; set; } }

    [Route("api/face-auth")]
    [ApiController]
    public class FaceAuthApiController : ControllerBase
    {
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly SignInManager<SIGHRUser> _signInManager;

        public FaceAuthApiController(UserManager<SIGHRUser> userManager, SignInManager<SIGHRUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // --- Nenhumas alterações nos métodos de Register e Delete ---
        [HttpPost("register")]
        [Authorize(Policy = "CollaboratorAccessUI")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterFacialProfile([FromBody] FaceRegistrationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FacialProfileBase64)) return BadRequest(new { message = "Dados faciais inválidos ou em falta." });
            byte[] facialProfileBytes;
            try { facialProfileBytes = Convert.FromBase64String(request.FacialProfileBase64); }
            catch (FormatException) { return BadRequest(new { message = "O formato dos dados faciais (Base64) é inválido." }); }
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            user.FacialProfile = facialProfileBytes;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded) return Ok(new { message = "Perfil facial registado com sucesso!" });
            return BadRequest(new { message = "Erro ao guardar o perfil facial no servidor." });
        }

        [HttpPost("delete")]
        [Authorize(Policy = "CollaboratorAccessUI")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFacialProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            user.FacialProfile = null;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded) return Ok(new { message = "Perfil facial apagado com sucesso!" });
            return BadRequest(new { message = "Erro ao apagar o perfil facial." });
        }
        [HttpPost("verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyAndLogin([FromBody] FaceVerificationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LiveFaceDescriptorBase64))
                return BadRequest(new { message = "Dados do rosto não recebidos." });

            float[] liveDescriptor;
            try
            {
                var descriptorBytes = Convert.FromBase64String(request.LiveFaceDescriptorBase64);
                if (descriptorBytes.Length != 512) return BadRequest(new { message = "Dados do rosto inválidos." });
                liveDescriptor = ToFloatArray(descriptorBytes);
            }
            catch (Exception) { return BadRequest(new { message = "Dados do rosto corrompidos." }); }

            const double faceDistanceThreshold = 0.5;

            // ============== CORREÇÃO PRINCIPAL ===================
            // A consulta agora procura qualquer utilizador (Admin ou Collaborator)
            // que tenha um perfil facial registado.
            var usersWithProfiles = await _userManager.Users
                .Where(u => u.FacialProfile != null && u.FacialProfile.Length == 512)
                .ToListAsync();

            Console.WriteLine($"[DEBUG] Perfis faciais válidos na BD: {usersWithProfiles.Count}");

            foreach (var user in usersWithProfiles)
            {
                var storedDescriptor = ToFloatArray(user.FacialProfile!);
                var distance = CalculateEuclideanDistance(liveDescriptor, storedDescriptor);

                Console.WriteLine($"[DEBUG] Verificando Rosto... Utilizador: {user.UserName} (Tipo: {user.Tipo}), Distância: {distance:F4}");

                if (distance < faceDistanceThreshold)
                {
                    // ===== LÓGICA DE LOGIN AJUSTADA =====
                    // FORÇAMOS O LOGIN USANDO O ESQUEMA DE COOKIE DE COLABORADOR,
                    // independentemente do tipo real do utilizador (Admin ou Collaborator).
                    // Isto garante que, após este login, o utilizador será tratado
                    // como um colaborador para fins de navegação.
                    const string authenticationScheme = "CollaboratorLoginScheme";

                    var principal = await _signInManager.CreateUserPrincipalAsync(user);
                    await HttpContext.SignInAsync(authenticationScheme, principal, new AuthenticationProperties { IsPersistent = true });

                    // A URL de redirecionamento é SEMPRE para o dashboard do colaborador.
                    string redirectUrl = Url.Action("Dashboard", "Collaborator") ?? "/";

                    return Ok(new { success = true, redirectUrl });
                }
            }

            return Unauthorized(new { success = false, message = "Rosto não reconhecido." });
        }

        private float[] ToFloatArray(byte[] byteArray)
        {
            var floatArray = new float[byteArray.Length / 4];
            Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
            return floatArray;
        }

        private double CalculateEuclideanDistance(float[] v1, float[] v2)
        {
            double sum = 0;
            for (int i = 0; i < v1.Length; i++)
            {
                sum += Math.Pow(v1[i] - v2[i], 2);
            }
            return Math.Sqrt(sum);
        }
    }
}