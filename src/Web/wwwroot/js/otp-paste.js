(() => {
    const BOX_SELECTOR = "[data-otp] > input:not([type=hidden])";

    const isBox = target => target instanceof HTMLInputElement && target.matches(BOX_SELECTOR);

    const fill = (box, character) => {
        box.value = character;
        box.dispatchEvent(new Event("input", { bubbles: true }));
    };

    window.addEventListener("beforeinput", event => {
        if (isBox(event.target) && event.inputType.startsWith("insert") && event.data?.length !== 1) event.target.removeAttribute("maxlength");
    }, true);

    window.addEventListener("input", event => {
        const box = event.target;
        if (!isBox(box)) return;
        box.setAttribute("maxlength", "1");
        if (box.value.length < 2) return;
        event.stopImmediatePropagation();
        const boxes = [...box.parentElement.querySelectorAll(":scope > input:not([type=hidden])")];
        const targets = boxes.slice(boxes.indexOf(box));
        const characters = [...box.value].filter(character => /[a-z0-9]/i.test(character));
        characters.slice(0, targets.length).forEach((character, index) => fill(targets[index], character));
    }, true);
})();
