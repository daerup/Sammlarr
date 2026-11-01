export function watch(owner) {
    const query = matchMedia('(width < 48rem)');
    const report = () => owner.invokeMethodAsync('Changed', query.matches);
    query.addEventListener('change', report);
    report();
    return {
        dispose: () => query.removeEventListener('change', report),
    };
}
