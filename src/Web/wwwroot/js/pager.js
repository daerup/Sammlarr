export function watchEnd(sentinel, owner, phone) {
    if (!sentinel) {
        return null;
    }

    const observer = new IntersectionObserver(
        entries => owner.invokeMethodAsync('OnEndInView', entries[0].intersectionRatio >= 0.95),
        { root: scrollParent(sentinel), rootMargin: phone ? '0px 0px 600px 0px' : '0px', threshold: 0.95 }
    );
    observer.observe(sentinel);

    return {
        recheck: () => {
            observer.unobserve(sentinel);
            observer.observe(sentinel);
        },
        dispose: () => observer.disconnect()
    };
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
