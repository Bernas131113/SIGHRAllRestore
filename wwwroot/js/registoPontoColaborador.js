// wwwroot/js/registoPontoColaborador.js

document.addEventListener('DOMContentLoaded', function () {
    //
    // Bloco de Inicialização e Seleção de Elementos
    //
    const loadingMsg = document.getElementById('loading-message-ponto');
    const successMsg = document.getElementById('success-message-ponto');
    const errorMsg = document.getElementById('error-message-ponto');

    /**
     * Obtém o token anti-falsificação (anti-forgery) do input oculto na página.
     * @returns {string} O valor do token ou uma string vazia se não for encontrado.
     */
    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    //
    // Bloco de Funções de Feedback Visual (Notificações)
    //

    /**
     * Mostra o indicador de "A processar..." e esconde outras mensagens.
     */
    function showLoading() {
        if (loadingMsg) loadingMsg.style.display = 'block';
        if (successMsg) successMsg.style.display = 'none';
        if (errorMsg) errorMsg.style.display = 'none';
    }

    /**
     * Mostra uma notificação (sucesso ou erro) com uma animação de fade.
     * @param {HTMLElement} element - O elemento da notificação (sucesso ou erro).
     * @param {string} message - A mensagem a ser exibida.
     * @param {number} duration - A duração em milissegundos que a mensagem ficará visível.
     */
    function showNotification(element, message, duration) {
        if (element) {
            element.textContent = message;
            element.style.display = 'block';

            window.requestAnimationFrame(() => {
                element.classList.add('show');
            });

            setTimeout(() => {
                element.classList.remove('show');
                setTimeout(() => {
                    if (!element.classList.contains('show')) {
                        element.style.display = 'none';
                    }
                }, 500);
            }, duration);
        }
    }

    function showSuccess(message) {
        if (loadingMsg) loadingMsg.style.display = 'none';
        showNotification(successMsg, message, 5000); // Mostra por 5 segundos.
    }

    function showError(message) {
        if (loadingMsg) loadingMsg.style.display = 'none';
        showNotification(errorMsg, message, 7000); // Mostra por 7 segundos.
    }


    //
    // Bloco de Comunicação com a API e Atualização da UI
    //

    /**
     * Função principal que envia o pedido para registar um ponto (Entrada, Saída, etc.).
     * @param {string} actionUrl - A URL da API para a ação específica.
     * @param {string} buttonId - O ID do botão que foi clicado.
     */
    async function registarPonto(actionUrl, buttonId) {
        const button = document.getElementById(buttonId);
        if (button) button.disabled = true;
        showLoading();

        const antiForgeryToken = getAntiForgeryToken();
        if (!antiForgeryToken) {
            showError("Erro de segurança. Por favor, atualize a página.");
            if (button) button.disabled = false;
            return;
        }

        try {
            const response = await fetch(actionUrl, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': antiForgeryToken,
                    'Accept': 'application/json'
                }
            });

            const data = await response.json();

            if (response.ok && data.success) {
                showSuccess(data.message || "Operação realizada com sucesso!");
                fetchPontoDoDia(); // Atualiza a informação do ponto na página.
            } else {
                showError(data.message || `Erro: Não foi possível concluir a operação.`);
            }

        } catch (error) {
            console.error('Erro na requisição de ponto:', error);
            showError('Erro de rede ou ao processar o pedido. Tente novamente.');
        } finally {
            if (button) button.disabled = false;
        }
    }

    /**
     * Função auxiliar para formatar a hora de um objeto Date para HH:MM no fuso horário local e em formato de 24 horas.
     * @param {string} dateString - A data/hora em formato string ISO 8601 (ex: "2024-01-01T10:00:00Z").
     */
    function formatTimeForDisplay(dateString) {
        // Verifica se a string é uma data "zero" (MinValue do .NET) ou inválida.
        if (!dateString || dateString.startsWith('0001-01-01')) {
            return '--:--';
        }
        try {
            const date = new Date(dateString);

            // toLocaleTimeString formata para o fuso horário local do utilizador.
            // hour12: false força o formato de 24 horas.
            return date.toLocaleTimeString('pt-PT', {
                hour: '2-digit',
                minute: '2-digit',
                hour12: false
            });
        } catch (e) {
            console.error("Erro ao formatar hora:", dateString, e);
            return 'Inválido';
        }
    }

    /**
     * Pede os dados de ponto do dia atual à API e atualiza os campos na página.
     */
    async function fetchPontoDoDia() {
        const displayEntrada = document.getElementById('displayEntrada');
        const displaySaidaAlmoco = document.getElementById('displaySaidaAlmoco');
        const displayEntradaAlmoco = document.getElementById('displayEntradaAlmoco');
        const displaySaida = document.getElementById('displaySaida');
        const noRecordMsg = document.getElementById('no-record-today-message');

        if (!displayEntrada) {
            console.error("Elementos de exibição de ponto não encontrados no DOM.");
            return;
        }

        if (typeof urls === 'undefined' || !urls.getPontoDoDia) {
            console.error("URL 'getPontoDoDia' não está definida.");
            return;
        }

        try {
            const response = await fetch(urls.getPontoDoDia);
            if (response.ok) {
                const data = await response.json();

                // Atualiza os spans com os valores formatados.
                displayEntrada.textContent = formatTimeForDisplay(data.horaEntrada);
                displaySaidaAlmoco.textContent = formatTimeForDisplay(data.saidaAlmoco);
                displayEntradaAlmoco.textContent = formatTimeForDisplay(data.entradaAlmoco);
                displaySaida.textContent = formatTimeForDisplay(data.horaSaida);

                // Esconde a mensagem "Nenhum registo" se a entrada foi registada.
                if (data.horaEntrada && !data.horaEntrada.startsWith('0001-01-01')) {
                    if (noRecordMsg) noRecordMsg.style.display = 'none';
                } else {
                    if (noRecordMsg) noRecordMsg.style.display = 'block';
                }

            } else {
                console.error("Erro ao obter o ponto do dia:", response.statusText);
            }
        } catch (error) {
            console.error('Erro de rede ao obter o ponto do dia:', error);
        }
    }

    //
    // Bloco de Configuração de Eventos
    // Associa as funções aos cliques dos botões.
    //
    if (typeof urls !== 'undefined') {
        document.getElementById('btnEntrada')?.addEventListener('click', () => registarPonto(urls.registarEntrada, 'btnEntrada'));
        document.getElementById('btnSaidaAlmoco')?.addEventListener('click', () => registarPonto(urls.registarSaidaAlmoco, 'btnSaidaAlmoco'));
        document.getElementById('btnEntradaAlmoco')?.addEventListener('click', () => registarPonto(urls.registarEntradaAlmoco, 'btnEntradaAlmoco'));
        document.getElementById('btnSaida')?.addEventListener('click', () => registarPonto(urls.registarSaida, 'btnSaida'));
    } else {
        console.error("Objeto 'urls' não definido. Os eventos de clique não foram configurados.");
    }
});