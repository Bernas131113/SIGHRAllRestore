namespace SIGHR.Models
{
    /// <summary>
    /// ViewModel que representa os dados necess�rios para a p�gina de erro da aplica��o.
    /// � utilizado para passar o identificador do pedido (Request ID) para a View de erro.
    /// </summary>
    public class ErrorViewModel
    {
        /// <summary>
        /// O identificador �nico do pedido HTTP que causou o erro.
        /// � �til para fins de depura��o (debugging) e para correlacionar com os logs do servidor.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Uma propriedade de conveni�ncia que retorna 'true' se o RequestId existir.
        /// � usada na View para decidir se deve ou n�o exibir o ID do pedido ao utilizador.
        /// </summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}