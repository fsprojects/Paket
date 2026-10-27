import Fuse from "https://esm.sh/fuse.js@7.0.0";

const searchBtn = document.querySelector("#search-btn");

function hideSearchBtn() {
    // Hide search icon if we can't search in the first place.
    searchBtn.style.display = 'none';
}

function debounce(mainFunction, delay) {
    // Declare a variable called 'timer' to store the timer ID
    let timer;

    // Return an anonymous function that takes in any number of arguments
    return function (...args) {
        // Clear the previous timer to prevent the execution of 'mainFunction'
        clearTimeout(timer);

        // Set a new timer that will execute 'mainFunction' after the specified delay
        timer = setTimeout(() => {
            mainFunction(...args);
        }, delay);
    };
}

const root = document.documentElement.getAttribute("data-root");
if (root && searchBtn) {
    let fuse = null;
    const searchIndexUrl = `${root}/index.json`;
    fetch(searchIndexUrl, {})
        .then(response => response.json())
        .then(index => {
            fuse = new Fuse(index, {
                includeScore: true,
                keys: ['uri', 'title', 'content', 'headings'],
                includeMatches: true,
                limit: 20,
                ignoreLocation: true,
                threshold: 0.6,
                minMatchCharLength: 2,
                ignoreFieldNorm: true,
                shouldSort: true
            });
        })
        .catch(() => {
            hideSearchBtn();
        })

    const searchDialog = document.querySelector("dialog");
    const empty = document.querySelector("dialog .empty");
    const resultsElement = document.querySelector("dialog ul");
    const searchBox = document.querySelector("dialog input[type=search]");

    function openSearch() {
        searchDialog.showModal();
        searchBox.focus();
    }

    function closeSearch() {
        searchBox.value = '';
        empty.textContent = "Type something to start searching.";
        clearResults();
        searchDialog.close();
    }

    searchBtn.addEventListener("click", openSearch)

    searchDialog.addEventListener("click", ev => {
        if (ev.target.tagName === "DIALOG") {
            closeSearch();
        }
    })

    function searchAux(searchTerm) {
        if (!fuse) return;

        const results = fuse.search(searchTerm);
        if (results.length === 0) {
            clearResults();
            empty.textContent = "No results were found";
        } else {
            if (location.hostname === 'localhost'){
                console.table(results);
            }

            empty.style.display = 'none';
            const newResultNodes =
                results
                    .map(result => {
                        const item = result.item;
                        const li = document.createElement("li");
                        const a = document.createElement("a");
                        a.setAttribute("href", item.uri);
                        const icon = document.createElement("iconify-icon");
                        icon.setAttribute("width", "24");
                        icon.setAttribute("height", "24");
                        icon.setAttribute("icon", item.type === "content" ? "iconoir:page" : "bxs:file-doc")
                        a.append(icon, item.title);
                        li.appendChild(a);
                        return li;
                    });
            resultsElement.replaceChildren(...newResultNodes);
        }
    }

    const search = debounce(searchAux, 250);

    function clearResults() {
        empty.style.display = 'block';
        resultsElement.replaceChildren();
    }

    searchBox.addEventListener('keyup', ev => {
        ev.stopPropagation();
        const searchTerm = ev.target.value;
        if (!searchTerm) {
            empty.textContent = "Type something to start searching.";
            clearResults();
        } else {
            search(searchTerm);
        }
    });

    window.addEventListener('keydown', ev => {
        if (ev.key === 'Escape' && searchDialog.open) {
            ev.preventDefault();
            closeSearch();
        }

        const focusedTag = document.activeElement?.tagName;
        const isTypingInInput = focusedTag === 'INPUT' || focusedTag === 'TEXTAREA';
        const isShortcut =
            (ev.key === '/' && !isTypingInInput) ||
            (ev.key === 'k' && (ev.ctrlKey || ev.metaKey));

        if (isShortcut && !searchDialog.open) {
            ev.preventDefault();
            openSearch();
        }
    })
} else {
    hideSearchBtn();
}
