﻿// wwwroot/js/adminGestaoFaltas.js

let API_URL_ADMIN_FALTAS_LISTAR;
let API_URL_ADMIN_FALTAS_EXCLUIR;
let todasAsFaltasAdminCarregadas = [];

function inicializarGestaoFaltasAdmin(urls) {
    API_URL_ADMIN_FALTAS_LISTAR = urls.listar;
    API_URL_ADMIN_FALTAS_EXCLUIR = urls.excluir;

    // Associa as funções aos respetivos botões de filtro e exclusão.
    document.querySelector('#gestaoFaltasAdminContent .filter-btn.apply')?.addEventListener('click', aplicarFiltrosFaltasAdmin);
    document.querySelector('#gestaoFaltasAdminContent .filter-btn.clear')?.addEventListener('click', limparFiltrosFaltasAdmin);

    document.getElementById('btn-ativar-excluir-faltas')?.addEventListener('click', ativarModoExclusaoFaltasAdmin);
    document.getElementById('btn-confirmar-excluir-faltas')?.addEventListener('click', excluirFaltasSelecionadasAdmin);
    document.getElementById('btn-cancelar-excluir-faltas')?.addEventListener('click', cancelarModoExclusaoFaltasAdmin);
    document.getElementById('selecionar-todas-faltas')?.addEventListener('change', function () { toggleSelecionarTodasFaltasAdmin(this); });

    // Carrega os dados iniciais na tabela ao entrar na página.
    carregarTodasAsFaltasDaApi();
}

/**
 * Formata uma data no formato ISO para "dd/mm/aaaa".
 */
function formatarDataParaAdmin(dataISO) {
    if (!dataISO || dataISO.startsWith('0001-01-01')) return 'N/D';
    try {
        const data = new Date(dataISO);
        return data.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' });
    } catch (e) { return 'Data Inválida'; }
}

/**
 * Função para formatar a hora de um objeto Date para HH:MM no fuso horário local e em formato de 24 horas.
 */
function formatarHoraParaAdmin(dateString) {
    if (!dateString || dateString.startsWith('0001-01-01')) {
        return '--:--';
    }
    try {
        const date = new Date(dateString); // A string será ISO 8601 UTC
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

async function carregarTodasAsFaltasDaApi() {
    const tbody = document.getElementById('tabela-gestao-todas-faltas')?.querySelector('tbody');
    const divNenhuma = document.getElementById('nenhuma-falta-admin-view');
    if (!tbody || !divNenhuma) return;

    tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;">A carregar faltas...</td></tr>';
    divNenhuma.style.display = 'none';

    const filtroNomeVal = document.getElementById('filtro-nome-falta').value;
    const filtroDataVal = document.getElementById('filtro-data-falta').value;
    let url = new URL(API_URL_ADMIN_FALTAS_LISTAR, window.location.origin);
    if (filtroNomeVal) url.searchParams.append('filtroNome', filtroNomeVal);
    if (filtroDataVal) url.searchParams.append('filtroData', filtroDataVal);

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);
        const data = await response.json();
        todasAsFaltasAdminCarregadas = data;
        renderizarTabelaAdminFaltas(todasAsFaltasAdminCarregadas);
    } catch (error) {
        console.error('Erro ao carregar todas as faltas (Admin):', error);
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; color:red;">Erro ao carregar faltas: ${error.message}</td></tr>`;
    }
}

function renderizarTabelaAdminFaltas(faltas) {
    const tbody = document.getElementById('tabela-gestao-todas-faltas').querySelector('tbody');
    const divNenhuma = document.getElementById('nenhuma-falta-admin-view');
    tbody.innerHTML = '';

    if (faltas.length === 0) {
        divNenhuma.style.display = 'block';
    } else {
        divNenhuma.style.display = 'none';
        faltas.forEach(falta => {
            const tr = tbody.insertRow();
            tr.dataset.faltaId = falta.faltaId;
            tr.insertCell().innerHTML = `<input type="checkbox" class="delete-checkbox-row delete-checkbox" value="${falta.faltaId}" onchange="verificarSelecaoParaExcluirFaltasAdmin()">`;
            tr.insertCell().textContent = falta.nomeUtilizador;
            tr.insertCell().textContent = formatarDataParaAdmin(falta.dataFalta);

            // Usa a nova função para formatar a hora de início e fim
            tr.insertCell().textContent = formatarHoraParaAdmin(falta.inicio);
            tr.insertCell().textContent = formatarHoraParaAdmin(falta.fim);

            tr.insertCell().textContent = falta.motivo;
            tr.insertCell().textContent = formatarDataParaAdmin(falta.dataRegisto);
        });
    }
    atualizarVisibilidadeCheckboxesAdmin();
}


