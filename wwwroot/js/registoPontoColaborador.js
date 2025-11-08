// wwwroot/js/registoPontoColaborador.js

/**
 * Obtém a posição geográfica atual do utilizador.
 * Retorna uma Promise que resolve com { latitude, longitude } ou { latitude: 0, longitude: 0 } em caso de erro.
 */
function getPosicaoAtual() {
    return new Promise((resolve) => {
        if ("geolocation" in navigator) {
            navigator.geolocation.getCurrentPosition(
                (posicao) => {
                    // Sucesso!
                    resolve({
                        latitude: posicao.coords.latitude,
                        longitude: posicao.coords.longitude
                    });
                },
                (erro) => {
                    // Erro (ex: permissão recusada)
                    console.warn(`Erro ao obter localização (Código: ${erro.code}): ${erro.message}`);
                    // Resolvemos com 0,0 para não bloquear o registo de ponto
                    resolve({ latitude: 0, longitude: 0 });
                },
                {
                    // Opções
                    enableHighAccuracy: true, // Pede mais precisão
                    timeout: 10000,           // Limite de 10 segundos
                    maximumAge: 0             // Não usar cache
                }
            );
        } else {
            // Browser não suporta geolocalização
            console.warn("Geolocalização não é suportada por este browser.");
            resolve({ latitude: 0, longitude: 0 });
        }
    });
}


document.addEventListener('DOMContentLoaded', function () {
    //
    // Bloco de Inicialização e Seleção de Elementos
    //
    const loadingMsg = document.getElementById('loading-message-ponto');
    const successMsg = document.getElementById('success-message-ponto');
    const errorMsg = document.getElementById('error-message-ponto');

    /**
     * Obtém o token anti-falsificação (anti-forgery) do input oculto na página.
     */
    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    //
    // Bloco de Funções de Feedback Visual (Notificações)
    //

    // ================== FUNÇÃO MODIFICADA ==================
    function showLoading(message = "A processar...") { // Adiciona mensagem opcional
        if (loadingMsg) {
            loadingMsg.textContent = message; // Define o texto
            loadingMsg.style.display = 'block';
        }
        if (successMsg) successMsg.style.display = 'none';
        if (errorMsg) errorMsg.style.display = 'none';
    }
    // ================== FIM DA MODIFICAÇÃO ==================


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
        showNotification(successMsg, message, 5000);
    }

    function showError(message) {
        if (loadingMsg) loadingMsg.style.display = 'none';
        showNotification(errorMsg, message, 7000);
    }

    //
    // Bloco de Comunicação com a API e Atualização da UI
    //

    /**
     * Função principal que envia o pedido para registar um ponto (Entrada, Saída, etc.).
     */
    // ================== FUNÇÃO MODIFICADA ==================
    async function registarPonto(actionUrl, buttonId) {
        const button = document.getElementById(buttonId);
        if (button) button.disabled = true;

        // 1. Mensagem de loading atualizada
        showLoading("A obter localização e a registar...");

        const antiForgeryToken = getAntiForgeryToken();
        if (!antiForgeryToken) {
            showError("Erro de segurança. Por favor, atualize a página.");
            if (button) button.disabled = false;
            return;
        }

        // 2. Obter a localização ANTES de fazer o fetch
        const localizacao = await getPosicaoAtual();
        console.log("Localização obtida:", localizacao);

        // 3. Preparar o body para enviar à API
        const requestBody = JSON.stringify({
            latitude: localizacao.latitude,
            longitude: localizacao.longitude
        });

        try {
            const response = await fetch(actionUrl, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': antiForgeryToken,
                    'Accept': 'application/json',
                    // 4. Adicionar o Content-Type para o JSON
                    'Content-Type': 'application/json'
                },
                // 5. Adicionar o body com as coordenadas
                body: requestBody
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
            if (loadingMsg) loadingMsg.style.display = 'none'; // <- Garante que o loading desaparece
            if (button) button.disabled = false;
        }
    }
    // ================== FIM DA MODIFICAÇÃO ==================


    /**
     * Função auxiliar para formatar a hora de um objeto Date para HH:MM no fuso horário local e em formato de 24 horas.
     */
    function formatTimeForDisplay(dateString) {
        if (!dateString || dateString.startsWith('0001-01-01')) {
            return '--:--';
        }
        try {
            const date = new Date(dateString);
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

        if (!displayEntrada) return;

        if (typeof urls === 'undefined' || !urls.getPontoDoDia) {
            console.error("URL 'getPontoDoDia' não está definida.");
            return;
        }

        try {
            const response = await fetch(urls.getPontoDoDia);
            if (response.ok) {
                const data = await response.json();
                displayEntrada.textContent = formatTimeForDisplay(data.horaEntrada);
                displaySaidaAlmoco.textContent = formatTimeForDisplay(data.saidaAlmoco);
                displayEntradaAlmoco.textContent = formatTimeForDisplay(data.entradaAlmoco);
                displaySaida.textContent = formatTimeForDisplay(data.horaSaida);
            } else {
                console.error("Erro ao obter o ponto do dia:", response.statusText);
            }
        } catch (error) {
            console.error('Erro de rede ao obter o ponto do dia:', error);
        }
    }

    /**
     * Lê os data-attributes que o Razor renderizou na primeira carga da página e formata as horas.
     */
    function initializeDisplayFromHtml() {
        const displays = [
            document.getElementById('displayEntrada'),
            document.getElementById('displaySaidaAlmoco'),
            document.getElementById('displayEntradaAlmoco'),
            document.getElementById('displaySaida')
        ];

        displays.forEach(span => {
            if (span) {
                const utcTime = span.getAttribute('data-utc-time');
                span.textContent = formatTimeForDisplay(utcTime);
            }
        });
    }

    //
    // Bloco de Configuração de Eventos
    //
    if (typeof urls !== 'undefined') {
        document.getElementById('btnEntrada')?.addEventListener('click', () => registarPonto(urls.registarEntrada, 'btnEntrada'));
        document.getElementById('btnSaidaAlmoco')?.addEventListener('click', () => registarPonto(urls.registarSaidaAlmoco, 'btnSaidaAlmoco'));
        document.getElementById('btnEntradaAlmoco')?.addEventListener('click', () => registarPonto(urls.registarEntradaAlmoco, 'btnEntradaAlmoco'));
        document.getElementById('btnSaida')?.addEventListener('click', () => registarPonto(urls.registarSaida, 'btnSaida'));
    } else {
        console.error("Objeto 'urls' não definido. Os eventos de clique não foram configurados.");
    }

    // Execução Inicial
    initializeDisplayFromHtml();
});