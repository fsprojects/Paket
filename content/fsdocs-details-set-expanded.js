const expandDetails = !!JSON.parse(localStorage.getItem('details-expanded'));

addEventListener('load', _ => {
    if (expandDetails) {
        for (const details of document.getElementsByTagName('details')) {
            details.setAttribute('open', 'true');
        }
    }
});
