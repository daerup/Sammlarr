const ROW_GAP = 12;

function layoutItem(item) {
    item.style.gridRowEnd = `span ${Math.ceil(item.getBoundingClientRect().height) + ROW_GAP}`;
}

export function init(grid) {
    if (!grid) {
        return;
    }
    if (grid.__masonryObserver) {
        grid.__masonryObserver.disconnect();
    }
    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            layoutItem(entry.target);
        }
    });
    for (const item of grid.children) {
        layoutItem(item);
        observer.observe(item);
    }
    grid.__masonryObserver = observer;
}

export function dispose(grid) {
    if (grid && grid.__masonryObserver) {
        grid.__masonryObserver.disconnect();
        grid.__masonryObserver = null;
    }
}
