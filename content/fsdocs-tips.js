let currentTip = null;
let currentTipElement = null;

function hideTip(name) {
    const el = document.getElementById(name);
    if (el) {
        try { el.hidePopover(); } catch (_) { }
    }
    currentTip = null;
    currentTipElement = null;
}

function showTip(evt, name, unique) {
    if (currentTip === unique) return;

    // Hide the previously shown tooltip before showing the new one
    if (currentTipElement !== null) {
        const prev = document.getElementById(currentTipElement);
        if (prev) {
            try { prev.hidePopover(); } catch (_) { }
        }
    }

    currentTip = unique;
    currentTipElement = name;

    const offset = 20;
    let x = evt.clientX;
    let y = evt.clientY + offset;

    const el = document.getElementById(name);
    const maxWidth = document.documentElement.clientWidth - x - 16;
    el.style.maxWidth = `${maxWidth}px`;
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;

    try { el.showPopover(); } catch (_) { }

    const rect = el.getBoundingClientRect();
    // Move tooltip if it would appear outside the viewport
    if (rect.bottom > window.innerHeight) {
        y = y - el.clientHeight - offset;
        el.style.top = `${y}px`;
    }
    if (rect.right > window.innerWidth) {
        x = x - el.clientWidth - offset;
        el.style.left = `${x}px`;
        el.style.maxWidth = `${document.documentElement.clientWidth - x - 16}px`;
    }
}

// Event delegation: trigger tooltips from data-fsdocs-tip attributes
document.addEventListener('mouseover', function (evt) {
    const target = evt.target.closest('[data-fsdocs-tip]');
    if (!target) return;
    const name = target.dataset.fsdocsTip;
    const unique = parseInt(target.dataset.fsdocsTipUnique, 10);
    showTip(evt, name, unique);
});

document.addEventListener('mouseout', function (evt) {
    const target = evt.target.closest('[data-fsdocs-tip]');
    if (!target) return;
    // Only hide when the mouse has left the trigger element entirely
    if (target.contains(evt.relatedTarget)) return;
    const name = target.dataset.fsdocsTip;
    const unique = parseInt(target.dataset.fsdocsTipUnique, 10);
    hideTip(name);
});

function Clipboard_CopyTo(value) {
    if (navigator.clipboard) {
        navigator.clipboard.writeText(value);
    } else {
        const tempInput = document.createElement("input");
        tempInput.value = value;
        document.body.appendChild(tempInput);
        tempInput.select();
        document.execCommand("copy");
        document.body.removeChild(tempInput);
    }
}

window.Clipboard_CopyTo = Clipboard_CopyTo;
