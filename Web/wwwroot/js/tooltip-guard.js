(() => {
    const TRIGGER_SELECTOR = '[id^="tooltip-"][id$="-trigger"]';
    const TAB_WINDOW_MS = 500;

    let lastTab = -Infinity;

    const unprompted = target => target instanceof Element
        && target.matches(TRIGGER_SELECTOR)
        && performance.now() - lastTab > TAB_WINDOW_MS;

    const abandoned = portal => portal.dataset.state === "closed"
        && portal.style.pointerEvents === "none"
        && portal.style.visibility === "visible";

    const hide = portal => requestAnimationFrame(() => {
        if (abandoned(portal)) {
            portal.style.setProperty("visibility", "hidden", "important");
            portal.style.setProperty("opacity", "0", "important");
        }
    });

    window.addEventListener("keydown", event => {
        if (event.key === "Tab") lastTab = performance.now();
    }, true);

    window.addEventListener("focus", event => {
        if (unprompted(event.target)) event.stopImmediatePropagation();
    }, true);

    new MutationObserver(records => records
        .map(record => record.target)
        .filter(portal => portal.matches("[data-bb-portal]:has(> [role=tooltip])") && abandoned(portal))
        .forEach(hide))
        .observe(document.body, { subtree: true, attributes: true, attributeFilter: ["data-state"] });
})();
