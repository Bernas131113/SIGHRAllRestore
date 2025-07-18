// wwwroot/js/layout-scripts.js

document.addEventListener('DOMContentLoaded', function () {
    const hamburgerButton = document.getElementById('hamburgerButton');
    const mainSidebar = document.getElementById('mainSidebar');
    const sidebarOverlay = document.getElementById('sidebarOverlay');

    if (hamburgerButton && mainSidebar && sidebarOverlay) {
        // Abre/Fecha a sidebar ao clicar no botão do hambúrguer
        hamburgerButton.addEventListener('click', function () {
            mainSidebar.classList.toggle('active');
            sidebarOverlay.classList.toggle('active');
        });

        // Fecha a sidebar ao clicar fora dela (no overlay)
        sidebarOverlay.addEventListener('click', function () {
            mainSidebar.classList.remove('active');
            sidebarOverlay.classList.remove('active');
        });

        // Opcional: Fecha a sidebar ao clicar num link (boa UX em mobile)
        mainSidebar.querySelectorAll('a').forEach(link => {
            link.addEventListener('click', function () {
                if (window.innerWidth <= 768) { // Apenas em ecrãs pequenos
                    mainSidebar.classList.remove('active');
                    sidebarOverlay.classList.remove('active');
                }
            });
        });
    } else {
        console.warn("Elementos do menu hambúrguer não encontrados. Verifique IDs no HTML.");
    }
});