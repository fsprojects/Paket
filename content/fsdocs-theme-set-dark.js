const savedTheme = localStorage.getItem('theme');
if (savedTheme) {
    window.document.documentElement.setAttribute("data-theme", savedTheme);
}