function aplicarFiltrosFaltasAdmin() {
    carregarTodasAsFaltasDaApi();
}

function limparFiltrosFaltasAdmin() {
    document.getElementById('filtro-nome-falta').value = '';
    document.getElementById('filtro-data-falta').value = '';
    carregarTodasAsFaltasDaApi();
}

function ativarModoExclusaoFaltasAdmin() {
    document.querySelectorAll('#tabela-gestao-todas-faltas .delete-checkbox').forEach(cb => cb.style.display = 'inline-block');
    document.getElementById('btn-ativar-excluir-faltas').style.display = 'none';
    document.getElementById('btn-confirmar-excluir-faltas').style.display = 'inline-block';
    document.getElementById('btn-cancelar-excluir-faltas').style.display = 'inline-block';
    document.getElementById('btn-confirmar-excluir-faltas').disabled = true;
    document.getElementById('selecionar-todas-faltas').style.display = 'inline-block';
}

function cancelarModoExclusaoFaltasAdmin() {
    document.querySelectorAll('#tabela-gestao-todas-faltas .delete-checkbox').forEach(cb => { cb.style.display = 'none'; cb.checked = false; });
    document.getElementById('btn-ativar-excluir-faltas').style.display = 'inline-block';
    document.getElementById('btn-confirmar-excluir-faltas').style.display = 'none';
    document.getElementById('btn-cancelar-excluir-faltas').style.display = 'none';
    document.getElementById('selecionar-todas-faltas').checked = false;
    document.getElementById('selecionar-todas-faltas').style.display = 'none';
}

function toggleSelecionarTodasFaltasAdmin(selectAllCheckbox) {
    document.querySelectorAll('#tabela-gestao-todas-faltas .delete-checkbox-row').forEach(cb => { cb.checked = selectAllCheckbox.checked; });
    verificarSelecaoParaExcluirFaltasAdmin();
}

function verificarSelecaoParaExcluirFaltasAdmin() {
    const algumSelecionado = Array.from(document.querySelectorAll('#tabela-gestao-todas-faltas .delete-checkbox-row:checked')).length > 0;
    document.getElementById('btn-confirmar-excluir-faltas').disabled = !algumSelecionado;
    const todasLinhas = document.querySelectorAll('#tabela-gestao-todas-faltas tbody .delete-checkbox-row');
    if (todasLinhas.length > 0) {
        document.getElementById('selecionar-todas-faltas').checked = Array.from(todasLinhas).every(cb => cb.checked);
    } else {
        document.getElementById('selecionar-todas-faltas').checked = false;
    }
}

async function excluirFaltasSelecionadasAdmin() {
    const checkboxes = document.querySelectorAll('#tabela-gestao-todas-faltas .delete-checkbox-row:checked');
    const ids = Array.from(checkboxes).map(cb => parseInt(cb.value));
    if (ids.length === 0 || !confirm(`Excluir ${ids.length} falta(s) selecionada(s)?`)) return;

    try {
        const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const headers = { 'Content-Type': 'application/json' };
        if (antiForgeryToken) headers['RequestVerificationToken'] = antiForgeryToken;

        const response = await fetch(API_URL_ADMIN_FALTAS_EXCLUIR, {
            method: 'POST', headers: headers, body: JSON.stringify(ids)
        });
        if (response.ok) {
            alert('Falta(s) excluída(s) com sucesso!');
            carregarTodasAsFaltasDaApi();
            cancelarModoExclusaoFaltasAdmin();
        } else {
            const err = await response.json().catch(() => ({ message: response.statusText }));
            alert(`Erro ao excluir: ${err.message}`);
        }
    } catch (error) {
        console.error('Erro ao excluir faltas (Admin):', error);
        alert('Erro de comunicação ao excluir faltas.');
    }
}

function atualizarVisibilidadeCheckboxesAdmin() {
    const emModoExclusao = document.getElementById('btn-confirmar-excluir-faltas')?.style.display !== 'none';
    document.querySelectorAll('#tabela-gestao-todas-faltas .delete-checkbox-row').forEach(cb => {
        cb.style.display = emModoExclusao ? 'inline-block' : 'none';
    });
    document.getElementById('selecionar-todas-faltas').style.display = emModoExclusao ? 'inline-block' : 'none';
    if (emModoExclusao) verificarSelecaoParaExcluirFaltasAdmin();
}