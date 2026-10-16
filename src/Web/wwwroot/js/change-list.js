export function scrollToSection(body, kind, offset) {
    const section = body.querySelector(`.change-section[data-kind="${kind}"]`);
    if (section) {
        body.scrollTop += section.getBoundingClientRect().top - body.getBoundingClientRect().top + offset;
    }
}
