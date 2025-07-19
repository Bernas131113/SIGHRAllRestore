﻿// wwwroot/js/registoPontoColaborador.js

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
    function showLoading() {
        if (loadingMsg) loadingMsg.style.display = 'block';
        if (successMsg) successMsg.style.display = 'none';
        if (errorMsg) errorMsg.style.display = 'none';
    }

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