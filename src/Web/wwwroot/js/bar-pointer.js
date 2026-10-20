(() => {
    window.addEventListener("pointermove", event => {
        const track = event.target instanceof Element ? event.target.closest(".sb-track") : null;
        if (track) track.style.setProperty("--sb-x", `${event.clientX - track.getBoundingClientRect().left}px`);
    }, { passive: true });
})();
