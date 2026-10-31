(() => {
    const phone = window.matchMedia("(width < 48rem)");
    const CLOSE_DISTANCE = 80;
    const EXIT_MS = 200;
    const RETRY_MS = 200;
    const RETRIES = 5;
    const open = [];
    const waiting = [];
    const push = history.pushState.bind(history);
    const replace = history.replaceState.bind(history);
    const go = history.go.bind(history);
    let quiet = 0;
    let href = location.href;

    const portals = () => document.querySelectorAll('[data-bb-portal][data-state="open"], [data-sheet][data-state="open"], [aria-modal="true"]:not([data-state="closed"])');
    const isTooltip = portal => portal.matches(":has(> [role=tooltip])");
    const isModal = portal => portal.querySelector('[aria-modal="true"]') !== null;
    const restyles = portal => portal.matches("[data-bb-portal]") && !isTooltip(portal) && !isModal(portal);
    const isDialog = portal => portal.matches('[data-sheet], [aria-modal="true"]');
    const tracks = portal => isDialog(portal) || restyles(portal);
    const isOpen = sheet => sheet.portal.isConnected && sheet.portal.dataset.state !== "closed";
    const top = () => open.findLast(sheet => sheet.entry);
    const current = () => open.find(sheet => sheet.entry && sheet.id === history.state?.sheet);
    const closing = sheet => sheet.portal.style.pointerEvents === "none";

    const mark = sheet => {
        push({ ...(history.state ?? {}), sheet: sheet.id }, "", location.href);
        sheet.entry = true;
    };

    const remember = sheet => {
        if (quiet > 0) waiting.push(sheet);
        else mark(sheet);
    };

    const goBackQuietly = steps => {
        quiet++;
        go(-steps);
    };

    const escape = sheet => {
        const cancel = sheet.portal.matches("[role=alertdialog]")
            ? sheet.portal.querySelector("[data-dialog-cancel]") ?? sheet.portal.querySelector(":scope > div:last-child > button:first-child")
            : null;
        if (cancel) {
            cancel.click();
            return;
        }
        const target = sheet.portal.querySelector("[role=listbox], [role=menu]") ?? sheet.portal;
        target.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true }));
    };

    const close = (sheet, tries = RETRIES) => {
        if (!closing(sheet)) escape(sheet);
        setTimeout(() => {
            if (!open.includes(sheet)) return;
            if (tries > 1) close(sheet, tries - 1);
            else if (!sheet.entry) remember(sheet);
        }, RETRY_MS);
    };

    const drag = (sheet, handle) => {
        let start = null;
        let distance = 0;
        handle.addEventListener("pointerdown", event => {
            start = event.clientY;
            distance = 0;
            sheet.portal.style.transition = "none";
            handle.setPointerCapture(event.pointerId);
        });
        handle.addEventListener("pointermove", event => {
            if (start === null) return;
            distance = Math.max(0, event.clientY - start);
            sheet.portal.style.setProperty("--sheet-drag", `${distance}px`);
        });
        const release = () => {
            if (start === null) return;
            start = null;
            sheet.portal.style.removeProperty("transition");
            if (distance > CLOSE_DISTANCE) close(sheet);
            else sheet.portal.style.removeProperty("--sheet-drag");
        };
        handle.addEventListener("pointerup", release);
        handle.addEventListener("pointercancel", release);
    };

    const restyle = sheet => {
        const handle = document.createElement("div");
        handle.className = "sheet-handle";
        handle.setAttribute("aria-hidden", "true");
        sheet.portal.prepend(handle);
        sheet.portal.style.removeProperty("--sheet-drag");
        sheet.portal.classList.add("as-sheet");
        sheet.handle = handle;
        sheet.scrim = event => {
            if (event.target === sheet.portal && sheet === open.at(-1)) close(sheet);
        };
        sheet.portal.addEventListener("click", sheet.scrim);
        drag(sheet, handle);
    };

    const unstyle = sheet => {
        sheet.handle?.remove();
        if (sheet.scrim) sheet.portal.removeEventListener("click", sheet.scrim);
        if (open.some(other => other.portal === sheet.portal)) return;
        sheet.portal.classList.remove("as-sheet", "sheet-under");
        sheet.portal.style.removeProperty("--sheet-drag");
    };

    const opened = portal => {
        const sheet = { portal, id: crypto.randomUUID(), entry: false, dialog: isDialog(portal) };
        if (restyles(portal)) restyle(sheet);
        const handle = portal.querySelector("[data-sheet-handle]");
        if (handle) drag(sheet, handle);
        open.push(sheet);
        remember(sheet);
    };

    const sync = () => {
        const closed = open.filter(sheet => !isOpen(sheet));
        closed.forEach(sheet => {
            open.splice(open.indexOf(sheet), 1);
            setTimeout(() => unstyle(sheet), EXIT_MS);
        });
        const steps = closed.filter(sheet => sheet.entry).length;
        if (steps > 0 && closed.some(sheet => sheet.id === history.state?.sheet)) goBackQuietly(steps);
        if (phone.matches) {
            portals().forEach(portal => {
                if (!open.some(sheet => sheet.portal === portal) && tracks(portal)) opened(portal);
            });
        }
        open.forEach((sheet, index) => {
            if (sheet.handle) sheet.portal.classList.toggle("sheet-under", open.slice(index + 1).some(later => later.dialog));
        });
    };

    history.pushState = (state, unused, url) => {
        const sheet = current();
        if (sheet) {
            replace(state, unused, url);
            sheet.entry = false;
        }
        else push(state, unused, url);
        href = location.href;
    };

    history.go = delta => {
        const sheet = current();
        if (sheet && delta < 0) {
            sheet.entry = false;
            go(delta - 1);
        }
        else go(delta);
    };

    history.back = () => history.go(-1);

    history.replaceState = (state, unused, url) => {
        const sheet = current();
        const before = location.href;
        replace(state, unused, url);
        if (sheet && location.href === before) replace({ ...(state ?? {}), sheet: sheet.id }, "", location.href);
        else if (sheet) sheet.entry = false;
        href = location.href;
    };

    window.addEventListener("popstate", event => {
        const moved = location.href !== href;
        href = location.href;
        if (quiet > 0) {
            quiet--;
            event.stopImmediatePropagation();
            if (quiet === 0) waiting.splice(0).filter(sheet => open.includes(sheet)).forEach(mark);
            return;
        }
        const sheet = top();
        if (sheet && event.state?.sheet !== sheet.id) {
            sheet.entry = false;
            close(sheet);
            if (!moved) event.stopImmediatePropagation();
            return;
        }
        if (event.state?.sheet && !moved && !open.some(sheet => sheet.id === event.state.sheet)) {
            event.stopImmediatePropagation();
            goBackQuietly(1);
        }
    }, { capture: true });

    document.addEventListener("keydown", event => {
        if (phone.matches && event.key === "Enter" && event.target.matches?.('input[enterkeyhint="search"]')) event.target.blur();
    }, { capture: true });

    new MutationObserver(sync).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-state"] });
    phone.addEventListener("change", sync);
})();
