(() => {
    const phone = window.matchMedia("(width < 48rem)");
    const DISMISS_DISTANCE = 56;
    let toast = null;
    let start = null;
    let offset = { x: 0, y: 0 };

    const closeButton = alert => [...alert.querySelectorAll("button")].find(button => button.textContent.trim() === "Close");

    const follow = () => {
        const sideways = Math.abs(offset.x) > -offset.y;
        toast.style.transform = sideways ? `translateX(${offset.x}px)` : `translateY(${offset.y}px)`;
        toast.style.opacity = String(1 - Math.min(0.8, Math.max(Math.abs(offset.x), -offset.y) / 200));
    };

    const reset = alert => {
        alert.style.removeProperty("transition");
        alert.style.removeProperty("transform");
        alert.style.removeProperty("opacity");
    };

    document.addEventListener("pointerdown", event => {
        const alert = phone.matches ? event.target.closest?.(".toast-viewport [role=alert]") : null;
        if (!alert || event.target.closest("button")) return;
        toast = alert;
        start = { x: event.clientX, y: event.clientY };
        offset = { x: 0, y: 0 };
        toast.style.transition = "none";
    });

    document.addEventListener("pointermove", event => {
        if (!toast) return;
        offset = { x: event.clientX - start.x, y: Math.min(0, event.clientY - start.y) };
        follow();
    });

    const release = () => {
        if (!toast) return;
        const alert = toast;
        toast = null;
        const close = closeButton(alert);
        if (close && Math.max(Math.abs(offset.x), -offset.y) > DISMISS_DISTANCE) close.click();
        else reset(alert);
    };

    document.addEventListener("pointerup", release);
    document.addEventListener("pointercancel", release);
})();
