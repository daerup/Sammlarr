const modal = document.getElementById("components-reconnect-modal");

const retryWhenVisible = async () => {
    if (document.visibilityState === "visible") await retry();
};

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenVisible);
    try {
        if (!await Blazor.reconnect() && !await Blazor.resumeCircuit()) location.reload();
    } catch {
        document.addEventListener("visibilitychange", retryWhenVisible);
    }
}

async function resume() {
    try {
        if (!await Blazor.resumeCircuit()) location.reload();
    } catch {
        modal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

modal.addEventListener("components-reconnect-state-changed", event => {
    const { state, secondsToNextAttempt } = event.detail;
    modal.classList.toggle("reconnect-waiting", state === "retrying" && secondsToNextAttempt > 0);
    if (state === "hide") {
        modal.close();
    } else if (state === "rejected") {
        location.reload();
    } else {
        if (!modal.open) modal.showModal();
        if (state === "failed") document.addEventListener("visibilitychange", retryWhenVisible);
        if (state !== "show" && state !== "retrying") [...modal.querySelectorAll("button")].find(button => button.checkVisibility())?.focus();
    }
});

modal.addEventListener("cancel", event => event.preventDefault());
modal.addEventListener("close", () => {
    if (!modal.classList.contains("components-reconnect-hide")) modal.showModal();
});
document.getElementById("components-reconnect-button").addEventListener("click", retry);
document.getElementById("components-resume-button").addEventListener("click", resume);
