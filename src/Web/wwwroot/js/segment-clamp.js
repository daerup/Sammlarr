(() => {
    const SEGMENT_SELECTOR = "[data-clamp] input[data-bb-segment]";

    window.addEventListener("input", event => {
        const segment = event.target;
        if (!(segment instanceof HTMLInputElement) || !segment.matches(SEGMENT_SELECTOR)) return;
        const max = Number(segment.dataset.max);
        if (Number(segment.value) > max) segment.value = String(max).padStart(Number(segment.dataset.digits), "0");
    }, true);
})();
