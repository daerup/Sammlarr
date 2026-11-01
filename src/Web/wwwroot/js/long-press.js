(() => {
    const HOLD_MS = 450;
    const SLOP = 10;
    let timer = 0;
    let start = null;
    let swallow = false;

    const cancel = () => {
        clearTimeout(timer);
        timer = 0;
        start = null;
    };

    document.addEventListener("pointerdown", event => {
        swallow = false;
        const target = event.button === 0 ? event.target.closest?.("[data-long-press]") : null;
        if (!target) return;
        start = { x: event.clientX, y: event.clientY };
        timer = setTimeout(() => {
            timer = 0;
            swallow = true;
            navigator.vibrate?.(10);
            target.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true, shiftKey: true }));
        }, HOLD_MS);
    });

    document.addEventListener("pointermove", event => {
        if (start && Math.hypot(event.clientX - start.x, event.clientY - start.y) > SLOP) cancel();
    });

    document.addEventListener("pointerup", cancel);
    document.addEventListener("pointercancel", cancel);

    window.addEventListener("click", event => {
        if (swallow && event.isTrusted) {
            swallow = false;
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    }, { capture: true });

    document.addEventListener("contextmenu", event => {
        if (event.target.closest?.("[data-long-press]")) event.preventDefault();
    });
})();
