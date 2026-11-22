(() => {
    const TRIGGER_SELECTOR = '[id^="tooltip-"][id$="-trigger"]';
    const TAB_WINDOW_MS = 500;

    let lastTab = -Infinity;

    const unprompted = target => target instanceof Element
        && target.matches(TRIGGER_SELECTOR)
        && performance.now() - lastTab > TAB_WINDOW_MS;

    window.addEventListener("keydown", event => {
        if (event.key === "Tab") lastTab = performance.now();
    }, true);

    window.addEventListener("focus", event => {
        if (unprompted(event.target)) event.stopImmediatePropagation();
    }, true);
})();
