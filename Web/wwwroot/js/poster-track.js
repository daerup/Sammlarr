const EDGE = 8;

function page(track) {
    return Math.max(track.clientWidth - 140, 240);
}

function state(track) {
    return {
        canLeft: track.scrollLeft > EDGE,
        canRight: track.scrollLeft + track.clientWidth < track.scrollWidth - EDGE,
    };
}

export function observe(track, owner) {
    if (!track) {
        return null;
    }

    const report = () => {
        const { canLeft, canRight } = state(track);
        owner.invokeMethodAsync('OnScrollState', canLeft, canRight);
    };

    const resize = new ResizeObserver(report);
    resize.observe(track);
    track.addEventListener('scroll', report, { passive: true });
    report();

    return {
        report,
        dispose: () => {
            resize.disconnect();
            track.removeEventListener('scroll', report);
        },
    };
}

export function scrollForward(track) {
    if (track) {
        track.scrollBy({ left: page(track), behavior: 'smooth' });
    }
}

export function scrollBack(track) {
    if (track) {
        track.scrollBy({ left: -page(track), behavior: 'smooth' });
    }
}
