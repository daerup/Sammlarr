export function watchEnd(sentinel, owner) {
    if (!sentinel) {
        return null;
    }

    const observer = new IntersectionObserver(
        entries => owner.invokeMethodAsync('OnDockedChanged', entries[0].isIntersecting),
        { root: scrollParent(sentinel), threshold: 0.95}
    );
    observer.observe(sentinel);

    return { dispose: () => observer.disconnect() };
}

function scrollParent(element) {
    for (let node = element.parentElement; node; node = node.parentElement) {
        const overflow = getComputedStyle(node).overflowY;
        if (overflow === 'auto' || overflow === 'scroll') {
            return node;
        }
    }

    return null;
}
