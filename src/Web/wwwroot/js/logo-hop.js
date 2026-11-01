(() => {
    const hovers = matchMedia("(hover: hover) and (pointer: fine)");
    const calm = matchMedia("(prefers-reduced-motion: reduce)");

    document.addEventListener("click", event => {
        if (hovers.matches || calm.matches) return;
        event.target.closest?.(".sammlarr-logo")?.classList.add("is-hopping");
    });

    document.addEventListener("animationend", event => {
        if (event.target.classList?.contains("sammlarr-bird")) event.target.closest(".sammlarr-logo")?.classList.remove("is-hopping");
    });
})();
