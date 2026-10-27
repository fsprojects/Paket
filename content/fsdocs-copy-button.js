// Adds a "Copy" button to every code block so readers can easily copy snippets.
function createCopyButton() {
    const button = document.createElement('button')
    button.className = 'copy-code-button'
    button.setAttribute('aria-label', 'Copy code to clipboard')
    button.textContent = 'Copy'
    return button
}

function attachCopyHandler(button, getText) {
    button.addEventListener('click', function () {
        const text = getText()
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(
                function () {
                    button.textContent = 'Copied!'
                    setTimeout(function () {
                        button.textContent = 'Copy'
                    }, 2000)
                },
                function () {
                    button.textContent = 'Failed'
                    setTimeout(function () {
                        button.textContent = 'Copy'
                    }, 2000)
                }
            )
        } else {
            // Fallback for non-HTTPS environments
            const el = document.createElement('textarea')
            el.value = text
            document.body.appendChild(el)
            el.select()
            document.execCommand('copy')
            document.body.removeChild(el)
            button.textContent = 'Copied!'
            setTimeout(function () {
                button.textContent = 'Copy'
            }, 2000)
        }
    })
}

document.addEventListener('DOMContentLoaded', function () {
    // table.pre blocks (F# highlighted code, sometimes with line numbers)
    document.querySelectorAll('table.pre').forEach(function (table) {
        const wrapper = document.createElement('div')
        wrapper.className = 'code-block-wrapper'
        table.parentNode.insertBefore(wrapper, table)
        wrapper.appendChild(table)

        const button = createCopyButton()
        wrapper.appendChild(button)

        const snippet = table.querySelector('.snippet pre')
        attachCopyHandler(button, function () {
            return (snippet || table).innerText
        })
    })

    // Standard pre > code blocks (Markdown fenced code, standalone fssnip, etc.)
    // Skip those already handled inside table.pre above.
    document.querySelectorAll('pre > code').forEach(function (code) {
        if (code.closest('table.pre')) return
        const pre = code.parentElement
        pre.classList.add('has-copy-button')

        const button = createCopyButton()
        pre.appendChild(button)

        attachCopyHandler(button, function () {
            return code.innerText
        })
    })
})
