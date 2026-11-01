const SLOP = 5;

export function watch(dial, owner) {
    if (!dial) {
        return null;
    }

    let start = null;
    let dragging = false;
    let last = -1;
    let swallow = false;

    const minuteAt = event => {
        const box = dial.getBoundingClientRect();
        const degrees = Math.atan2(event.clientX - (box.left + box.width / 2), box.top + box.height / 2 - event.clientY) * 180 / Math.PI;
        return Math.round((degrees + 360) % 360 / 6) % 60;
    };

    const down = event => {
        if (dial.dataset.kind === 'Minute') {
            start = { x: event.clientX, y: event.clientY };
        }
    };

    const move = event => {
        if (!start || (!dragging && Math.hypot(event.clientX - start.x, event.clientY - start.y) < SLOP)) {
            return;
        }
        if (!dragging) {
            dragging = true;
            dial.setPointerCapture(event.pointerId);
            dial.classList.add('is-dragging');
        }
        const minute = minuteAt(event);
        if (minute !== last) {
            last = minute;
            owner.invokeMethodAsync('DragTo', minute);
        }
    };

    const up = () => {
        swallow = dragging;
        if (dragging) {
            dial.classList.remove('is-dragging');
        }
        start = null;
        dragging = false;
        last = -1;
    };

    const click = event => {
        if (swallow) {
            swallow = false;
            event.preventDefault();
            event.stopPropagation();
        }
    };

    dial.addEventListener('pointerdown', down);
    dial.addEventListener('pointermove', move);
    dial.addEventListener('pointerup', up);
    dial.addEventListener('pointercancel', up);
    dial.addEventListener('click', click, true);

    return {
        dispose: () => {
            dial.removeEventListener('pointerdown', down);
            dial.removeEventListener('pointermove', move);
            dial.removeEventListener('pointerup', up);
            dial.removeEventListener('pointercancel', up);
            dial.removeEventListener('click', click, true);
        },
    };
}
