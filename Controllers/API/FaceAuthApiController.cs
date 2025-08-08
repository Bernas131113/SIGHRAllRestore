using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Areas.Identity.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

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

            // OTIMIZAÇÃO: Selecionar apenas os dados necessários da base de dados.
            var usersWithProfiles = await _userManager.Users
                .Where(u => u.FacialProfile != null && u.FacialProfile.Length == 512)
                .Select(u => new { u.Id, u.UserName, u.Tipo, u.FacialProfile }) // Projeta apenas os dados essenciais
                .ToListAsync();

            Console.WriteLine($"[DEBUG] Perfis faciais válidos na BD: {usersWithProfiles.Count}");

            foreach (var userProjection in usersWithProfiles)
            {
                var storedDescriptor = ToFloatArray(userProjection.FacialProfile!);
                var distance = CalculateEuclideanDistance(liveDescriptor, storedDescriptor);

                Console.WriteLine($"[DEBUG] Verificando... Utilizador: {userProjection.UserName}, Distância: {distance:F4}");

                if (distance < faceDistanceThreshold)
                {
                    // Correspondência encontrada! Agora, busca o objeto completo do utilizador para fazer o login.
                    var userToLogin = await _userManager.FindByIdAsync(userProjection.Id);
                    if (userToLogin == null) continue; // Segurança extra caso o user seja apagado durante o processo

                    const string authenticationScheme = "CollaboratorLoginScheme";

                    var principal = await _signInManager.CreateUserPrincipalAsync(userToLogin);
                    await HttpContext.SignInAsync(authenticationScheme, principal, new AuthenticationProperties { IsPersistent = true });

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