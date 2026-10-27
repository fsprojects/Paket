// Automatically scroll to the active aside menu item.
const mainMenu = document.getElementById('fsdocs-main-menu');
function scrollToActiveItem(activeItem) {
    const halfMainMenuHeight = mainMenu.offsetHeight / 2
    if(activeItem.offsetTop > halfMainMenuHeight){
        mainMenu.scrollTop = (activeItem.offsetTop - halfMainMenuHeight) - (activeItem.offsetHeight / 2);
    }
}

const activeItem = document.querySelector("aside .nav-item.active");
if (activeItem && mainMenu) {
    scrollToActiveItem(activeItem);
}

function scrollToAndExpandSelectedMember() {
    if (location.hash) {
        const details = document.querySelector(`tr > td.fsdocs-member-usage:has(a[href='${location.hash}']) ~ td.fsdocs-member-xmldoc > details`);
        details?.setAttribute('open', 'true');
        const header = document.querySelector(`a[href='${location.hash}']`);
        header?.scrollIntoView({ behavior: 'instant'});
    }
}

scrollToAndExpandSelectedMember();
addEventListener('hashchange', scrollToAndExpandSelectedMember);

if(location.pathname.startsWith('/reference/')) {
    // Scroll to API Reference header
    const navHeaders = document.querySelectorAll(".nav-header");
    for (const navHeader of navHeaders) {
        if (navHeader.textContent && navHeader.textContent.trim() === 'API Reference') {
            scrollToActiveItem(navHeader);
        }
    }
}