window.focusSearchInput = (elementId) => {
    const host = document.getElementById(elementId);
    if (!host) {
        return;
    }

    if (typeof host.focus === "function") {
        host.focus();
    }

    const input = host.matches("input") ? host : host.querySelector("input");
    if (!input) {
        return;
    }

    input.focus();
    if (typeof input.select === "function") {
        input.select();
    }
};

window.registerSearchModalEscape = (dotNetRef) => {
    window.unregisterSearchModalEscape();

    const handler = (event) => {
        if (event.key !== "Escape") {
            return;
        }

        event.preventDefault();
        dotNetRef.invokeMethodAsync("CloseFromEscapeAsync");
    };

    document.addEventListener("keydown", handler, true);
    window.__searchModalEscapeHandler = handler;
};

window.unregisterSearchModalEscape = () => {
    if (!window.__searchModalEscapeHandler) {
        return;
    }

    document.removeEventListener("keydown", window.__searchModalEscapeHandler, true);
    window.__searchModalEscapeHandler = null;
};

window.registerBrowseSearchShortcut = (dotNetRef) => {
    window.unregisterBrowseSearchShortcut();

    const handler = (event) => {
        const isShortcut = (event.ctrlKey || event.metaKey)
            && !event.shiftKey
            && !event.altKey
            && typeof event.key === "string"
            && event.key.toLowerCase() === "k";

        if (!isShortcut) {
            return;
        }

        const target = event.target;
        const tagName = target && target.tagName ? target.tagName.toLowerCase() : "";
        const isTypingTarget = target && (target.isContentEditable || tagName === "input" || tagName === "textarea" || tagName === "select");
        if (isTypingTarget) {
            return;
        }

        event.preventDefault();
        dotNetRef.invokeMethodAsync("OpenSearchFromShortcutAsync");
    };

    document.addEventListener("keydown", handler, true);
    window.__browseSearchShortcutHandler = handler;
};

window.unregisterBrowseSearchShortcut = () => {
    if (!window.__browseSearchShortcutHandler) {
        return;
    }

    document.removeEventListener("keydown", window.__browseSearchShortcutHandler, true);
    window.__browseSearchShortcutHandler = null;
};

